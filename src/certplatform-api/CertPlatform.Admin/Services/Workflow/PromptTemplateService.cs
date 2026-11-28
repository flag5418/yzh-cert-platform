
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.Domain.Scope;
using YZH.Core.DataBase.Interfaces;
// 消歧：Entities.Wf 与 Entities.Cert 均有 PromptTemplate
// ★ 2026-11-27：wf_prompt_template 已退役，本服务唯一数据源 = cert_prompt_template
using PromptTemplate = CertPlatform.Admin.Entities.Cert.PromptTemplate;

namespace CertPlatform.Admin.Services.Workflow;

/// <summary>
/// 提示词工作台数据层（原 Prompt 模板服务移植）。
///
/// <para><b>★ 2026-11-27 口径变更</b>：唯一数据源 = <c>cert_prompt_template</c>
/// （<c>wf_prompt_template</c> 已 RENAME 退役）。字段对应：</para>
/// <list type="bullet">
///   <item><c>PromptCode</c> = 提示词<b>类型</b>（doc_group / doc_content / clause_attr_gen …）</item>
///   <item><c>Code</c> = 行<b>业务键</b>（= 工作台前端的 <c>promptCode</c>）</item>
///   <item><c>ScopeKey</c> = 标准<b>短码</b>（工作台前端传/收的是 GUID，由本服务负责换算）</item>
///   <item>启用/禁用唯一字段 = <c>IsValid</c>（铁律九，cert 表无 <c>IsActive</c>）</item>
/// </list>
/// </summary>
public class PromptTemplateService
{
    private readonly IDbOrm _db;
    private readonly ICoreResourceQuery _core;
    private readonly ILogger<PromptTemplateService> _logger;

