using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Entities.Cert;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Cert;

/// <summary>
/// 提示词模板通用服务 —— 统一管理全部提示词的 CRUD + 作用域解析。
///
/// <para><b>设计理念</b>：不同业务模块（条款属性 / 文档提取 / 入场检查 / 报告结论 ...）
/// 经由此服务存取提示词，只传 <c>promptCode</c> + <c>scopeKey</c> 即可，
/// ⛔ 每个业务不再自己拼 SQL 找提示词。</para>
///
/// <para><b>作用域解析优先级</c>（具体 → 通用）：
/// <list type="number">
/// <item><c>OrgCode + ScopeKey</c></item>
/// <item><c>OrgCode</c>（机构默认）</item>
/// <item><c>ScopeKey</c>（平台级 × 细分维度）</item>
/// <item>平台默认（OrgCode = 空 &amp; ScopeKey = 空）</item>
/// </list></para>
///
/// <para><b>占位符注册</c>：各业务通过 <see cref="RegisterPlaceholder"/> 声明占位符信息，
/// 供前端渲染插入按钮。</para>
/// </summary>
public sealed class PromptTemplateService
{
    private readonly IDbOrm _db;

    /// <summary>占位符注册表（promptCode → 占位符列表）</summary>
    private static readonly Dictionary<string, List<PlaceholderDescriptor>> PlaceholderRegistry = new(StringComparer.Ordinal);

