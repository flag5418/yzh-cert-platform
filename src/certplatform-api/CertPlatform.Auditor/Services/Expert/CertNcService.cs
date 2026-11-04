using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Services.Workflow;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert;

// ══════════════════════════════════════════════════════════════════════
// 请求 / 响应契约
// ══════════════════════════════════════════════════════════════════════

/// <summary>NC 签发请求</summary>
public class CertNcIssueRequest
{
    /// <summary>检查项 Code</summary>
    public string CheckItemCode { get; set; } = string.Empty;
    /// <summary>不符合事实描述</summary>
    public string FactDesc { get; set; } = string.Empty;
    /// <summary>违反条款原文引用</summary>
    public string? RequirementRef { get; set; }
    /// <summary>严重程度：major / minor</summary>
    public string Severity { get; set; } = "minor";
    /// <summary>整改要求</summary>
    public string? CorrectiveReq { get; set; }
    /// <summary>整改截止日期</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>来源任务 Code</summary>
    public string? TaskCode { get; set; }
    /// <summary>审核员自定义 NC 编号（空 → LLM 建议 / 系统自动生成）</summary>
    public string? NcNo { get; set; }
}

/// <summary>NC 结果</summary>
public class CertNcResult
{
    public string NcCode { get; set; } = string.Empty;
    public string NcNo { get; set; } = string.Empty;
    public string Status { get; set; } = "pending_review";
    public string Message { get; set; } = string.Empty;
}

/// <summary>NC 状态流转请求</summary>
public class CertNcTransitionRequest
{
    public string NcCode { get; set; } = string.Empty;
    public string Action { get; set; } = ""; // issue / rectifying / verifying / closed / escalated
    public string? Remark { get; set; }
    public string? CloseEvidenceJson { get; set; }
}

/// <summary>AI NC 建议请求</summary>
public class CertNcAiSuggestRequest
{
    public string CheckItemCode { get; set; } = string.Empty;
    public string TaskItemCode { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════════════════
// NC 服务
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// NC 不符合项服务（V2 — 生命周期 + AI 建议）
/// <para>职责：</para>
/// <list type="bullet">
///   <item>NC 签发：手动创建 + 流转状态机</item>
///   <item>AI 建议：调用 nc_suggest prompt 生成 NC 描述草案</item>
/// </list>
/// </summary>
public class CertNcService
{
    private readonly IDbOrm _db;
    private readonly LlmInvokeService _llm;
    private readonly IPromptWorkbench _promptWb;
    private readonly ILogger<CertNcService> _logger;

    private static readonly HashSet<string> ValidStatuses = new()
    {
        "pending_review", "issued", "rectifying", "verifying", "closed", "escalated"
    };

    // 允许的状态流转
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new()
    {
        ["pending_review"] = new() { "issued", "closed" },
        ["issued"] = new() { "rectifying", "escalated" },
        ["rectifying"] = new() { "verifying", "escalated" },
        ["verifying"] = new() { "closed", "rectifying" },
        ["escalated"] = new() { "rectifying", "closed" }
    };

    public CertNcService(
        IDbOrm db,
        LlmInvokeService llm,
        IPromptWorkbench promptWb,
        ILogger<CertNcService> logger)
    {
        _db = db;
        _llm = llm;
        _promptWb = promptWb;
        _logger = logger;
    }

    // ================================================================
    // 一、NC 签发
    // ================================================================

    /// <summary>
    /// 签发不符合项（审核员确认后落库 cert_nc）
    /// <para>状态起点 = pending_review，审核员确认后应该调用 Transition(issue) 流转到 issued</para>
    /// </summary>
    public async Task<Result<CertNcResult>> IssueAsync(string userCode, CertNcIssueRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CheckItemCode))
            return Result<CertNcResult>.Fail("检查项 Code 不能为空");

        if (string.IsNullOrWhiteSpace(req.FactDesc))
            return Result<CertNcResult>.Fail("不符合事实描述不能为空");

        var ncCode = Guid.NewGuid().ToString("N");
        var ncNo = req.NcNo ?? await GenerateNcNoAsync();

        var nc = new CertNc
        {
            Code = ncCode,
            NcNo = ncNo,
            CheckItemCode = req.CheckItemCode,
            FactDesc = req.FactDesc,
            RequirementRef = req.RequirementRef,
            Severity = req.Severity,
            CorrectiveReq = req.CorrectiveReq,
            DueDate = req.DueDate,
            Status = "pending_review",
            Origin = "manual",
            CreatedInTaskCode = req.TaskCode,
            Sort = 0,
            CreateBy = userCode,
            CreateTime = DateTime.Now
        };

        await _db.InsertAsync(nc);

        return Result<CertNcResult>.Ok(new CertNcResult
        {
            NcCode = ncCode,
            NcNo = ncNo,
            Status = "pending_review",
            Message = "NC 已签发（待审核）"
        });
    }