    public PromptTemplateService(IDbOrm db, ICoreResourceQuery core, ILogger<PromptTemplateService> logger)
    {
        _db = db;
        _core = core;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、标准口径换算（工作台前端用 GUID，库里 ScopeKey 存短码）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>标准 GUID（<c>cert_iso_standard.Code</c>）→ 可读短码（<c>StandardCode</c>）。</summary>
    public async Task<string?> ResolveStandardSlugAsync(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return null;
        var r = await _core.GetStandardByCodeAsync(standardCode);
        if (r != null && !string.IsNullOrWhiteSpace(r.StandardCode))
            return r.StandardCode;
        // 已经是短码（老数据兜底）就直接用
        return standardCode;
    }

    /// <summary>标准短码 → GUID（DTO 下发用：前端作用域键 = GUID）。</summary>
    public async Task<string?> ResolveStandardGuidAsync(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var r = await _core.ResolveStandardAsync(slug);
        return r?.Code;
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、读取
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 按类型取提示词列表（<paramref name="standardCode"/> 为 GUID 时按标准短码过滤，
    /// 并保留平台级 <c>ScopeKey</c> 为空的兜底行）。
    /// </summary>
    public async Task<List<PromptTemplate>> GetListAsync(string? promptType, string? standardCode)
    {
        var result = await _db.GetListAsync<PromptTemplate>(x =>
            x.IsValid == 1 && x.IsDeleted == false
            && (string.IsNullOrEmpty(promptType) || x.PromptCode == promptType));

        if (!result.Success)
        {
            _logger.LogWarning("Prompt 模板列表查询失败：{Error}", result.Error);
            return new List<PromptTemplate>();
        }

        var list = result.Data ?? new List<PromptTemplate>();
        if (!string.IsNullOrWhiteSpace(standardCode))
        {
            var slug = await ResolveStandardSlugAsync(standardCode);
            list = list.Where(x =>
                string.Equals(x.ScopeKey, slug, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(x.ScopeKey)).ToList();
        }

        return list.OrderBy(x => x.PromptCode).ThenBy(x => x.ScopeKey).ToList();
    }

    /// <summary>按行业务键 <c>Code</c> 取一条。</summary>
    public async Task<PromptTemplate?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var result = await _db.GetOneAsync<PromptTemplate>(
            x => x.Code == code && x.IsValid == 1 && x.IsDeleted == false);
        return result.Success ? result.Data : null;
    }

    /// <summary>
    /// 取指定类型当前生效的提示词（标准级 → 平台级回退）。
    /// <para>★ 与 <c>PromptWorkbenchService.ResolveActiveAsync</c> 同一口径。</para>
    /// </summary>
    public async Task<PromptTemplate?> GetActiveAsync(string promptType, string? standardCode = null)
    {
        if (string.IsNullOrWhiteSpace(promptType)) return null;

        var list = await GetListAsync(promptType, standardCode);
        if (list.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(standardCode))
        {
            var slug = await ResolveStandardSlugAsync(standardCode);
            var specific = list.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.ScopeKey)
                && string.Equals(x.ScopeKey, slug, StringComparison.OrdinalIgnoreCase));
            if (specific != null) return specific;
        }

        return list.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.ScopeKey)) ?? list[0];
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、保存 / 删除 / 激活
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建或更新提示词（按行业务键 <c>Code</c> 幂等 upsert：无则新增、有则覆盖正文）。
    /// <para>准则 A：定位 / 分流只用 <c>Code</c>，⛔ 不用 <c>Id</c>。</para>
    /// </summary>
    public async Task<(bool Success, string Message)> SaveAsync(PromptTemplate entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "提示词编码（Code）不能为空");
        if (string.IsNullOrWhiteSpace(entity.PromptCode))
            return (false, "提示词类型（PromptCode）不能为空");
        if (string.IsNullOrWhiteSpace(entity.Template))
            return (false, "template 不能为空");

        var existing = await GetByCodeAsync(entity.Code);
        var now = DateTime.Now;

        if (existing == null)
        {
            // ★ OrgCode / ScopeKey 在库里是 NOT NULL 无默认值：null ⇒ 1364「必填字段为空」
            entity.OrgCode ??= string.Empty;
            entity.ScopeKey ??= string.Empty;
            entity.IsValid = 1;
            entity.IsDeleted = false;
            entity.CreateTime = now;
            if (string.IsNullOrWhiteSpace(entity.Status)) entity.Status = "active";

            var inserted = await _db.InsertAsync(entity);
            if (!inserted.Success)
                return (false, inserted.Error ?? "保存失败");
            return (true, "保存成功");
        }

        existing.PromptCode = entity.PromptCode;
        existing.PromptName = string.IsNullOrWhiteSpace(entity.PromptName) ? existing.PromptName : entity.PromptName;
        existing.ScopeKey = entity.ScopeKey ?? string.Empty;
        existing.Template = entity.Template;
        existing.Remark = entity.Remark;
        existing.IsValid = 1;
        if (!string.IsNullOrWhiteSpace(entity.Status)) existing.Status = entity.Status;
        existing.UpdateTime = now;

        var updated = await _db.UpdateAsync(existing,
            nameof(PromptTemplate.PromptCode), nameof(PromptTemplate.PromptName),
            nameof(PromptTemplate.ScopeKey), nameof(PromptTemplate.Template),
            nameof(PromptTemplate.Remark), nameof(PromptTemplate.IsValid),
            nameof(PromptTemplate.Status), nameof(PromptTemplate.UpdateTime));
        if (!updated.Success)
            return (false, updated.Error ?? "保存失败");
        return (true, "保存成功");
    }

    /// <summary>删除提示词（逻辑禁用：IsValid = 0）。</summary>
    public async Task<bool> DeleteAsync(string code)
    {
        var entity = await GetByCodeAsync(code);
        if (entity == null) return false;

        entity.IsValid = 0;
        entity.UpdateTime = DateTime.Now;
        var result = await _db.UpdateAsync(entity, nameof(PromptTemplate.IsValid), nameof(PromptTemplate.UpdateTime));
        return result.Success;
    }

    /// <summary>
    /// 激活提示词：目标行 <c>IsValid=1</c>，同类型 + 同作用域的其他行 <c>IsValid=0</c>。
    /// <para>★ cert 表无 <c>IsActive</c>（铁律九：启用唯一字段 = <c>IsValid</c>），
    /// 「一次仅一条生效」按 <c>(PromptCode, ScopeKey)</c> 维度表达。</para>
    /// </summary>
    public async Task<bool> ActivateAsync(string code)
    {
        var target = await GetByCodeAsync(code);
        if (target == null) return false;

        var siblings = await _db.GetListAsync<PromptTemplate>(x =>
            x.PromptCode == target.PromptCode
            && x.ScopeKey == target.ScopeKey
            && x.Code != code
            && x.IsValid == 1);

        if (siblings.Success && siblings.Data != null)
        {
            foreach (var item in siblings.Data)
            {
                item.IsValid = 0;
                item.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(item, nameof(PromptTemplate.IsValid), nameof(PromptTemplate.UpdateTime));
            }
        }

        target.IsValid = 1;
        target.UpdateTime = DateTime.Now;
        var result = await _db.UpdateAsync(target, nameof(PromptTemplate.IsValid), nameof(PromptTemplate.UpdateTime));
        return result.Success;
    }

    /// <summary>
    /// 【工作台】标准下拉选项：<c>Code</c>（GUID，工作台作用域键）+ <c>StandardCode</c> / <c>StandardName</c>（展示）。
    /// <para>⚠️ 返回值用 <c>Code</c>（GUID）：工作台前端的 <c>standardCode</c> 字段是 GUID 口径，
    /// 落库时由本服务换算为短码写入 <c>cert_prompt_template.ScopeKey</c>。</para>
    /// </summary>
    public async Task<List<StandardOption>> GetStandardOptionsAsync()
    {
        var list = await _db.GetListAsync<CertPlatform.Shared.Entities.Cert.ISOStandard>(x => x.IsValid == 1);
        if (!list.Success || list.Data == null) return new List<StandardOption>();

        return list.Data
            .OrderBy(x => x.StandardCode)
            .Select(x => new StandardOption
            {
                Code = x.Code ?? "",
                StandardCode = x.StandardCode,
                StandardName = x.StandardName,
                Display = $"{x.StandardName}（{x.StandardCode}）",
                IsValid = x.IsValid,
                FamilyCode = x.FamilyCode,
                Category = x.Category
            })
            .ToList();
    }

    /// <summary>标准下拉项</summary>
    /// <remarks>
    /// ⚠️ 显式 <c>[JsonPropertyName]</c> 不可省：本项目 MVC JSON 选项是
    /// <c>PropertyNamingPolicy = null</c>（PascalCase），而 DTO 惯例是 camelCase。
    /// 不标注 ⇒ 前端读 <c>data.code</c> 得 undefined ⇒ 「下拉有选项但选中后存空值」且零报错。
    /// </remarks>
    public class StandardOption
    {
        /// <summary>★ 工作台作用域键（cert_iso_standard.Code，GUID）</summary>
        [System.Text.Json.Serialization.JsonPropertyName("code")]
        public string Code { get; set; } = "";
        /// <summary>可读标准号（如 iso9001，不含年份）</summary>
        [System.Text.Json.Serialization.JsonPropertyName("standardCode")]
        public string StandardCode { get; set; } = "";
        /// <summary>标准名称</summary>
        [System.Text.Json.Serialization.JsonPropertyName("standardName")]
        public string StandardName { get; set; } = "";
        /// <summary>下拉展示文本</summary>
        [System.Text.Json.Serialization.JsonPropertyName("display")]
        public string Display { get; set; } = "";
        /// <summary>启用状态（0/1，左树启用/禁用徽章用；查询已过滤 IsValid=1）</summary>
        [System.Text.Json.Serialization.JsonPropertyName("isValid")]
        public int IsValid { get; set; } = 1;
        /// <summary>★ 所属族 GUID（cert_standard_family.Code）；null = 未归族（前端三层树「族」定位用）</summary>
        [System.Text.Json.Serialization.JsonPropertyName("familyCode")]
        public string? FamilyCode { get; set; }
        /// <summary>★ 所属体系（iso_category 字典 DicValue，如 quality）；前端「体系」定位用</summary>
        [System.Text.Json.Serialization.JsonPropertyName("category")]
        public string? Category { get; set; }
    }
}