    public PromptTemplateService(IDbOrm db)
    {
        _db = db;
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、占位符注册（各业务模块在启动时调用）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>注册一个提示词的占位符列表（前端依此渲染插入按钮）</summary>
    public static void RegisterPlaceholder(string promptCode, string token, string description)
    {
        if (!PlaceholderRegistry.TryGetValue(promptCode, out var list))
        {
            list = new List<PlaceholderDescriptor>();
            PlaceholderRegistry[promptCode] = list;
        }
        list.Add(new PlaceholderDescriptor(token, description));
    }

    /// <summary>获取某提示词的所有已注册占位符</summary>
    public static IReadOnlyList<PlaceholderDescriptor> GetPlaceholders(string promptCode)
    {
        return PlaceholderRegistry.TryGetValue(promptCode, out var list)
            ? list.AsReadOnly()
            : Array.Empty<PlaceholderDescriptor>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、读取
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 按 promptCode + 作用域解析获取最匹配的提示词。
    /// <para>⚠️ 不返回 null：平台默认行缺失时回落到 <see cref="FallbackTemplates"/></para>
    /// </summary>
    /// <param name="promptCode">提示词代码（如 clause_attr_gen）</param>
    /// <param name="orgCode">机构 Code（可空）</param>
    /// <param name="scopeKey">细分维度 —— 标准 Code / 阶段 Code（可空）</param>
    public async Task<PromptTemplate> GetAsync(
        string promptCode, string? orgCode = null, string? scopeKey = null)
    {
        // 作用域匹配优先级（最具体的先匹配）
        PromptTemplate? match;

        if (!string.IsNullOrWhiteSpace(orgCode) && !string.IsNullOrWhiteSpace(scopeKey))
        {
            match = await _db.Client.Queryable<PromptTemplate>()
                .Where(p => p.PromptCode == promptCode && p.IsValid == 1 && !p.IsDeleted
                            && p.OrgCode == orgCode && p.ScopeKey == scopeKey)
                .FirstAsync();
            if (match != null) return match;
        }

        if (!string.IsNullOrWhiteSpace(orgCode))
        {
            match = await _db.Client.Queryable<PromptTemplate>()
                .Where(p => p.PromptCode == promptCode && p.IsValid == 1 && !p.IsDeleted
                            && p.OrgCode == orgCode && string.IsNullOrEmpty(p.ScopeKey))
                .FirstAsync();
            if (match != null) return match;
        }

        if (!string.IsNullOrWhiteSpace(scopeKey))
        {
            match = await _db.Client.Queryable<PromptTemplate>()
                .Where(p => p.PromptCode == promptCode && p.IsValid == 1 && !p.IsDeleted
                            && string.IsNullOrEmpty(p.OrgCode) && p.ScopeKey == scopeKey)
                .FirstAsync();
            if (match != null) return match;
        }

        // 平台默认
        match = await _db.Client.Queryable<PromptTemplate>()
            .Where(p => p.PromptCode == promptCode && p.IsValid == 1 && !p.IsDeleted
                        && string.IsNullOrEmpty(p.OrgCode) && string.IsNullOrEmpty(p.ScopeKey))
            .FirstAsync();
        if (match != null) return match;

        // 回落兜底
        return CreateFallback(promptCode);
    }

    /// <summary>按 promptCode 取所有作用域变体（给管理页面用）</summary>
    public async Task<List<PromptTemplate>> GetByCodeAsync(string promptCode, CancellationToken ct = default)
    {
        return await _db.Client.Queryable<PromptTemplate>()
            .Where(p => p.PromptCode == promptCode && p.IsValid == 1 && !p.IsDeleted)
            .OrderBy(p => p.Sort)
            .ToListAsync(ct);
    }

    /// <summary>取某业务模块下所有提示词（按 promptCode 前缀匹配）</summary>
    public async Task<List<PromptTemplate>> GetByTypeAsync(string type, CancellationToken ct = default)
    {
        return await _db.Client.Queryable<PromptTemplate>()
            .Where(p => p.PromptCode.StartsWith(type) && p.IsValid == 1 && !p.IsDeleted)
            .OrderBy(p => p.Sort)
            .ToListAsync(ct);
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、保存
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 保存提示词（幂等 upsert）：按 (PromptCode, OrgCode, ScopeKey) 三元组定位。
    /// <para>已有 = 更新正文；未有 = 新建。</para>
    /// </summary>
    public async Task<(bool Ok, string Message)> SaveAsync(
        string promptCode, string template, string? orgCode = null, string? scopeKey = null,
        string? promptName = null, string? remark = null, string? userCode = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(promptCode))
            return (false, "promptCode 不能为空");
        if (string.IsNullOrWhiteSpace(template))
            return (false, "模板正文不能为空");

        var existing = await FindExactAsync(promptCode, orgCode, scopeKey, ct);

        if (existing == null)
        {
            var entity = new PromptTemplate
            {
                Code = Guid.NewGuid().ToString("N"),
                PromptCode = promptCode,
                PromptName = promptName ?? promptCode,
                OrgCode = orgCode,
                ScopeKey = scopeKey,
                Template = template,
                Remark = remark,
                Sort = 0,
                IsValid = 1,
                Status = "active",
                IsDeleted = false,
                CreateBy = userCode ?? string.Empty,
                CreateTime = DateTime.Now,
            };
            var ins = await _db.InsertAsync(entity);
            return ins.Success ? (true, "保存成功") : (false, $"保存失败：{ins.Error}");
        }

        existing.Template = template;
        existing.PromptName = promptName ?? existing.PromptName;
        existing.Remark = remark ?? existing.Remark;
        existing.UpdateBy = userCode;
        existing.UpdateTime = DateTime.Now;

        var upd = await _db.UpdateAsync(
            existing,
            nameof(PromptTemplate.Template),
            nameof(PromptTemplate.PromptName),
            nameof(PromptTemplate.Remark),
            nameof(PromptTemplate.UpdateBy),
            nameof(PromptTemplate.UpdateTime));

        return upd.Success ? (true, "保存成功") : (false, $"保存失败：{upd.Error}");
    }

    /// <summary>逻辑删除（IsValid = 0 + IsDeleted = true）</summary>
    public async Task<(bool Ok, string Message)> DeleteAsync(
        string promptCode, string? orgCode = null, string? scopeKey = null, string? userCode = null, CancellationToken ct = default)
    {
        var existing = await FindExactAsync(promptCode, orgCode, scopeKey, ct);
        if (existing == null)
            return (false, "记录不存在");

        existing.IsValid = 0;
        existing.IsDeleted = true;
        existing.DeleteBy = userCode;
        existing.DeleteTime = DateTime.Now;
        existing.UpdateBy = userCode;
        existing.UpdateTime = DateTime.Now;

        var result = await _db.UpdateAsync(
            existing,
            nameof(PromptTemplate.IsValid),
            nameof(PromptTemplate.IsDeleted),
            nameof(PromptTemplate.DeleteBy),
            nameof(PromptTemplate.DeleteTime),
            nameof(PromptTemplate.UpdateBy),
            nameof(PromptTemplate.UpdateTime));

        return result.Success ? (true, "删除成功") : (false, $"删除失败：{result.Error}");
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、工具
    // ════════════════════════════════════════════════════════════════════

    private async Task<PromptTemplate?> FindExactAsync(
        string promptCode, string? orgCode, string? scopeKey, CancellationToken ct)
    {
        return await _db.Client.Queryable<PromptTemplate>()
            .Where(p => p.PromptCode == promptCode
                        && p.OrgCode == orgCode
                        && p.ScopeKey == scopeKey
                        && p.IsValid == 1 && !p.IsDeleted)
            .FirstAsync(ct);
    }

    private static PromptTemplate CreateFallback(string promptCode)
    {
        return FallbackTemplates.TryGetValue(promptCode, out var template)
            ? new PromptTemplate
            {
                Code = $"fallback_{promptCode}",
                PromptCode = promptCode,
                PromptName = promptCode,
                Template = template,
                IsValid = 1,
                Status = "fallback",
            }
            : new PromptTemplate
            {
                Code = $"fallback_{promptCode}",
                PromptCode = promptCode,
                PromptName = promptCode,
                Template = string.Empty,
                IsValid = 1,
                Status = "fallback",
            };
    }

    /// <summary>兜底提示词（库中没有任何行时生效；各业务可在此扩展）</summary>
    private static readonly Dictionary<string, string> FallbackTemplates = new(StringComparer.Ordinal);

    /// <summary>注册兜底提示词（启动时调用）</summary>
    public static void RegisterFallback(string promptCode, string template)
    {
        FallbackTemplates[promptCode] = template;
    }
}

/// <summary>占位符描述符</summary>
public sealed record PlaceholderDescriptor(string Token, string Description);
