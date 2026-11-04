using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert;

// ══════════════════════════════════════════════════════════════════════
// 请求 / 响应契约
// ══════════════════════════════════════════════════════════════════════

/// <summary>证据列表查询请求</summary>
public class CertEvidenceListRequest
{
    public string? Keyword { get; set; }
    public string? CheckItemCode { get; set; }
    public string? CapturedIn { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>证据列表响应行</summary>
public class CertEvidenceRow
{
    public string Code { get; set; } = string.Empty;
    public string CheckItemCode { get; set; } = string.Empty;
    public string? ClauseCode { get; set; }
    public string CapturedIn { get; set; } = "manual";
    public string? CapturedTaskCode { get; set; }
    public string EvidenceKind { get; set; } = "manual";
    public string? SourceCode { get; set; }
    public string? SourceName { get; set; }
    public string? Anchor { get; set; }
    public string? Snippet { get; set; }
    public string? LlmSummary { get; set; }
    public string? TranscriptText { get; set; }
    public string? ContentMD5 { get; set; }
    public string? PrevContentMD5 { get; set; }
    public string Status { get; set; } = "collected";
    public DateTime? CapturedAt { get; set; }
    public int Sort { get; set; }
    public string? CreateBy { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>证据列表分页响应</summary>
public class CertEvidenceListResponse
{
    public int Total { get; set; }
    public List<CertEvidenceRow> Rows { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>新增证据请求</summary>
public class CertEvidenceAddRequest
{
    /// <summary>来源名称（必填）</summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>关联检查项 Code（可空 = 暂不挂接）</summary>
    public string? CheckItemCode { get; set; }

    /// <summary>来源编码（FileCode 等）</summary>
    public string? SourceCode { get; set; }

    /// <summary>证据类型：doc_slice / pq_result / lead / onsite_media / interview / sample / manual</summary>
    public string EvidenceKind { get; set; } = "manual";

    /// <summary>谁采集的：prep / onsite / manual / doc / pq</summary>
    public string CapturedIn { get; set; } = "manual";

    /// <summary>原文摘录</summary>
    public string? Snippet { get; set; }

    /// <summary>LLM 可消费的标准化文本</summary>
    public string? LlmSummary { get; set; }

    /// <summary>转录文本（与 Snippet 二选一）</summary>
    public string? TranscriptText { get; set; }

    /// <summary>锚点定位</summary>
    public string? Anchor { get; set; }

    /// <summary>附件 JSON</summary>
    public string? AttachmentsJson { get; set; }
}

/// <summary>新增证据响应</summary>
public class CertEvidenceAddResponse
{
    public string EvidenceCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>更新证据请求（部分更新）</summary>
public class CertEvidenceUpdateRequest
{
    public string Code { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    /// <summary>证据类型：doc_slice / pq_result / lead / onsite_media / interview / sample / manual</summary>
    public string? EvidenceKind { get; set; }
    public string? LlmSummary { get; set; }
    public string? Snippet { get; set; }
    public string? SourceCode { get; set; }
    public string? Anchor { get; set; }
    public string Status { get; set; } = "";
    public string? DroppedReason { get; set; }
}

/// <summary>删除证据请求</summary>
public class CertEvidenceDeleteRequest
{
    public string Code { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════════════════
// 服务
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 证据管理服务（V2 — cert_evidence CRUD + MD5 变更检测 + 列表）
/// <para>职责：</para>
/// <list type="bullet">
///   <item>列表查询：分页 + 关键字模糊 + 检查项过滤</item>
///   <item>新增证据：MD5 自动计算（基于 LlmSummary / Snippet）</item>
///   <item>内容更新：修改 LlmSummary / Snippet 后自动更新 ContentMD5</item>
/// </list>
/// </summary>
public class CertEvidenceService
{
    private readonly IDbOrm _db;
    private readonly ILogger<CertEvidenceService> _logger;

    public CertEvidenceService(IDbOrm db, ILogger<CertEvidenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>分页查询证据列表</summary>
    public async Task<Result<CertEvidenceListResponse>> GetListAsync(
        string userCode, CertEvidenceListRequest req)
    {
        var query = _db.Client.Queryable<CertEvidence>()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(req.Keyword))
            query = query.Where(e =>
                e.SourceName.Contains(req.Keyword)
                || e.Code.Contains(req.Keyword)
                || e.LlmSummary.Contains(req.Keyword));

        if (!string.IsNullOrWhiteSpace(req.CheckItemCode))
            query = query.Where(e => e.CheckItemCode == req.CheckItemCode);

        if (!string.IsNullOrWhiteSpace(req.CapturedIn))
            query = query.Where(e => e.CapturedIn == req.CapturedIn);

        var total = query.Count();

        var items = query
            .OrderBy(e => e.Sort)
            .OrderBy(e => e.CreateTime, SqlSugar.OrderByType.Desc)
            .Skip((req.PageIndex - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToList();

        var rows = items.Select(e => new CertEvidenceRow
        {
            Code = e.Code,
            CheckItemCode = e.CheckItemCode,
            ClauseCode = e.ClauseCode,
            CapturedIn = e.CapturedIn,
            CapturedTaskCode = e.CapturedTaskCode,
            EvidenceKind = e.EvidenceKind,
            SourceCode = e.SourceCode,
            SourceName = e.SourceName,
            Anchor = e.Anchor,
            Snippet = e.Snippet,
            LlmSummary = e.LlmSummary,
            TranscriptText = e.TranscriptText,
            ContentMD5 = e.ContentMD5,
            PrevContentMD5 = e.PrevContentMD5,
            Status = e.Status,
            CapturedAt = e.CapturedAt,
            Sort = e.Sort,
            CreateBy = e.CreateBy,
            CreateTime = e.CreateTime
        }).ToList();

        return Result<CertEvidenceListResponse>.Ok(new CertEvidenceListResponse
        {
            Total = total,
            Rows = rows,
            Message = $"查询完成，共 {total} 条证据"
        });
    }

    /// <summary>新增证据（自动计算 ContentMD5）</summary>
    public async Task<Result<CertEvidenceAddResponse>> AddAsync(
        string userCode, CertEvidenceAddRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SourceName))
            return Result<CertEvidenceAddResponse>.Fail("来源名称不能为空");

        // 规范化 LlmSummary：优先用传入值，其次用 Snippet，其次用 TranscriptText
        var llmSummary = req.LlmSummary ?? req.Snippet ?? req.TranscriptText;

        var evidence = new CertEvidence
        {
            Code = Guid.NewGuid().ToString("N"),
            CheckItemCode = req.CheckItemCode ?? "",
            SourceName = req.SourceName,
            SourceCode = req.SourceCode,
            EvidenceKind = req.EvidenceKind,
            CapturedIn = req.CapturedIn,
            Snippet = req.Snippet ?? req.TranscriptText,
            LlmSummary = llmSummary,
            Anchor = req.Anchor,
            AttachmentsJson = req.AttachmentsJson,
            ContentMD5 = ComputeMd5(llmSummary ?? req.Snippet ?? ""),
            Status = "collected",
            CapturedAt = DateTime.Now,
            Sort = 0,
            CreateBy = userCode,
            CreateTime = DateTime.Now
        };

        await _db.InsertAsync(evidence);

        // ★ 2026-11-03：证据新增后，已分析过的检查项标记「建议重新分析」
        await MarkRerunSuggestedAsync(userCode, evidence.CheckItemCode);

        return Result<CertEvidenceAddResponse>.Ok(new CertEvidenceAddResponse
        {
            EvidenceCode = evidence.Code,
            Message = "证据已添加"
        });
    }

    /// <summary>更新证据（部分内容，自动同步 ContentMD5）</summary>
    public async Task<Result> UpdateAsync(
        string userCode, CertEvidenceUpdateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return Result.Fail("证据 Code 不能为空");

        var existing = await _db.GetOneAsync<CertEvidence>(e =>
            e.Code == req.Code && !e.IsDeleted);
        if (existing.Data == null)
            return Result.Fail("证据不存在");

        var e = existing.Data;

        if (req.SourceName != null) e.SourceName = req.SourceName;
        if (req.EvidenceKind != null) e.EvidenceKind = req.EvidenceKind;
        if (req.Snippet != null) e.Snippet = req.Snippet;
        if (req.SourceCode != null) e.SourceCode = req.SourceCode;
        if (req.Anchor != null) e.Anchor = req.Anchor;

        if (req.LlmSummary != null)
        {
            // 检测内容是否实际变更：保存 PrevContentMD5
            e.PrevContentMD5 = e.ContentMD5;
            e.LlmSummary = req.LlmSummary;
            e.ContentMD5 = ComputeMd5(req.LlmSummary);
        }
        else if (req.Snippet != null)
        {
            // 只改原文摘录时同步 LLM 消费文本 —— 分析实际读的是 LlmSummary，
            // 不同步会出现「页面上改了证据、分析还用旧内容」的静默不一致。
            e.PrevContentMD5 = e.ContentMD5;
            e.LlmSummary = req.Snippet;
            e.ContentMD5 = ComputeMd5(req.Snippet);
        }

        if (!string.IsNullOrEmpty(req.Status))
            e.Status = req.Status;

        if (req.DroppedReason != null)
            e.DroppedReason = req.DroppedReason;

        e.UpdateBy = userCode;
        e.UpdateTime = DateTime.Now;

        await _db.UpdateAsync(e);

        // ★ 2026-11-03：证据内容变化后，已分析过的检查项标记「建议重新分析」
        await MarkRerunSuggestedAsync(userCode, e.CheckItemCode);

        return Result.Ok();
    }

    /// <summary>删除证据（软删，保留审计留痕）</summary>
    public async Task<Result> DeleteAsync(string userCode, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Fail("证据 Code 不能为空");

        var existing = await _db.GetOneAsync<CertEvidence>(e =>
            e.Code == code && !e.IsDeleted);
        if (existing.Data == null)
            return Result.Fail("证据不存在");

        var e = existing.Data;
        e.IsDeleted = true;
        e.DeleteBy = userCode;
        e.DeleteTime = DateTime.Now;
        e.UpdateBy = userCode;
        e.UpdateTime = DateTime.Now;

        await _db.UpdateAsync(e);

        // ★ 2026-11-03：证据删除后，已分析过的检查项标记「建议重新分析」
        await MarkRerunSuggestedAsync(userCode, e.CheckItemCode);

        return Result.Ok();
    }

    /// <summary>查询证据详情（含全文内容，供预览页使用）</summary>
    public async Task<Result<CertEvidenceRow>> GetDetailAsync(string userCode, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<CertEvidenceRow>.Fail("证据 Code 不能为空");

        var result = await _db.GetOneAsync<CertEvidence>(e =>
            e.Code == code && !e.IsDeleted);
        if (result.Data == null)
            return Result<CertEvidenceRow>.Fail("证据不存在");

        var e = result.Data;
        return Result<CertEvidenceRow>.Ok(new CertEvidenceRow
        {
            Code = e.Code,
            CheckItemCode = e.CheckItemCode,
            ClauseCode = e.ClauseCode,
            CapturedIn = e.CapturedIn,
            CapturedTaskCode = e.CapturedTaskCode,
            EvidenceKind = e.EvidenceKind,
            SourceCode = e.SourceCode,
            SourceName = e.SourceName,
            Anchor = e.Anchor,
            Snippet = e.Snippet,
            LlmSummary = e.LlmSummary,
            TranscriptText = e.TranscriptText,
            ContentMD5 = e.ContentMD5,
            PrevContentMD5 = e.PrevContentMD5,
            Status = e.Status,
            CapturedAt = e.CapturedAt,
            Sort = e.Sort,
            CreateBy = e.CreateBy,
            CreateTime = e.CreateTime
        });
    }

    /// <summary>
    /// 证据变化后，把该检查项「已完成自动分析」的任务项标记为「建议重新分析」。
    /// <para>★ 用户 2026-11-03：认证准备已提交新证据时，应提示审核员可以重新自动分析。</para>
    /// <para>只影响已分析过的任务项；从未分析过的保持 none。</para>
    /// </summary>
    private async Task MarkRerunSuggestedAsync(string userCode, string? checkItemCode)
    {
        if (string.IsNullOrWhiteSpace(checkItemCode)) return;

        var result = await _db.GetListAsync<CertTaskItem>(i =>
            i.CheckItemCode == checkItemCode && !i.IsDeleted);
        var items = result.Data ?? new List<CertTaskItem>();
        var analyzed = items.Where(i =>
            string.Equals(i.AnalysisState, "done", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(i.AnalysisState) && i.AnalyzeCount > 0))
            .Where(i => !string.Equals(i.RerunState, "suggested", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (analyzed.Count == 0) return;

        var now = DateTime.Now;
        foreach (var item in analyzed)
        {
            item.RerunState = "suggested";
            item.UpdateBy = userCode;
            item.UpdateTime = now;
            // R-A：列级写回
            await _db.UpdateAsync(item,
                nameof(CertTaskItem.RerunState),
                nameof(CertTaskItem.UpdateBy),
                nameof(CertTaskItem.UpdateTime));
        }
    }

    /// <summary>计算 MD5（工具方法）</summary>
    private static string ComputeMd5(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
