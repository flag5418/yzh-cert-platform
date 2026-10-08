
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Wf;

namespace CertPlatform.Admin.Services.Workflow;

/// <summary>
/// Prompt 模板服务（业务层移植）
/// <para>对照旧 Cert.Platform/Services/Admin/Platform/Wf/PromptTemplateService.cs（EF Core 实现），</para>
/// <para>职责不变：列表筛选（类型 / 适用技能）+ 按编码读取 + 取生效模板 + 保存（编码幂等 upsert）+ 逻辑删除 + 激活；</para>
/// <para>ORM 改写为新架构 IDbOrm（旧 EF DbContext 已不在新架构中）。</para>
/// </summary>
public class PromptTemplateService
{
    private readonly IDbOrm _db;
    private readonly ILogger<PromptTemplateService> _logger;

    public PromptTemplateService(IDbOrm db, ILogger<PromptTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 获取提示词列表（可按类型 / 适用技能筛选）
    /// <para>对照旧实现：skillTarget 命中「指定技能」或「无技能限制（null）」的记录</para>
    /// </summary>
    public async Task<List<PromptTemplate>> GetListAsync(string? promptType = null, string? skillTarget = null)
    {
        var result = await _db.GetListAsync<PromptTemplate>(x =>
            x.IsValid == 1
            && (string.IsNullOrEmpty(promptType) || x.PromptType == promptType)
            && (string.IsNullOrEmpty(skillTarget) || x.SkillTarget == skillTarget || x.SkillTarget == null));

        if (!result.Success)
        {
            _logger.LogWarning("Prompt 模板列表查询失败：{Error}", result.Error);
            return new List<PromptTemplate>();
        }

        return result.Data!
            .OrderBy(x => x.PromptType)
            .ThenBy(x => x.PromptCode)
            .ToList();
    }

    /// <summary>根据 prompt_code 获取单条提示词</summary>
    public async Task<PromptTemplate?> GetByCodeAsync(string promptCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode)) return null;
        var result = await _db.GetOneAsync<PromptTemplate>(
            x => x.PromptCode == promptCode && x.IsValid == 1);
        return result.Success ? result.Data : null;
    }

    /// <summary>
    /// 获取指定类型当前生效的提示词（适用技能优先精确匹配，回退到「通用 / 无技能限制」）
    /// </summary>
    public async Task<PromptTemplate?> GetActiveAsync(string promptType, string? skillTarget = null)
    {
        if (string.IsNullOrWhiteSpace(promptType)) return null;

        var result = await _db.GetListAsync<PromptTemplate>(x =>
            x.PromptType == promptType && x.IsActive == true && x.IsValid == 1);

        if (!result.Success || result.Data == null) return null;

        var candidates = result.Data;
        if (!string.IsNullOrWhiteSpace(skillTarget))
        {
            var specific = candidates.FirstOrDefault(x => x.SkillTarget == skillTarget);
            if (specific != null) return specific;
        }

        return candidates.FirstOrDefault(x => x.SkillTarget == null || x.SkillTarget == "all");
    }

    /// <summary>
    /// 创建或更新提示词（按 prompt_code 幂等匹配；更新时覆盖正文并置为生效）
    /// <para>⛔ 2026-10-02 起<b>不再 +1 版本</b>（裁决：提示词不做版本管理），
    /// 原 <c>Version</c> 原样保留 —— <c>DocExtractionRuleService.AI</c> 与
    /// <c>BuildNcPromptSkill</c> 仍按 <c>OrderByDescending(Version)</c> 取行。</para>
    /// </summary>
    public async Task<(bool Success, string Message)> SaveAsync(PromptTemplate entity)
    {
        if (string.IsNullOrWhiteSpace(entity.PromptCode))
            return (false, "prompt_code 不能为空");
        if (string.IsNullOrWhiteSpace(entity.PromptType))
            return (false, "prompt_type 不能为空");
        if (string.IsNullOrWhiteSpace(entity.Template))
            return (false, "template 不能为空");

        var existing = await GetByCodeAsync(entity.PromptCode);

        if (existing == null)
        {
            // 准则 A：新增 — Id 不参与分流（保持实体默认 0）
            entity.Code = Guid.NewGuid().ToString("N");
            entity.Version = 1;
            entity.IsActive = true;
            entity.IsValid = 1;
            entity.CreateTime = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(entity.Status)) entity.Status = "active";

            var inserted = await _db.InsertAsync(entity);
            if (!inserted.Success)
                return (false, inserted.Error ?? "保存失败");
            return (true, "保存成功");
        }

        // 更新：靠 Code 定位（禁止回填 Id 作 WHERE / 分流键）
        if (string.IsNullOrWhiteSpace(existing.Code))
            return (false, "更新失败：缺少业务键 Code");

        entity.Code = existing.Code;
        // ★ 统一 AI 配置（Q3=a）：本工作台不编辑行级模型参数 ⇒ 更新时**原样保留**。
        //   否则请求里没有这几个字段会把它们写成 NULL，NC 链路 BuildNcPromptSkill 读到空值即炸。
        entity.ModelName = existing.ModelName;
        entity.MaxTokens = existing.MaxTokens;
        entity.Temperature = existing.Temperature;
        // ★ 不做版本管理：保留原版本号，仅刷新正文
        entity.Version = existing.Version;
        entity.IsActive = true;
        entity.IsValid = 1;
        entity.CreateTime = existing.CreateTime;
        entity.CreateBy = existing.CreateBy;
        entity.UpdateTime = DateTime.Now;
        entity.DeleteBy = null;
        entity.DeleteTime = null;
        if (string.IsNullOrWhiteSpace(entity.Status)) entity.Status = existing.Status;

        var updated = await _db.UpdateAsync(entity);
        if (!updated.Success)
            return (false, updated.Error ?? "保存失败");
        return (true, "保存成功");
    }

    /// <summary>删除提示词（逻辑禁用：IsValid = 0）</summary>
    public async Task<bool> DeleteAsync(string promptCode)
    {
        var entity = await GetByCodeAsync(promptCode);
        if (entity == null) return false;

        entity.IsValid = 0;
        entity.UpdateTime = DateTime.Now;
        var result = await _db.UpdateAsync(entity);
        return result.Success;
    }

    /// <summary>
    /// 激活提示词（同类型的其他提示词置为不生效，对齐旧实现的一次仅一条生效语义）
    /// </summary>
    public async Task<bool> ActivateAsync(string promptCode)
    {
        var target = await GetByCodeAsync(promptCode);
        if (target == null) return false;

        var siblings = await _db.GetListAsync<PromptTemplate>(x =>
            x.PromptType == target.PromptType && x.PromptCode != promptCode && x.IsValid == 1);

        if (siblings.Success && siblings.Data != null)
        {
            foreach (var item in siblings.Data)
            {
                item.IsActive = false;
                item.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(item);
            }
        }

        target.IsActive = true;
        target.UpdateTime = DateTime.Now;
        var result = await _db.UpdateAsync(target);
        return result.Success;
    }

    /// <summary>
    /// 【工作台】标准下拉选项：<c>Code</c>（GUID，存库值）+ <c>StandardCode</c> / <c>StandardName</c>（展示）。
    /// <para>⚠️ 返回值用 <c>Code</c>（GUID），因为 <c>wf_prompt_template.StandardCode</c> 与
    /// <c>cert_enterprise_stage.StandardCode</c> 同口径都存 GUID —— 存可读编码会造成「页面看 A、匹配 B」且不报错。</para>
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
                IsValid = x.IsValid
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
        /// <summary>★ 存库值（cert_iso_standard.Code，GUID）</summary>
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
    }
}