    // ================================================================
    // 二、状态流转
    // ================================================================

    /// <summary>
    /// 驱动 NC 状态流转（含审计字段填充）
    /// </summary>
    public async Task<Result<CertNcResult>> TransitionAsync(
        string userCode, CertNcTransitionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NcCode))
            return Result<CertNcResult>.Fail("NC Code 不能为空");

        var nc = await _db.GetOneAsync<CertNc>(n => n.Code == req.NcCode && !n.IsDeleted);
        if (nc.Data == null)
            return Result<CertNcResult>.Fail("NC 不存在");

        var currentStatus = nc.Data.Status;
        var action = req.Action.ToLowerInvariant();

        // 校验流转合法性
        if (!AllowedTransitions.TryGetValue(currentStatus, out var allowed) || !allowed.Contains(action))
        {
            return Result<CertNcResult>.Fail($"状态 {currentStatus} 不允许流转到 {action}");
        }

        var newStatus = action switch
        {
            "issued" or "rectifying" or "verifying" or "closed" or "escalated" => action,
            _ => null
        };

        if (newStatus == null || !ValidStatuses.Contains(newStatus))
            return Result<CertNcResult>.Fail($"未知状态：{action}");

        // 执行流转
        nc.Data.Status = newStatus;

        var msg = $"状态 {currentStatus} → {newStatus}";

        switch (action)
        {
            case "issued":
                nc.Data.IssuedBy = userCode;
                nc.Data.IssuedAt = DateTime.Now;
                nc.Data.Origin = "manual";
                msg = "NC 已正式发布";
                break;
            case "closed":
                nc.Data.ClosedBy = userCode;
                nc.Data.ClosedAt = DateTime.Now;
                nc.Data.CloseEvidenceJson = req.CloseEvidenceJson;
                msg = "NC 已关闭";
                break;
            case "rectifying":
                msg = "NC 进入整改中";
                break;
            case "verifying":
                msg = "NC 进入验证中";
                break;
            case "escalated":
                msg = "NC 已升级";
                break;
        }

        if (!string.IsNullOrEmpty(req.Remark))
            nc.Data.Remark = req.Remark;

        nc.Data.UpdateBy = userCode;
        nc.Data.UpdateTime = DateTime.Now;

        await _db.UpdateAsync(nc.Data);

        return Result<CertNcResult>.Ok(new CertNcResult
        {
            NcCode = nc.Data.Code,
            NcNo = nc.Data.NcNo,
            Status = newStatus,
            Message = msg
        });
    }

    // ================================================================
    // 三、AI 建议（调用 nc_suggest prompt）
    // ================================================================

    /// <summary>
    /// AI 生成 NC 描述建议（不调 LLM 时返回空建议，失败不阻断主流程）
    /// </summary>
    public async Task<Result<string>> SuggestNcDescriptionAsync(
        string userCode, CertNcAiSuggestRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TaskItemCode))
            return Result<string>.Fail("任务项 Code 不能为空");

        try
        {
            // 取任务项
            var itemResult = await _db.GetOneAsync<CertTaskItem>(i =>
                i.Code == req.TaskItemCode && !i.IsDeleted);
            if (itemResult.Data == null)
                return Result<string>.Fail("任务项不存在");

            var item = itemResult.Data;

            // 取已有结论
            var conclusionResult = await _db.GetOneAsync<CertCheckResult>(r =>
                r.CheckItemCode == item.CheckItemCode && !r.IsDeleted);

            // 取检查项明细
            var detail = await _db.GetOneAsync<CertAuditPlanDetail>(d =>
                d.Code == item.CheckItemCode && !d.IsDeleted);
            if (detail.Data == null)
                return Result<string>.Fail("检查项明细不存在");

            // 解析 prompt
            var promptView = await _promptWb.ResolveActiveAsync(PromptTypes.NcSuggest, null);
            if (promptView?.Template == null)
                return Result<string>.Fail("未找到 NC 建议提示词模板（nc_suggest）");

            // ★ 2026-11-03：优先用审核员判定；未判定时用系统建议，并明确标注「未经审核员确认」
            var finalConclusion = conclusionResult.Data?.Conclusion;
            var aiConclusion = conclusionResult.Data?.AiConclusion;
            var conclusionText = finalConclusion ?? aiConclusion ?? "未分析";
            var conclusionSource = finalConclusion != null ? "审核员判定" : (aiConclusion != null ? "系统建议（未经审核员确认）" : "—");

            var prompt = $"## 检查项\n" +
                $"条款：{(detail.Data.Clauses ?? "-")}\n" +
                $"条款序号：{(detail.Data.ClauseNumbers ?? "-")}\n" +
                $"涉及部门：{(detail.Data.DeptNames ?? "-")}\n\n" +
                $"## 已有结论\n" +
                $"结论：{conclusionText}（来源：{conclusionSource}）\n" +
                $"判定依据：{conclusionResult.Data?.Reason ?? ""}\n\n" +
                $"请生成 JSON: {{\"fact_desc\":\"不符合事实描述\",\"requirement_ref\":\"违反条款原文\",\"severity\":\"minor或major\",\"corrective_req\":\"整改建议\",\"nc_no_suggestion\":\"建议 NC 编号\"}}";

            // 调 LLM
            var aiSettings = await GetAiSettingsAsync();
            var response = await _llm.CompleteAsync(new LlmInvokeRequest
            {
                BaseUrl = aiSettings.BaseUrl,
                ApiKey = aiSettings.ApiKey,
                Model = aiSettings.Model,
                Temperature = aiSettings.Temperature,
                MaxTokens = aiSettings.MaxTokens,
                SystemPrompt = "你是审核专家，根据检查项和现有结论，生成 NC 不符合项描述建议。",
                Prompt = prompt,
                DocumentContent = "",
                ForceJson = true
            });

            if (!response.Success || response.Json == null)
                return Result<string>.Fail($"LLM 失败：{response.Message}");

            // 提取 fact_desc 作为建议
            try
            {
                var root = response.Json.RootElement;
                var factDesc = root.TryGetProperty("fact_desc", out var fd) ? fd.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(factDesc))
                    return Result<string>.Fail("LLM 未返回有效描述");

                return Result<string>.Ok(factDesc);
            }
            catch (Exception ex)
            {
                return Result<string>.Fail($"解析 LLM 结果失败：{ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NC AI 建议失败 ItemCode={ItemCode}", req.TaskItemCode);
            return Result<string>.Fail($"NC AI 建议失败：{ex.Message}");
        }
    }

    // ================================================================
    // 四、辅助
    // ================================================================

    private async Task<string> GenerateNcNoAsync()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var todayPrefix = $"NC-{today}-";

        // 查今天的 NC 最大值
        var countResult = await _db.GetListAsync<CertNc>(n =>
            n.NcNo != null && n.NcNo.StartsWith(todayPrefix) && !n.IsDeleted);
        var maxNo = (countResult.Data?.Count ?? 0) + 1;

        return $"{todayPrefix}{maxNo:D3}";
    }

    /// <summary>从 cert_sys_config 读取 AI 设置</summary>
    private async Task<(string ApiKey, string BaseUrl, string Model, int MaxTokens, float Temperature)> GetAiSettingsAsync()
    {
        const float defaultTemperature = 0.7f;
        const int defaultMaxTokens = 4096;
        const string defaultModel = "qwen-turbo";
        const string defaultBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";

        try
        {
            var rows = await _db.Client.Queryable<YZH.Core.Api.Models.System.SysConfig>()
                .Where(x => x.Category == "ai_model" && !x.IsDeleted)
                .Select(x => new { x.ConfigKey, x.ConfigValue })
                .ToListAsync();

            string apiKey = "", baseUrl = defaultBaseUrl, model = defaultModel;
            int maxTokens = defaultMaxTokens;
            float temperature = defaultTemperature;

            foreach (var row in rows)
            {
                switch (row.ConfigKey)
                {
                    case "ai_api_key": apiKey = row.ConfigValue ?? ""; break;
                    case "ai_base_url": baseUrl = row.ConfigValue ?? baseUrl; break;
                    case "ai_model_name": model = row.ConfigValue ?? model; break;
                    case "ai_max_tokens":
                        if (int.TryParse(row.ConfigValue, out var mt)) maxTokens = mt;
                        break;
                    case "ai_temperature":
                        if (float.TryParse(row.ConfigValue, out var tp)) temperature = tp;
                        break;
                }
            }

            return (apiKey, baseUrl, model, maxTokens, temperature);
        }
        catch
        {
            return ("", defaultBaseUrl, defaultModel, defaultMaxTokens, defaultTemperature);
        }
    }
}
