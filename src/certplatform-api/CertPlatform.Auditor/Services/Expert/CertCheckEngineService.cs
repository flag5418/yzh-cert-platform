using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Auditor.Entities.Cert;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Domain.Clauses;
using CertPlatform.Shared.Services.Workflow;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert;

// ══════════════════════════════════════════════════════════════════════
// 请求 / 响应契约
// ══════════════════════════════════════════════════════════════════════

/// <summary>执行单检查项符合性分析请求</summary>
public class CertCheckAnalysisRequest
{
    /// <summary>任务项 Code</summary>
    public string TaskItemCode { get; set; } = string.Empty;

    /// <summary>
    /// 分支：<c>prep</c> / <c>onsite</c>。
    /// <para>为空 ⇒ 从 <c>cert_task.TaskKind</c> 推导（正常路径都走这个）。</para>
    /// <para>显式传入仅用于「不建任务、手工对某检查项跑一次准备分析」这种调试场景。</para>
    /// </summary>
    public string? StageKind { get; set; }

    /// <summary>
    /// 锁定项处理：<c>throttle</c>(跳过) / <c>override</c>(强制重跑)。
    /// <para>⚠️ 2026-11-01 起 <c>override</c> <b>不再能绕过锁</b> —— 锁是硬跳过，
    /// 唯一出口是解锁（46 号 §10.1）。本字段保留仅为兼容旧调用方，实际等价于 throttle。</para>
    /// </summary>
    public string LockedMode { get; set; } = "throttle";
}

/// <summary>符合性分析输出 DTO（从 LLM JSON 解析）</summary>
public class CertCheckConclusionDto
{
    public string Conclusion { get; set; } = "conformant"; // conformant / minor_nc / major_nc / observation
    public string Summary { get; set; } = "";
    /// <summary>审核证据正文（对应审核报告 Audit Evidence；可多条、按条款分点）</summary>
    public string AuditEvidence { get; set; } = "";
    /// <summary>逐条款审核证据（clause_evidence）</summary>
    public List<CertClauseEvidenceDto> ClauseEvidences { get; set; } = new();
    public string Recommendation { get; set; } = "";
    public int Confidence { get; set; } = 0;
    public List<string> EvidenceCodes { get; set; } = new();
    public string? NcSuggestion { get; set; }
    public string? SeveritySuggestion { get; set; }
    public string? ClauseCodes { get; set; }

    /// <summary>
    /// 疑点分析（<b>可多条</b>；空数组 = 无疑点 ⇒ 系统建议该检查项合格）。
    /// <para>2026-11-03：系统只给建议，疑点是否成立、最终是否符合由审核员判定。</para>
    /// </summary>
    public List<CertDoubtDto> Doubts { get; set; } = new();
    /// <summary>模型是否提供了疑点数组；缺失或格式错误不能视为无疑点。</summary>
    public bool DoubtsProvided { get; set; }

    // ═══ 准备分支专用（StageKind=prep）═══
    // 准备问「资料齐不齐」，检查问「符不符合」—— 两个值域，⛔ 不要混用（46 号 F3）。

    /// <summary>found / partial / missing / unknown / na</summary>
    public string? PrepState { get; set; }

    /// <summary>缺的材料（JSON 数组字符串）</summary>
    public string? MissingKindsJson { get; set; }

    /// <summary>跨文档矛盾（JSON 数组字符串）</summary>
    public string? ConflictsJson { get; set; }

    /// <summary>doc / remote / onsite / test（验到什么程度）</summary>
    public string? VerifyLevel { get; set; }

    /// <summary>准备期"建议开 NC 吗" + 理由（只是给现场的建议）</summary>
    public string? PrepNcHint { get; set; }
}

/// <summary>逐条款审核证据</summary>
public class CertClauseEvidenceDto
{
    /// <summary>条款号（4.1 / 7.1.2）</summary>
    public string ClauseNumber { get; set; } = "";
    /// <summary>该条款的审核证据正文</summary>
    public string AuditEvidence { get; set; } = "";
}

/// <summary>疑点（一条 = 一个可独立审阅的可疑点）</summary>
public class CertDoubtDto
{
    /// <summary>疑点标题（一句话）</summary>
    public string Point { get; set; } = "";

    /// <summary>疑点说明（为什么可疑：涉及哪份文件 / 条款 / 缺什么）</summary>
    public string Detail { get; set; } = "";
}

/// <summary>分析执行结果</summary>
public class CertAnalysisResult
{
    /// <summary>本次分析的 <c>cert_check_analysis.Code</c></summary>
    public string AnalysisCode { get; set; } = string.Empty;
    public string TaskItemCode { get; set; } = string.Empty;
    public string CheckItemCode { get; set; } = string.Empty;

    /// <summary>分支：prep / onsite</summary>
    public string StageKind { get; set; } = string.Empty;

    /// <summary>该 (检查项, 分支) 的第几次分析</summary>
    public int RunNo { get; set; }

    /// <summary>本分支的结论摘要（prep=PrepState / onsite=Conclusion）</summary>
    public string Conclusion { get; set; } = string.Empty;

    /// <summary>落了几个条款的结果行</summary>
    public int ClauseResultCount { get; set; }

    public bool LockedSkipped { get; set; }
    public int LockedSkippedCount { get; set; }
    public int EvidenceUsedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool LlmCalled { get; set; }
    public long? DurationMs { get; set; }
    public CertCheckConclusionDto? ConclusionData { get; set; }
}

// ══════════════════════════════════════════════════════════════════════
// 检查引擎
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 认证分析引擎（V2 — 准备/检查共用一条链 · 分支靠 StageKind 分流 · 锁硬跳过 · 分析层留痕）
/// <para>职责：</para>
/// <list type="bullet">
///   <item>（1）锁闸门：<c>cert_check_lock</c> 命中 ⇒ 硬跳过，⛔ 不调 LLM（§10.1，任何 RunMode 绕不过）</item>
///   <item>（2）执行分析：按分支解析提示词 → 组装 → 调 LLM → 解析 JSON → 落「分析行 + 逐条款结果 + 证据链」</item>
///   <item>（3）证据指纹：记录本次分析使用的 MD5 集 → <c>cert_check_analysis.EvidenceFingerprint</c>
///         （<b>只用于过期告警，不用于跳过</b>，§10.4）</item>
/// </list>
/// </summary>
public class CertCheckEngineService
{
    /// <summary>AI 连接配置（readonly struct 避开 record == 运算符的可访问性冲突）</summary>
    private readonly struct AiSettings
    {
        public string ApiKey { get; }
        public string BaseUrl { get; }
        public string Model { get; }
        public int MaxTokens { get; }
        public float Temperature { get; }
        public AiSettings(string apiKey, string baseUrl, string model, int maxTokens, float temperature)
        {
            ApiKey = apiKey;
            BaseUrl = baseUrl;
            Model = model;
            MaxTokens = maxTokens;
            Temperature = temperature;
        }
    }

    private readonly IDbOrm _db;
    private readonly LlmInvokeService _llm;
    private readonly IPromptWorkbench _promptWb;
    private readonly CertCheckLockService _lock;
    private readonly CertCheckContextBuilder _ctxBuilder;
    private readonly ILogger<CertCheckEngineService> _logger;

    /// <summary>分支常量 —— 与 <c>cert_task.TaskKind</c> / <c>cert_check_analysis.StageKind</c> 同值域</summary>
    public const string StagePrep = "prep";
    public const string StageOnsite = "onsite";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CertCheckEngineService(
        IDbOrm db,
        LlmInvokeService llm,
        IPromptWorkbench promptWb,
        CertCheckLockService lockService,
        CertCheckContextBuilder ctxBuilder,
        ILogger<CertCheckEngineService> logger)
    {
        _db = db;
        _llm = llm;
        _promptWb = promptWb;
        _lock = lockService;
        _ctxBuilder = ctxBuilder;
        _logger = logger;
    }

    // ================================================================
    // 一、执行单检查项分析（准备 / 检查共用一条链，靠 StageKind 分流）
    // ================================================================

    /// <summary>
    /// 执行单检查项分析（锁闸门 + 分析层落库 + 证据链快照 + LLM 调用）。
    ///
    /// <para><b>★ 2026-11-01 结构重写（46 号 §4.5）</b>，与旧版的四处差别：</para>
    /// <list type="number">
    ///   <item><b>L1 锁闸门②</b>：查 <c>cert_check_lock</c> 而不是结果行的 <c>IsLocked</c> ——
    ///         锁是<b>检查项级</b>的，且是<b>硬跳过</b>（任何 RunMode 都绕不过，§10.1）。</item>
    ///   <item><b>L3 提示词按分支分流</b>：prep→<c>audit_prep</c> / onsite→<c>audit_check</c>（F2）。
    ///         两者问的问题不同（"资料齐不齐" vs "符不符合"），⛔ 不能共用一个提示词。</item>
    ///   <item><b>L8 落库改成「一次分析一行」</b>：旧当前分析置 <c>IsCurrent=NULL</c> →
    ///         插新分析行（<c>RunNo+1</c>）→ 逐条款写结果 → 写证据链快照。
    ///         这是 F13「一行两段装不下两分支」的结构解。</item>
    ///   <item><b>逐条款落库</b>（F4）：旧版只落 <c>Clauses.Split(',')[0]</c>，多条款检查项会丢结论。</item>
    /// </list>
    /// </summary>
    public async Task<Result<CertAnalysisResult>> AnalyzeCheckItemAsync(
        string userCode, CertCheckAnalysisRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TaskItemCode))
            return Result<CertAnalysisResult>.Fail("任务项 Code 不能为空");

        // ---- L0：取任务项 + 任务头（拿 TaskKind 决定分支）----
        var itemResult = await _db.GetOneAsync<CertTaskItem>(i =>
            i.Code == req.TaskItemCode && !i.IsDeleted);
        if (itemResult.Data == null)
            return Result<CertAnalysisResult>.Fail("任务项不存在");

        var item = itemResult.Data;
        var task = (await _db.GetOneAsync<CertTask>(t =>
            t.Code == item.TaskCode && !t.IsDeleted)).Data;

        // 分支：显式传入优先，否则从任务头推导
        var stageKind = NormalizeStage(req.StageKind ?? task?.TaskKind);
        var isPrep = stageKind == StagePrep;

        var result = new CertAnalysisResult
        {
            TaskItemCode = req.TaskItemCode,
            CheckItemCode = item.CheckItemCode,
            StageKind = stageKind,
            AnalysisCode = Guid.NewGuid().ToString("N")
        };

        // ---- L1：闸门② 锁判定（硬跳过，⛔ 不调 LLM、不写结果）----
        if (await _lock.IsLockedAsync(item.CheckItemCode, stageKind))
        {
            result.LockedSkipped = true;
            result.LockedSkippedCount = 1;
            result.Message = $"检查项已锁定（{stageKind}），跳过 —— 锁的唯一出口是解锁（46 号 §10.1）";

            // 任务项留痕：skipped 而不是 failed（勾到已锁定的项是正常误操作，不是错误）
            item.Status = "skipped";
            item.Message = Truncate(result.Message, 500);
            item.UpdateBy = userCode;
            item.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(item,
                nameof(item.Status), nameof(item.Message), nameof(item.UpdateBy), nameof(item.UpdateTime));

            await LogAnalysisAsync(result);
            return Result<CertAnalysisResult>.Ok(result);
        }

        // ---- L2：取检查项明细 + 关联证据 ----
        var detail = (await _db.GetOneAsync<CertAuditPlanDetail>(d =>
            d.Code == item.CheckItemCode && !d.IsDeleted)).Data;

        var evidences = (await _db.GetListAsync<CertEvidence>(e =>
            e.CheckItemCode == item.CheckItemCode && !e.IsDeleted)).Data
            ?? new List<CertEvidence>();
        var activeEvidences = evidences.Where(e =>
            !string.Equals(e.Status, "dropped", StringComparison.OrdinalIgnoreCase)).ToList();

        // ---- L2.5：装配三模型（★ 条款模型 + 文档模型 → 原始证据）----
        // 用户口径（2026-11-02）：检查项 → 多个条款 Code → 按条款编号过滤企业文档 →
        // 按条款聚合摘录 → 作为原始证据交给 LLM。一个检查项一次推理，信息量天然有界。
        var planClauseCodes = SplitClauses(detail?.Clauses, detail?.ClauseNumbers)
            .Select(c => c.ClauseCode).ToList();
        var clauseRefs = ClauseRefs.Parse(detail?.ClauseRefsJson);
        var context = await _ctxBuilder.BuildAsync(
            planClauseCodes, task?.EnterpriseCode, activeEvidences,
            clauseRefs.Count > 0 ? clauseRefs : null, detail?.DeptNames);

        foreach (var w in context.Warnings)
            _logger.LogWarning("[CertCheck] 装配告警 item={Item} {Warn}", item.CheckItemCode, w);
        if (context.Clauses.Count == 0)
            return Result<CertAnalysisResult>.Fail(
                $"条款无法完整装配，请重新选择或修复条款：{string.Join("；", context.Warnings)}");
        if (await HasCurrentManualDecisionAsync(item.CheckItemCode, stageKind))
            return Result<CertAnalysisResult>.Fail("当前分析已有审核员判定，暂不自动替换，请先处理人工判定");

        // ---- L3：解析提示词（★ 按分支分流，F2）----
        var standardCode = task != null ? await ResolveStandardCodeAsync(task.PlanCode) : null;
        var promptType = isPrep ? PromptTypes.AuditPrep : PromptTypes.AuditCheck;

        var promptView = await _promptWb.ResolveActiveAsync(promptType, standardCode);
        if (promptView?.Template == null)
            return Result<CertAnalysisResult>.Fail(isPrep
                ? "未找到认证准备提示词模板（audit_prep）"
                : "未找到符合性分析提示词模板（audit_check）");

        // 回退告警（§6.2）：命中平台级 = 该标准还没真正配，⛔ 不能静默
        if (string.IsNullOrEmpty(promptView.StandardCode))
            _logger.LogWarning(
                "[CertCheck] 提示词回退到平台级 type={Type} standard={Standard}",
                promptType, standardCode ?? "(null)");

        // ---- L4：组装 prompt（★ 真正渲染 DB 里的模板正文，按分支取不同占位符集）----
        var prompt = BuildPrompt(promptView.Template, stageKind, detail, context, activeEvidences);
        if (Encoding.UTF8.GetByteCount(prompt) > 60000)
            return Result<CertAnalysisResult>.Fail("完整分析输入超过当前 60000 字节留痕上限，未调用模型；需要分批处理，不能截断资料后生成结论");

        // ---- L5：计算证据指纹（只用于过期告警，不用于跳过）----
        var fingerprint = string.Join("|", evidences
            .Where(e => !string.IsNullOrEmpty(e.ContentMD5))
            .Select(e => e.ContentMD5!)
            .OrderBy(m => m));

        // ---- L6：调 LLM（两段式优先：① 审核发现 → ② 证据生成；缺提示词/失败回退旧单段）----
        var aiSettings = await GetAiSettingsAsync();
        var twoStage = await RunTwoStageAsync(stageKind, detail, context, activeEvidences, aiSettings);
        var llmResponse = twoStage?.Response ?? await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = aiSettings.BaseUrl,
            ApiKey = aiSettings.ApiKey,
            Model = aiSettings.Model,
            Temperature = aiSettings.Temperature,
            MaxTokens = aiSettings.MaxTokens,
            SystemPrompt = isPrep
                ? "你是认证审核专家，正在做认证准备（入场前资料完备性检查）。请判断该检查项的资料是否齐全，输出 JSON。"
                : "你是认证审核专家，请根据条款要求和证据内容，输出符合性判定 JSON。",
            Prompt = prompt,
            DocumentContent = "",
            ForceJson = true
        });
        var pipeline = twoStage?.Pipeline ?? "single";

        result.LlmCalled = true;
        result.DurationMs = llmResponse.DurationMs;
        result.EvidenceUsedCount = evidences.Count;

        if (!llmResponse.Success || llmResponse.Json == null || llmResponse.JsonRepaired
            || string.Equals(llmResponse.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
            || (llmResponse.CompletionTokens.HasValue && llmResponse.CompletionTokens.Value >= aiSettings.MaxTokens))
        {
            var failure = llmResponse.Success
                ? "模型输出不完整或不是有效 JSON"
                : llmResponse.Message ?? "未知错误";
            item.AnalysisState = "failed";
            item.Status = "failed";
            item.Message = Truncate($"LLM 调用失败：{failure}", 500);
            item.UpdateBy = userCode;
            item.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(item,
                nameof(item.AnalysisState), nameof(item.Status), nameof(item.Message),
                nameof(item.UpdateBy), nameof(item.UpdateTime));

            // 失败也留一条分析行（否则"跑过但失败"在界面上完全看不见）
            await InsertFailedAnalysisAsync(userCode, item, task, stageKind, promptType,
                promptView, fingerprint, $"LLM 调用失败：{failure}");

            result.Message = $"LLM 调用失败：{failure}";
            await LogAnalysisAsync(result);
            return Result<CertAnalysisResult>.Fail(result.Message);
        }

        // ---- L7：解析 LLM JSON ----
        var conclusion = ParseConclusion(llmResponse);
        if (conclusion == null)
            return Result<CertAnalysisResult>.Fail("模型结果无法解析，未替换已有分析结果");
        result.ConclusionData = conclusion;

        // 资料覆盖尚未经过完整性验证，无疑点不能推断为合格。
        var aiConclusion = isPrep || context.Clauses.Count != 1 ? null : ResolveSuggestedConclusion(conclusion);
        var aiSeverity = isPrep || aiConclusion != CertVerdictMapper.ConclusionNc
            ? null
            : CertVerdictMapper.ToSeverity(conclusion?.Conclusion, conclusion?.SeveritySuggestion);

        // 返回值里的 Conclusion 用「系统建议」口径（onsite 的 major_nc → nc），
        // 原始值保留在 ConclusionData 里供排查
        result.Conclusion = isPrep
            ? (CertVerdictMapper.NormalizePrepState(conclusion?.PrepState) ?? "unknown")
            : (aiConclusion ?? "unknown");

        // ---- L8：落库（分析行 + 逐条款结果 + 证据链）----
        // ★ 条款来源优先用装配上下文（条款号取自 cert_iso_clause，权威）；
        //   计划明细的 ClauseNumbers 是导入时的手填串，实测有 "m 1" 这类脏前缀。
        var clausePairs = context.Clauses.Select(c => (c.ClauseCode, (string?)c.ClauseNumber)).ToList();
        var now = DateTime.Now;
        // ★ 2026-11-25 逐条款审核证据预清洗 + 归一：供「按逻辑条款合并」使用（跨标准同号 / 父子条款）。
        var evidenceMap = BuildClauseEvidenceMap(conclusion);

        using var tran = _db.BeginTransaction();
        try
        {
            // ③ 闸门：写入前再断言一次（防「入队后、执行前被锁上」的时间窗，§10.2）
            if (await _lock.IsLockedAsync(item.CheckItemCode, stageKind))
                throw new InvalidOperationException(
                    $"检查项已锁定，拒绝写入（CheckItemCode={item.CheckItemCode}, StageKind={stageKind}）");
            if (await HasCurrentManualDecisionAsync(item.CheckItemCode, stageKind))
                throw new InvalidOperationException("分析期间出现审核员判定，拒绝替换当前结果");

            // 8.1 旧当前分析置 IsCurrent=NULL，算本次 RunNo
            var runNo = await DemoteCurrentAnalysisAsync(item.CheckItemCode, stageKind, userCode);
            result.RunNo = runNo;

            // 8.2 插分析行（IsCurrent=1）
            var analysisInsert = await _db.InsertAsync(new CertCheckAnalysis
            {
                Code = result.AnalysisCode,
                CheckItemCode = item.CheckItemCode,
                StageKind = stageKind,
                EnterpriseCode = task?.EnterpriseCode,
                PlanCode = task?.PlanCode,
                TaskCode = item.TaskCode,
                TaskItemCode = item.Code,
                IsCurrent = 1,                       // ⛔ 历史行才是 NULL，当前行必须是 1
                RunNo = runNo,
                Status = "done",
                PromptType = promptType,
                PromptCode = promptView.Code,
                PromptVersion = promptView.Version.ToString(),
                ModelName = aiSettings.Model,
                EvidenceFingerprint = fingerprint,
                InputSnapshotJson = prompt,
                ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    stage_kind = stageKind,
                    pipeline = pipeline,
                    clause_count = clausePairs.Count,
                    evidence_count = evidences.Count,
                    // ★ 装配质量指标：这三项为 0 就说明「三模型没串上」，⛔ 不能只看结论
                    attr_hit_count = context.AttrHitCount,
                    doc_hit_count = context.DocHitCount,
                    excerpt_count = context.ExcerptCount,
                    assembly_warnings = context.Warnings,
                    // ★ 召回路径记账（改造4）：逐条款「RM 计划轴 + 来源份数」，供审核员核对「不遗漏、不虚增」
                    recall_trace = context.RecallTrace,
                    prep_state = conclusion?.PrepState,
                    conclusion = conclusion?.Conclusion,
                    // 原始值 vs 落库值并存：排查「模型说 major_nc，库里却是 nc」时不用再猜
                    conclusion_mapped = isPrep
                        ? CertVerdictMapper.NormalizePrepState(conclusion?.PrepState)
                        : CertVerdictMapper.ToConclusion(conclusion?.Conclusion),
                    // 系统建议（⛔ 不是最终结论；最终由审核员判定）
                    ai_suggestion = isPrep
                        ? CertVerdictMapper.NormalizePrepState(conclusion?.PrepState)
                        : aiConclusion,
                    ai_severity_suggestion = aiSeverity,
                    doubt_count = conclusion?.Doubts.Count ?? 0,
                    doubts_provided = conclusion?.DoubtsProvided ?? false,
                    doubts = conclusion?.Doubts,
                    summary = conclusion?.Summary,
                    audit_evidence = conclusion?.AuditEvidence,
                    clause_evidence = conclusion?.ClauseEvidences,
                    severity = isPrep
                        ? null
                        : CertVerdictMapper.ToSeverity(conclusion?.Conclusion, conclusion?.SeveritySuggestion),
                    confidence = conclusion?.Confidence
                }),
                AnalyzedAt = now,
                DurationMs = (int)llmResponse.DurationMs,
                InputTokenCount = llmResponse.PromptTokens,
                OutputTokenCount = llmResponse.CompletionTokens,
                CreateBy = userCode,
                CreateTime = now
            });
            if (!analysisInsert.Success)
                throw new InvalidOperationException(analysisInsert.Error ?? "保存分析记录失败");

            // 8.3 逐条款写结果行（F4：全部条款，不是首条）
            var written = 0;
            foreach (var (clauseCode, clauseNumber) in clausePairs)
            {
                // 多标准计划：条款自己的标准优先（⛔ 不能用「计划里的第一个标准」覆盖所有条款）
                var clauseStandard = context.Clauses
                    .FirstOrDefault(c => string.Equals(c.ClauseCode, clauseCode, StringComparison.Ordinal))
                    ?.StandardCode;
                var resultInsert = await _db.InsertAsync(new CertCheckResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    AnalysisCode = result.AnalysisCode,
                    StageKind = stageKind,
                    CheckItemCode = item.CheckItemCode,
                    ClauseCode = clauseCode,
                    ClauseNumber = clauseNumber,
                    StandardCode = string.IsNullOrWhiteSpace(clauseStandard) ? standardCode : clauseStandard,

                    // ★ 一行只填一段（§4.5.4）：prep 只写 Prep 组、onsite 只写 Check 组
                    PrepState = isPrep && clausePairs.Count == 1 ? CertVerdictMapper.NormalizePrepState(conclusion?.PrepState) : null,
                    MissingKindsJson = isPrep && clausePairs.Count == 1 ? conclusion?.MissingKindsJson : null,
                    ConflictsJson = isPrep && clausePairs.Count == 1 ? conclusion?.ConflictsJson : null,
                    VerifyLevel = isPrep && clausePairs.Count == 1 ? conclusion?.VerifyLevel : null,
                    PrepNcHint = isPrep && clausePairs.Count == 1 ? conclusion?.PrepNcHint : null,

                    // ★ 2026-11-03：分析只写「系统建议」，⛔ 不写最终结论
                    //   最终 Conclusion / Severity 由审核员在判定弹窗里写（SaveResultAsync）。
                    //   无疑点 ⇒ 建议合格；有疑点 ⇒ 按模型结论映射，模型说合格也不给「合格」的假建议。
                    AiConclusion = isPrep ? null : aiConclusion,
                    AiSeverity = isPrep ? null : aiSeverity,
                    DoubtsJson = isPrep || clausePairs.Count != 1
                        ? null
                        : (conclusion != null && conclusion.Doubts.Count > 0
                            // ★ 2026-11-25 疑点同样过清洗（剥系统痕迹 + 英文字段中文化），但不剥「未标注/缺失」（疑点本就是写这个）
                            ? JsonSerializer.Serialize(conclusion.Doubts.Select(d => new CertDoubtDto
                            {
                                Point = SanitizeDoubtText(d.Point),
                                Detail = SanitizeDoubtText(d.Detail)
                            }).ToList())
                            : null),
                    NcSuggested = isPrep ? null : aiConclusion == CertVerdictMapper.ConclusionNc,
                    ConclusionBy = null,
                    ConclusionAt = null,

                    // 审核证据：★ 2026-11-25 改为「按逻辑条款合并 + 写入前清洗」——
                    //   ① 跨标准同条款号（3 体系的 4.1）/ 父子条款（4 与 4.1）合并为一段；
                    //   ② 剥除置信度 / 召回标签 / 内部编号 / 元数据缺失注等「系统痕迹」；
                    //   ③ 优先该条款证据 → 回退整体 audit_evidence → summary。
                    // ⛔ 审核发现（文档原文）不写在这里 —— 它是事实来源，不是审核员署名证据。
                    Reason = ResolveEvidenceForClause(conclusion, clauseNumber, clausePairs, evidenceMap),
                    RollupTo = true,
                    CreateBy = userCode,
                    CreateTime = now
                });
                if (!resultInsert.Success)
                    throw new InvalidOperationException(resultInsert.Error ?? "保存条款结果失败");
                written++;
            }
            result.ClauseResultCount = written;

            // 8.4 证据链快照（这次用了哪几条，当时内容是什么）
            await WriteEvidenceLinksAsync(result.AnalysisCode, evidences, userCode, now);

            // 任务项状态
            item.AnalysisState = "done";
            item.Status = "done";
            item.RerunState = "none";
            item.AnalyzeCount += 1;
            item.LastAnalyzedAt = now;
            item.EvidenceCount = evidences.Count;
            item.Message = null;
            item.UpdateBy = userCode;
            item.UpdateTime = now;
            var itemUpdate = await _db.UpdateAsync(item,
                nameof(item.AnalysisState), nameof(item.Status), nameof(item.RerunState),
                nameof(item.AnalyzeCount), nameof(item.LastAnalyzedAt), nameof(item.EvidenceCount),
                nameof(item.Message), nameof(item.UpdateBy), nameof(item.UpdateTime));
            if (!itemUpdate.Success)
                throw new InvalidOperationException(itemUpdate.Error ?? "更新任务状态失败");

            // 分析审计日志（运维指标：跑了多少次 / 指纹 / 跳过数）
            await LogAnalysisAsync(result, result.AnalysisCode, fingerprint);

            tran.Commit();
        }
        catch (Exception ex)
        {
            tran.Rollback();
            _logger.LogError(ex, "分析落库失败 ItemCode={ItemCode}", req.TaskItemCode);
            return Result<CertAnalysisResult>.Fail($"分析落库失败：{ex.Message}");
        }

        result.Message = clausePairs.Count == 0
            ? $"分析完成（{stageKind}）→ {(isPrep ? "准备状态" : "系统建议")} {result.Conclusion}；⚠️ 该检查项未配置条款，未写结论行"
            : $"分析完成（{stageKind}）→ {(isPrep ? "准备状态" : "系统建议")} {result.Conclusion}"
              + (isPrep ? "" : "（最终结论由审核员判定）")
              + $"，{result.ClauseResultCount} 个条款";
        return Result<CertAnalysisResult>.Ok(result);
    }

    // ================================================================
    // 二、Prompt 组装（★ 模板正文真正参与渲染 —— 旧版 BuildCheckPrompt 完全忽略 template 参数）
    // ================================================================

    /// <summary>
    /// 组装最终 prompt：<b>渲染 DB 里的模板正文</b> + 填入结构化块。
    ///
    /// <para><b>★ 2026-11-01 修复</b>：旧版 <c>BuildCheckPrompt(template, ...)</c> 收了
    /// <paramref name="template"/> 却<b>从不使用</b> —— 提示词工作台改的正文对分析毫无影响，
    /// 等于「提示词表是个摆设」。现在改为：</para>
    /// <list type="bullet">
    ///   <item>模板里出现已知占位符 ⇒ 原位替换（<c>StringComparison.Ordinal</c>，⛔ 不走文化相关比较）；</item>
    ///   <item>模板里一个占位符都没有 ⇒ 当作「前言」，后面接结构化块（兼容静态正文）；</item>
    ///   <item>替换后仍残留 <c>{{</c> ⇒ 记错误日志（正文拼错或程序没传值，跑下去只会得到废结论）。</item>
    /// </list>
    /// </summary>
    private string BuildPrompt(
        string template,
        string stageKind,
        Entities.Expert.CertAuditPlanDetail? detail,
        CertCheckContext context,
        List<CertEvidence> evidences)
    {
        var isPrep = stageKind == StagePrep;

        var clauseBlocks = context.RenderClauseBlocks(isPrep);
        var onsiteBlock = BuildOnsiteEvidenceBlock(evidences);

        // 键值对按 §6.3 的占位符表；后三个是 DB 里旧 audit_check 正文用的名字（兼容）
        //
        // ★ 2026-11-02：旧名字不再各自拼一份残片，而是<b>复用同一份装配结果</b> ——
        //   旧实现里 {{ClauseInfo}} 只给「条款编码/序号/部门」，{{EvidenceList}} 只给
        //   cert_evidence（实测 0 行）⇒ onsite 分支永远拿不到条款属性和企业文档摘录，
        //   实测判出「未提供任何支持性证据」。三个名字现在分别是：
        //     {{ClauseInfo}} / {{CheckRequirement}} → 条款清单（原文 + 9 属性）
        //     {{EvidenceList}}                       → 企业文档命中（按条款聚合的摘录）
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{task_kind}}"] = stageKind,
            ["{{check_item_name}}"] = BuildCheckItemName(detail),
            ["{{dept_names}}"] = string.IsNullOrWhiteSpace(detail?.DeptNames) ? "-" : detail!.DeptNames!,
            ["{{clause_blocks}}"] = clauseBlocks,
            ["{{previous_results}}"] = "",          // incremental 才有值，V1 留空
            ["{{onsite_evidence}}"] = isPrep ? "" : onsiteBlock,

            ["{{ClauseInfo}}"] = context.RenderClauseInfo(),
            ["{{CheckRequirement}}"] = context.RenderClauseInfo(),
            ["{{EvidenceList}}"] = context.RenderDocHits()
        };

        var hasPlaceholder = map.Keys.Any(k => template.Contains(k, StringComparison.Ordinal));
        var rendered = template;
        foreach (var kv in map)
            rendered = rendered.Replace(kv.Key, kv.Value, StringComparison.Ordinal);

        // 静态正文（无任何占位符）⇒ 当前言，后面补结构化块，否则模型拿不到任何条款/证据
        if (!hasPlaceholder)
            rendered = rendered.TrimEnd() + "\n\n" + clauseBlocks + (isPrep ? "" : "\n\n" + onsiteBlock);

        // 残留占位符 ⇒ 正文拼错或程序没传值。⛔ 不静默：记错误日志便于运营修正文。
        if (rendered.Contains("{{", StringComparison.Ordinal))
        {
            var residual = System.Text.RegularExpressions.Regex
                .Matches(rendered, @"\{\{[A-Za-z0-9_]+\}\}")
                .Select(m => m.Value).Distinct().ToList();
            _logger.LogError(
                "[CertCheck] 提示词渲染后仍残留占位符 stage={Stage} residual={Residual}",
                stageKind, string.Join(",", residual));
        }

        return rendered;
    }

    /// <summary>「03 文件控制（4.2.4/4.2.5）｜品质部」这样的可读检查项名</summary>
    private static string BuildCheckItemName(Entities.Expert.CertAuditPlanDetail? detail)
    {
        if (detail == null) return "-";
        var numbers = detail.ClauseNumbers ?? detail.Clauses ?? "-";
        var dept = string.IsNullOrWhiteSpace(detail.DeptNames) ? "" : $"｜{detail.DeptNames}";
        return $"{numbers}{dept}";
    }

    /// <summary>
    /// 现场证据块（<c>{{onsite_evidence}}</c>，仅检查分支）。
    /// <para>访谈转录 / 照片说明 / 抽样记录 —— 与准备分支的"文档摘录"是两套东西。</para>
    /// </summary>
    private static string BuildOnsiteEvidenceBlock(List<CertEvidence> evidences)
    {
        var onsite = evidences.Where(e =>
            string.Equals(e.CapturedIn, StageOnsite, StringComparison.OrdinalIgnoreCase) ||
            e.EvidenceKind == "onsite_media" || e.EvidenceKind == "manual").ToList();

        if (onsite.Count == 0) return "## 现场证据\n(无现场采集证据)";

        var sb = new StringBuilder("## 现场证据\n");
        foreach (var ev in onsite)
        {
            sb.AppendLine($"- [{ev.Code}] {ev.SourceName ?? ev.Code}（{ev.EvidenceKind}）");
            if (!string.IsNullOrEmpty(ev.TranscriptText))
                sb.AppendLine($"  转录：{ev.TranscriptText}");
            if (!string.IsNullOrEmpty(ev.CapturedPlace))
                sb.AppendLine($"  地点：{ev.CapturedPlace}");
            if (!string.IsNullOrEmpty(ev.PresentPersons))
                sb.AppendLine($"  在场人员：{ev.PresentPersons}");
        }
        return sb.ToString();
    }

    // ================================================================
    // 两段式证据链路：① 审核发现（clause_evidence_findings）→ ② 证据生成（audit_check_evidence）
    // 提示词统一存在 cert_prompt_template（不再散落），缺行 / 失败则回退旧单段 audit_check。
    // ================================================================

    /// <summary>读统一提示词表 <c>cert_prompt_template</c> 里的平台级生效行（OrgCode 空、ScopeKey 空）。</summary>
    private async Task<string?> ResolveCertPromptTemplateAsync(string promptCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode)) return null;
        var rows = await _db.Client.Queryable<CertPromptTemplateRow>()
            .Where(x => x.PromptCode == promptCode && !x.IsDeleted && x.IsValid == 1
                        && (x.OrgCode == null || x.OrgCode == ""))
            .ToListAsync();
        return (rows ?? new List<CertPromptTemplateRow>())
            .Where(x => string.IsNullOrWhiteSpace(x.ScopeKey) && !string.IsNullOrWhiteSpace(x.Template))
            .Select(x => x.Template)
            .FirstOrDefault();
    }

    /// <summary>两段式执行结果：把响应与「实际走的管线」一起带回，便于落库留痕。</summary>
    private sealed record TwoStageOutcome(LlmInvokeResponse Response, string Pipeline);

    /// <summary>
    /// 两段式分析（仅 onsite 检查分支）：先逐条款跑「审核发现」，再跑一次「证据生成」。
    /// <para>任一端提示词缺行 / 第一段失败 ⇒ 返回 null，由调用方回退旧单段 audit_check。</para>
    /// </summary>
    private async Task<TwoStageOutcome?> RunTwoStageAsync(
        string stageKind, Entities.Expert.CertAuditPlanDetail? detail,
        CertCheckContext context, List<CertEvidence> evidences, AiSettings aiSettings)
    {
        if (!string.Equals(stageKind, StageOnsite, StringComparison.Ordinal)) return null;

        var findingsTpl = await ResolveCertPromptTemplateAsync(PromptTypes.ClauseEvidenceFindings);
        var genTpl = await ResolveCertPromptTemplateAsync(PromptTypes.AuditEvidenceGen);
        if (string.IsNullOrWhiteSpace(findingsTpl) || string.IsNullOrWhiteSpace(genTpl))
        {
            _logger.LogWarning(
                "[CertCheck] 两段式提示词缺行 findings={F} gen={G}，回退旧单段 audit_check",
                string.IsNullOrWhiteSpace(findingsTpl) ? "missing" : "ok",
                string.IsNullOrWhiteSpace(genTpl) ? "missing" : "ok");
            return null;
        }

        var findings = new List<string>();
        foreach (var clause in context.Clauses)
        {
            var resp = await _llm.CompleteAsync(new LlmInvokeRequest
            {
                BaseUrl = aiSettings.BaseUrl,
                ApiKey = aiSettings.ApiKey,
                Model = aiSettings.Model,
                Temperature = aiSettings.Temperature,
                MaxTokens = aiSettings.MaxTokens,
                SystemPrompt = "你是认证审核专家，正在做第一阶段「审核发现」：只从条款与企业文档摘录中找回事实，不写符合性结论。输出 JSON。",
                Prompt = RenderFindingsPrompt(findingsTpl, clause, context),
                DocumentContent = "",
                ForceJson = true
            });
            if (!resp.Success || resp.Json == null)
            {
                _logger.LogWarning("[CertCheck] 两段式第一段失败 clause={C} msg={M}", clause.ClauseNumber, resp.Message);
                return null;
            }
            findings.Add(resp.Json.RootElement.GetRawText());
        }

        var (findingsText, doubtsText, assembliesText) = SplitFindings(findings);
        var genResp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = aiSettings.BaseUrl,
            ApiKey = aiSettings.ApiKey,
            Model = aiSettings.Model,
            Temperature = aiSettings.Temperature,
            MaxTokens = aiSettings.MaxTokens,
            SystemPrompt = "你是认证审核专家，正在做第二阶段「证据生成」：消费审核发现，写出审核证据并给出默认结论。输出 JSON。",
            Prompt = RenderEvidenceGenPrompt(genTpl, detail, context, findingsText, doubtsText, assembliesText, evidences),
            DocumentContent = "",
            ForceJson = true
        });
        if (!genResp.Success || genResp.Json == null)
            return null;

        _logger.LogInformation(
            "[CertCheck] 两段式执行成功 item={Item} clauses={N} findings={F}",
            detail?.Code, context.Clauses.Count, findings.Count);
        return new TwoStageOutcome(genResp, "two_stage");
    }

    private static string RenderFindingsPrompt(string template, ClauseContext clause, CertCheckContext context)
    {
        var allowedRefs = string.Join("、", context.Clauses.Select(c => c.ClauseNumber).Distinct());
        var excerpts = new StringBuilder();
        foreach (var s in clause.Slices)
        {
            excerpts.AppendLine($"《{s.FileName}》");
            for (var i = 0; i < s.Excerpts.Count; i++)
                excerpts.AppendLine(s.Excerpts[i]);
        }

        return template
            .Replace("{{standard_name}}", "", StringComparison.Ordinal)
            .Replace("{{mode}}", "findings", StringComparison.Ordinal)
            .Replace("{{clause_number}}", clause.ClauseNumber, StringComparison.Ordinal)
            .Replace("{{clause_title}}", clause.Title, StringComparison.Ordinal)
            .Replace("{{clause_content}}", clause.Description ?? "", StringComparison.Ordinal)
            .Replace("{{chapter_profile}}", clause.ChapterProfile, StringComparison.Ordinal)
            .Replace("{{allowed_clause_refs}}", allowedRefs, StringComparison.Ordinal)
            .Replace("{{referenced_clauses}}", "", StringComparison.Ordinal)
            .Replace("{{doc_facts}}", clause.FactText, StringComparison.Ordinal)
            .Replace("{{doc_excerpts}}", excerpts.ToString(), StringComparison.Ordinal)
            .Replace("{{recall_paths}}", RenderRecallPaths(clause), StringComparison.Ordinal)
            .Replace("{{doc_catalog}}", context.DocCatalogText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 渲染「召回轴计划 vs 实际命中」（供 <c>clause_evidence_findings</c> 的 <c>{{recall_paths}}</c>）。
    /// <para>目的：让模型知道<b>本该从哪条轴找回、实际有没有找回</b> ——
    /// ⛔ 轴 0 命中只表示「本次召回未找到」，<b>不等于</b>企业没有该证据（可能是文档未提取/未上传/键不匹配）。</para>
    /// </summary>
    private static string RenderRecallPaths(ClauseContext clause)
    {
        var hitDocs = clause.Slices.Select(s => s.FileCode).Distinct(StringComparer.Ordinal).Count();
        var sb = new StringBuilder();
        sb.AppendLine($"本条命中企业文档份数：{hitDocs}");
        if (clause.RecallPaths.Count == 0)
        {
            sb.AppendLine("召回轴计划：（条款属性缺失，未生成召回计划）");
            return sb.ToString();
        }

        sb.AppendLine("召回轴计划（RM = 系统按条款属性推导的召回路线）：");
        foreach (var p in clause.RecallPaths)
        {
            var key = string.IsNullOrWhiteSpace(p.Key) ? "" : $" 键={p.Key}";
            var types = p.DocTypes.Count > 0 ? $" 体裁={string.Join("/", p.DocTypes)}" : "";
            var reason = string.IsNullOrWhiteSpace(p.Reason) ? "" : $" 原因={p.Reason}";
            sb.AppendLine($"- {p.Axis} {AxisName(p.Axis)}{key}{types} 提取={p.Extract}{reason}");
        }
        sb.AppendLine("（说明：轴计划 = 系统按条款属性推导的召回路线；命中 0 份仅代表「本次未召回」，⛔ 不得据此写成企业未提供。）");
        return sb.ToString();
    }

    /// <summary>召回轴代号 → 中文名（仅供提示词可读性，⛔ 不参与任何业务判定）。</summary>
    private static string AxisName(string axis) => axis switch
    {
        RecallAxis.Clause => "条款轴",
        RecallAxis.Process => "过程轴",
        RecallAxis.Dept => "部门轴",
        RecallAxis.DocNo => "编号轴",
        RecallAxis.Site => "现场轴",
        RecallAxis.Interview => "访谈轴",
        RecallAxis.Semantic => "语义轴",
        _ => axis
    };

    private static string RenderEvidenceGenPrompt(
        string template, Entities.Expert.CertAuditPlanDetail? detail,
        CertCheckContext context, string findings, string doubts, string assemblies, List<CertEvidence> evidences)
    {
        return template
            .Replace("{{check_item_name}}", BuildCheckItemName(detail), StringComparison.Ordinal)
            .Replace("{{dept_names}}", string.IsNullOrWhiteSpace(detail?.DeptNames) ? "-" : detail!.DeptNames!, StringComparison.Ordinal)
            .Replace("{{clause_blocks}}", context.RenderClauseInfo(), StringComparison.Ordinal)
            .Replace("{{findings}}", findings, StringComparison.Ordinal)
            .Replace("{{doubts}}", doubts, StringComparison.Ordinal)
            .Replace("{{assemblies}}", assemblies, StringComparison.Ordinal)
            .Replace("{{onsite_evidence}}", BuildOnsiteEvidenceBlock(evidences), StringComparison.Ordinal);
    }

    /// <summary>
    /// 把第一段「审核发现」的逐条款 JSON 拆成三段喂给第二段：
    /// <c>EvidenceFindings</c>（事实）、<c>Doubts</c>（疑点）、<c>EvidenceAssembly</c>（证据组合），
    /// 各自带条款号前缀，避免第二段再啃整份发现 JSON。
    /// </summary>
    private static (string Findings, string Doubts, string Assemblies) SplitFindings(List<string> findingsJson)
    {
        var f = new StringBuilder();
        var d = new StringBuilder();
        var a = new StringBuilder();
        foreach (var raw in findingsJson)
        {
            string clauseNo = "";
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                // 精简版 findings 直接输出顶层 ClauseNumber；保留旧版 ClauseContext 兜底
                if (root.TryGetProperty("ClauseNumber", out var cn)
                    && cn.ValueKind == JsonValueKind.String)
                    clauseNo = cn.GetString() ?? "";
                else if (root.TryGetProperty("ClauseContext", out var cc)
                    && cc.ValueKind == JsonValueKind.Object
                    && cc.TryGetProperty("ClauseNumber", out var ccn)
                    && ccn.ValueKind == JsonValueKind.String)
                    clauseNo = ccn.GetString() ?? "";

                AppendFindingSection(f, root, "EvidenceFindings", clauseNo, "证据发现");
                AppendFindingSection(d, root, "Doubts", clauseNo, "疑点");
                AppendFindingSection(a, root, "EvidenceAssembly", clauseNo, "证据组合");
            }
            catch (JsonException)
            {
                f.AppendLine(raw);
            }
        }
        return (f.ToString(), d.ToString(), a.ToString());
    }

    private static void AppendFindingSection(StringBuilder sb, JsonElement root, string prop, string clauseNo, string header)
    {
        if (!root.TryGetProperty(prop, out var el)
            || el.ValueKind != JsonValueKind.Array
            || el.GetArrayLength() == 0) return;

        sb.AppendLine($"## {header}（条款 {clauseNo}）");
        foreach (var item in el.EnumerateArray())
            sb.AppendLine(item.GetRawText());
        sb.AppendLine();
    }

    // ================================================================
    // 三、LLM JSON 解析
    // ================================================================

    private CertCheckConclusionDto? ParseConclusion(LlmInvokeResponse response)
    {
        if (response.Json == null)
            return null;

        try
        {
            var root = response.Json.RootElement;
            return new CertCheckConclusionDto
            {
                Conclusion = root.TryGetProperty("conclusion", out var c) ? c.GetString() ?? "unknown" : "unknown",
                Summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                AuditEvidence = GetStr(root, "audit_evidence") ?? "",
                ClauseEvidences = ParseClauseEvidences(root),
                Recommendation = root.TryGetProperty("recommendation", out var r) ? r.GetString() ?? "" : "",
                Confidence = root.TryGetProperty("confidence", out var f) ? f.GetInt32() : 0,
                NcSuggestion = root.TryGetProperty("nc_suggestion", out var n) ? n.GetString() : null,
                SeveritySuggestion = root.TryGetProperty("severity_suggestion", out var sv) ? sv.GetString() : null,
                EvidenceCodes = root.TryGetProperty("evidence_codes", out var evs) && evs.ValueKind == JsonValueKind.Array
                    ? evs.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                    : new List<string>(),
                Doubts = ParseDoubts(root),
                DoubtsProvided = root.TryGetProperty("doubts", out var doubts) && doubts.ValueKind == JsonValueKind.Array,

                // ── 准备分支字段（audit_prep 输出；缺失时保持 null，⛔ 不要回填成检查分支的值）──
                PrepState = GetStr(root, "prep_state"),
                MissingKindsJson = GetJsonArrayText(root, "missing_kinds"),
                ConflictsJson = GetJsonArrayText(root, "conflicts"),
                VerifyLevel = GetStr(root, "verify_level"),
                PrepNcHint = GetStr(root, "prep_nc_hint")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 LLM 结论 JSON 失败");
            return null;
        }
    }

    /// <summary>解析逐条款审核证据 <c>clause_evidence</c>。</summary>
    private static List<CertClauseEvidenceDto> ParseClauseEvidences(JsonElement root)
    {
        var list = new List<CertClauseEvidenceDto>();
        if (!root.TryGetProperty("clause_evidence", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var no = GetStr(item, "clause_number") ?? GetStr(item, "ClauseNumber") ?? "";
            var text = GetStr(item, "audit_evidence") ?? GetStr(item, "AuditEvidence") ?? "";
            if (string.IsNullOrWhiteSpace(no) || string.IsNullOrWhiteSpace(text)) continue;
            list.Add(new CertClauseEvidenceDto
            {
                ClauseNumber = no.Trim(),
                AuditEvidence = text.Trim()
            });
        }
        return list;
    }

    // ══════════════════════════════════════════════════════════════════
    // 审核证据：写入前清洗 + 按逻辑条款合并（★ 2026-11-25）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>模型写进证据正文的「置信度」（提示词已禁止，此处兜底剥除）。</summary>
    private static readonly Regex EvidenceConfidenceRx =
        new(@"[（(][^）)]{0,8}置信度[^）)]{0,12}[)）]", RegexOptions.Compiled);

    /// <summary>元数据缺失括号注（如「（未标注版本与发布日期）」）—— 提示词要求直接省略，此处兜底剥除。</summary>
    private static readonly Regex EvidenceMissingMetaRx =
        new(@"[（(](?:未标注|未注明|未列明|无编号|无日期|无版本|缺失|未附|未提供|不详|未知|未填写|空白)[^）)]{0,24}[)）]", RegexOptions.Compiled);

    /// <summary>内部发现编号引用（「见 F1」「参见 F4/A1」「(F4)」）。</summary>
    private static readonly Regex EvidenceRefCodeRx =
        new(@"(?:[（(]?\s*(?:见|参见|详见)\s*[FA]\d+(?:\s*[/、,和]\s*[FA]\d+)*\s*[)）]?)|(?:[（(]\s*[FA]\d+(?:\s*[/、,和]\s*[FA]\d+)*\s*[)）])", RegexOptions.Compiled);

    /// <summary>系统/测试痕迹词（召回、语义召回、定位片段、章节候选、模型评分）—— 括号内出现即整体剥除。</summary>
    private static readonly Regex EvidenceTestTermRx =
        new(@"[（(][^）)]{0,12}(?:语义召回|召回|定位片段|章节候选|模型评分|测试语义)[^）)]{0,12}[)）]", RegexOptions.Compiled);

    /// <summary>
    /// 「查无此物」类否定词（提示词要求只写 doubts，不写进证据正文）。
    /// ⛔ 表外一律不误伤中性事实（如「未定义具体门牌号」不在表内，原样保留）。
    /// </summary>
    private static readonly string[] EvidenceAbsenceVerbs =
    {
        "未提供", "未发现", "未附", "未见", "未开展", "未实施", "未执行", "未能提供",
        "缺乏", "无法证实", "无法证明", "无法确认", "不足以", "均未", "尚未",
        "未建立", "未形成", "未保留",
        // ★ P4：补「元数据缺失」类否定（原只有括号注 EvidenceMissingMetaRx 覆盖，句级否定剥离漏掉）
        "未标注", "未注明", "未列明", "无编号", "无版本", "无日期"
    };

    private static readonly Regex SentenceDelimRx = new(@"([。；;])", RegexOptions.Compiled);

    /// <summary>内部锚点括号注（如「（DocSection：4.1）」「（Section 5.2）」）—— 系统投影痕迹，剥除。</summary>
    private static readonly Regex EvidenceInternalAnchorRx =
        new(@"[（(]\s*(?:DocSection|Section|Anchor|片段|锚点)\s*[:：]?\s*[^）)]{0,24}[)）]", RegexOptions.Compiled);

    /// <summary>常见英文字段名 → 中文（提示词要求完全中文化，此处兜底）。</summary>
    private static readonly (Regex Rx, string Repl)[] EvidenceEnFields =
    {
        (new Regex(@"\bOwner\b", RegexOptions.Compiled), "责任人"),
        (new Regex(@"\bImpact\b", RegexOptions.Compiled), "影响"),
        (new Regex(@"\bMonitor\b", RegexOptions.Compiled), "监视"),
        (new Regex(@"\bReview\b", RegexOptions.Compiled), "评审"),
        (new Regex(@"\bFrequency\b", RegexOptions.Compiled), "频次"),
    };

    /// <summary>
    /// 审核证据写入前清洗：剥除「系统痕迹」（置信度 / 召回标签 / 内部编号 / 元数据缺失注），
    /// 保证落库正文是审核员可直接使用的结论。
    /// <para>⚠️ 只剥「查无此物 / 系统内部」类痕迹，<b>不动中性事实</b>（如「厂房为老厂房，未定义具体门牌号」）。</para>
    /// </summary>
    public static string SanitizeEvidence(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = raw;
        text = EvidenceConfidenceRx.Replace(text, string.Empty);
        text = EvidenceMissingMetaRx.Replace(text, string.Empty);
        text = EvidenceTestTermRx.Replace(text, string.Empty);
        text = EvidenceRefCodeRx.Replace(text, string.Empty);
        text = EvidenceInternalAnchorRx.Replace(text, string.Empty);
        text = ApplyEnFields(text);
        text = RemoveAbsenceSentences(text);
        return Cleanup(text);
    }

    /// <summary>疑点文本清洗：剥系统痕迹 + 中文化英文字段名；⛔ 保留「未标注/缺失」类表述（疑点本就是写这个）。</summary>
    public static string SanitizeDoubtText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = raw;
        text = EvidenceConfidenceRx.Replace(text, string.Empty);
        text = EvidenceTestTermRx.Replace(text, string.Empty);
        text = EvidenceInternalAnchorRx.Replace(text, string.Empty);
        text = ApplyEnFields(text);
        return Cleanup(text);
    }

    private static string ApplyEnFields(string text)
    {
        foreach (var (rx, repl) in EvidenceEnFields)
            text = rx.Replace(text, repl);
        return text;
    }

    /// <summary>清理因剥除产生的空括号 / 多余空白 / 连续标点。</summary>
    private static string Cleanup(string text)
    {
        text = Regex.Replace(text, @"[（(]\s*[)）]", string.Empty);
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"[，、；]{2,}", "，");
        text = Regex.Replace(text, @"[ \t]+([，。；、）)])", "$1");
        text = Regex.Replace(text, @"([（(])[ \t]+", "$1");
        return text.Trim();
    }

    /// <summary>
    /// 按句剥离「查无此物」类否定句（提示词要求这类信息只写进 doubts，⛔ 不进证据正文）。
    /// <para>以「。；;」分句，句中出现 <see cref="EvidenceAbsenceVerbs"/> 任一动词 ⇒ 整句丢弃；
    /// 表外表述（如中性事实「未定义具体门牌号」）原样保留。</para>
    /// </summary>
    private static string RemoveAbsenceSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        // 捕获组 ⇒ Split 结果含分隔符，形如 [句0, 符0, 句1, 符1, ..., 尾句]
        var parts = SentenceDelimRx.Split(text);
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < parts.Length; i += 2)
        {
            var sentence = parts[i];
            var delim = i + 1 < parts.Length ? parts[i + 1] : string.Empty;
            if (ContainsAbsenceVerb(sentence)) continue;
            sb.Append(sentence).Append(delim);
        }
        return sb.ToString();
    }

    private static bool ContainsAbsenceVerb(string sentence)
    {
        if (sentence.Length == 0) return false;
        foreach (var v in EvidenceAbsenceVerbs)
            if (sentence.Contains(v, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>归一化条款号 → 该条款证据（同名多条取最长的一段），值已清洗。</summary>
    private static Dictionary<string, string> BuildClauseEvidenceMap(CertCheckConclusionDto? conclusion)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (conclusion == null) return map;
        foreach (var ce in conclusion.ClauseEvidences)
        {
            var key = ClauseRefs.NormalizeNumber(ce.ClauseNumber);
            if (key.Length == 0) continue;
            var clean = SanitizeEvidence(ce.AuditEvidence);
            if (clean.Length == 0) continue;
            if (!map.TryGetValue(key, out var old) || clean.Length > old.Length)
                map[key] = clean;
        }
        return map;
    }

    /// <summary>
    /// 取某结果行的审核证据（★ 2026-11-25 合并口径）：
    /// <list type="number">
    /// <item>把本条款号归入「同族条款」——父子条款（4 / 4.1）互为前缀 + <c>.</c>；跨标准同号（3 体系的 4.1）归一化后同键；</item>
    /// <item>按「最细条款号优先」取合并后的证据；</item>
    /// <item>同族都无证据时，回退整体 <c>audit_evidence</c> → <c>summary</c>。</item>
    /// </list>
    /// </summary>
    private static string ResolveEvidenceForClause(
        CertCheckConclusionDto? conclusion,
        string? clauseNumber,
        List<(string ClauseCode, string? ClauseNumber)> clausePairs,
        Dictionary<string, string> evidenceMap)
    {
        if (conclusion == null) return string.Empty;
        var norm = ClauseRefs.NormalizeNumber(clauseNumber);

        // ① 同族候选（含自身），按条款号长度降序 = 最细优先
        var candidates = new List<string>();
        if (norm.Length > 0) candidates.Add(norm);
        foreach (var (_, n) in clausePairs)
        {
            var m = ClauseRefs.NormalizeNumber(n);
            if (m.Length == 0 || m == norm) continue;
            if (m.StartsWith(norm + ".", StringComparison.Ordinal)
                || norm.StartsWith(m + ".", StringComparison.Ordinal))
                candidates.Add(m);
        }
        foreach (var key in candidates.OrderByDescending(x => x.Length))
            if (evidenceMap.TryGetValue(key, out var merged) && merged.Length > 0)
                return merged;

        // ② 回退整体证据 / 概括
        var fallback = !string.IsNullOrWhiteSpace(conclusion.AuditEvidence)
            ? conclusion.AuditEvidence
            : conclusion.Summary ?? string.Empty;
        return SanitizeEvidence(fallback);
    }

    /// <summary>
    /// 计算「系统建议结论」（⛔ 不是最终结论）。
    /// <para>资料完整性尚未验证时，不根据空疑点自动建议合格。</para>
    /// <list type="bullet">
    ///   <item>无疑点 ⇒ 不生成建议结论；</item>
    ///   <item>有疑点 ⇒ 按模型结论映射；模型若说合格 / 不适用，降级为 <c>ofi</c>
    ///         （有疑点就不该给「合格」的建议，但也不替审核员判不合格）。</item>
    /// </list>
    /// </summary>
    private static string? ResolveSuggestedConclusion(CertCheckConclusionDto? conclusion)
    {
        if (conclusion == null) return null;

        if (conclusion.Doubts.Count == 0)
            return null;

        var mapped = CertVerdictMapper.ToConclusion(conclusion.Conclusion);
        return mapped == CertVerdictMapper.ConclusionPass || mapped == CertVerdictMapper.ConclusionNa
            ? CertVerdictMapper.ConclusionOfi
            : mapped ?? CertVerdictMapper.ConclusionOfi;
    }

    private static string? GetStr(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>
    /// 解析 <c>doubts</c>（疑点分析，可多条）。
    /// <para>兼容两种写法：<c>[{"point":"...","detail":"..."}]</c> 与 <c>["..."]</c>；
    /// 空数组 / 缺失目前均返回空列表，不据此推断合格。</para>
    /// </summary>
    private static List<CertDoubtDto> ParseDoubts(JsonElement root)
    {
        var list = new List<CertDoubtDto>();
        if (!root.TryGetProperty("doubts", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    list.Add(new CertDoubtDto { Point = text!.Trim(), Detail = "" });
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object) continue;

            var point = item.TryGetProperty("point", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? ""
                : "";
            var detail = item.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? ""
                : "";

            // 只有 detail 没有 point 时，用 detail 首句当标题，避免界面上出现空标题
            if (string.IsNullOrWhiteSpace(point) && !string.IsNullOrWhiteSpace(detail))
                point = detail.Length > 40 ? detail[..40] : detail;

            if (string.IsNullOrWhiteSpace(point) && string.IsNullOrWhiteSpace(detail)) continue;
            list.Add(new CertDoubtDto { Point = point.Trim(), Detail = detail.Trim() });
        }
        return list;
    }

    /// <summary>把 JSON 数组属性序列化回字符串（落 <c>MissingKindsJson</c> / <c>ConflictsJson</c>）；非数组返回 null</summary>
    private static string? GetJsonArrayText(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Array ? v.GetRawText() : null;
    }

    // ================================================================
    // 三·补、入场检查内容生成（认证准备「检查内容」）
    // ================================================================

    /// <summary>
    /// 为检查项生成「入场检查内容」（查什么 / 怎么查，可多条）。
    /// <para>★ 用户 2026-11-03：认证准备阶段多一个「检查内容」，可由系统自动生成，审核员可再编辑。</para>
    /// <para>只会替换 <c>Source=ai</c> 的旧行，人工维护的行保留。</para>
    /// </summary>
    public async Task<Result<List<CertOnsitePlanItem>>> GenerateOnsitePlanAsync(
        string userCode, string checkItemCode)
    {
        if (string.IsNullOrWhiteSpace(checkItemCode))
            return Result<List<CertOnsitePlanItem>>.Fail("检查项 Code 不能为空");

        var detailResult = await _db.GetOneAsync<CertAuditPlanDetail>(d =>
            d.Code == checkItemCode && !d.IsDeleted);
        var detail = detailResult.Data;
        if (detail == null)
            return Result<List<CertOnsitePlanItem>>.Fail("检查项不存在");

        var planResult = await _db.GetOneAsync<CertAuditPlan>(p =>
            p.Code == detail.PlanCode && !p.IsDeleted);
        var plan = planResult.Data;
        if (plan == null)
            return Result<List<CertOnsitePlanItem>>.Fail("认证计划不存在");

        var clauseCodes = SplitClauses(detail.Clauses, detail.ClauseNumbers)
            .Select(c => c.ClauseCode).ToList();
        var detailRefs = ClauseRefs.Parse(detail.ClauseRefsJson);
        if (clauseCodes.Count == 0 && detailRefs.Count == 0)
            return Result<List<CertOnsitePlanItem>>.Fail("该检查项未配置条款，无法生成检查内容");

        var promptType = PromptTypes.OnsitePlan;
        var standardCode = await ResolveStandardCodeAsync(detail.PlanCode);
        var promptView = await _promptWb.ResolveActiveAsync(promptType, standardCode);
        if (promptView?.Template == null)
            return Result<List<CertOnsitePlanItem>>.Fail("未找到入场检查内容提示词模板（onsite_plan）");

        var context = await _ctxBuilder.BuildAsync(
            clauseCodes, plan.EnterpriseCode, new List<CertEvidence>(),
            detailRefs.Count > 0 ? detailRefs : null, detail.DeptNames);
        if (context.Clauses.Count == 0)
            return Result<List<CertOnsitePlanItem>>.Fail(
                $"条款无法完整装配，请重新选择或修复条款：{string.Join("；", context.Warnings)}");
        var prompt = BuildPrompt(promptView.Template, StagePrep, detail, context, new List<CertEvidence>());
        if (Encoding.UTF8.GetByteCount(prompt) > 60000)
            return Result<List<CertOnsitePlanItem>>.Fail("完整分析输入超过当前 60000 字节上限，未调用模型；需要分批处理");

        var aiSettings = await GetAiSettingsAsync();
        var llmResponse = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = aiSettings.BaseUrl,
            ApiKey = aiSettings.ApiKey,
            Model = aiSettings.Model,
            Temperature = aiSettings.Temperature,
            MaxTokens = aiSettings.MaxTokens,
            SystemPrompt = "你是资深 ISO 管理体系审核员，请输出入场检查内容清单 JSON。",
            Prompt = prompt,
            DocumentContent = "",
            ForceJson = true
        });

        if (!llmResponse.Success || llmResponse.Json == null || llmResponse.JsonRepaired
            || string.Equals(llmResponse.FinishReason, "length", StringComparison.OrdinalIgnoreCase)
            || (llmResponse.CompletionTokens.HasValue && llmResponse.CompletionTokens.Value >= aiSettings.MaxTokens))
            return Result<List<CertOnsitePlanItem>>.Fail(
                $"AI 生成失败：{llmResponse.Message ?? "模型未返回有效 JSON"}");

        var items = ParseOnsitePlanItems(llmResponse.Json.RootElement);
        if (items.Count == 0)
            return Result<List<CertOnsitePlanItem>>.Fail("AI 未生成任何检查内容，请重试或手工添加");

        var clausesByNumber = context.Clauses
            .GroupBy(c => ClauseRefs.NormalizeNumber(c.ClauseNumber), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var referencedNumbers = items.SelectMany(i => (i.ClauseNumbers ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ClauseRefs.NormalizeNumber).Distinct(StringComparer.Ordinal).ToList();
        var invalidNumbers = referencedNumbers
            .Where(n => !clausesByNumber.TryGetValue(n, out var matches) || matches.Count != 1).ToList();
        if (invalidNumbers.Count > 0)
            return Result<List<CertOnsitePlanItem>>.Fail(
                $"生成的条款号不存在或对应多个标准版本，无法确定归属：{string.Join(",", invalidNumbers)}");

        using var tran = _db.BeginTransaction();
        try
        {
        var now = DateTime.Now;

        // 只清掉旧「系统生成」行，人工行保留
        var existingResult = await _db.GetListAsync<CertOnsitePlanItem>(i =>
            i.CheckItemCode == checkItemCode && !i.IsDeleted);
        var oldAiRows = (existingResult.Data ?? new List<CertOnsitePlanItem>())
            .Where(i => string.Equals(i.Source, "ai", StringComparison.OrdinalIgnoreCase))
            .Where(i => string.IsNullOrWhiteSpace(i.UpdateBy)
                && string.Equals(i.Status, "planned", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var old in oldAiRows)
        {
            old.IsDeleted = true;
            old.DeleteBy = userCode;
            old.DeleteTime = now;
            old.UpdateBy = userCode;
            old.UpdateTime = now;
            // R-A：只写软删相关列
            var updated = await _db.UpdateAsync(old,
                nameof(CertOnsitePlanItem.IsDeleted),
                nameof(CertOnsitePlanItem.DeleteBy),
                nameof(CertOnsitePlanItem.DeleteTime),
                nameof(CertOnsitePlanItem.UpdateBy),
                nameof(CertOnsitePlanItem.UpdateTime));
            if (!updated.Success) throw new InvalidOperationException(updated.Error ?? "更新旧检查项失败");
        }

        var seq = (existingResult.Data ?? new List<CertOnsitePlanItem>())
            .Where(i => !i.IsDeleted)
            .Select(i => Math.Max(i.SeqNo, i.Sort))
            .DefaultIfEmpty(0)
            .Max();

        // 条款号 → 条款 Code，便于后续按 Code 追溯
        var numberToCode = clausesByNumber.Where(g => g.Value.Count == 1)
            .ToDictionary(g => g.Key, g => g.Value[0].ClauseCode, StringComparer.Ordinal);
        var detailRefsByNumber = detailRefs
            .GroupBy(r => ClauseRefs.NormalizeNumber(r.ClauseNumber), StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);

        var created = new List<CertOnsitePlanItem>();
        foreach (var item in items)
        {
            seq++;
            var clauseNumbers = item.ClauseNumbers;
            var mappedCodes = string.IsNullOrWhiteSpace(clauseNumbers)
                ? null
                : string.Join(",",
                    clauseNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(n => numberToCode.TryGetValue(ClauseRefs.NormalizeNumber(n), out var code) ? code : null)
                        .Where(c => c != null)
                        .Distinct());

            var entity = new CertOnsitePlanItem
            {
                Code = Guid.NewGuid().ToString("N"),
                PlanCode = detail.PlanCode,
                CheckItemCode = checkItemCode,
                SeqNo = seq,
                Sort = seq,
                CheckWhat = Truncate(item.CheckWhat, 500) ?? "",
                CheckMethod = Truncate(item.CheckMethod, 50),
                TargetDept = Truncate(item.TargetDept, 200),
                SampleRequirement = Truncate(item.SampleRequirement, 200),
                ExpectedFinding = Truncate(item.ExpectedFinding, 500),
                ClauseCodes = Truncate(mappedCodes, 500),
                ClauseRefsJson = ClauseRefs.ToJson(
                    (clauseNumbers ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(n => detailRefsByNumber.GetValueOrDefault(ClauseRefs.NormalizeNumber(n)))
                        .Where(r => r != null)),
                Source = "ai",
                Status = "planned",
                CreateBy = userCode,
                CreateTime = now
            };
            var inserted = await _db.InsertAsync(entity);
            if (!inserted.Success) throw new InvalidOperationException(inserted.Error ?? "保存检查项失败");
            created.Add(entity);
        }

        tran.Commit();
        return Result<List<CertOnsitePlanItem>>.Ok(created);
        }
        catch (Exception ex)
        {
            tran.Rollback();
            _logger.LogError(ex, "更新现场检查内容失败 CheckItemCode={CheckItemCode}", checkItemCode);
            return Result<List<CertOnsitePlanItem>>.Fail($"保存现场检查内容失败：{ex.Message}");
        }
    }

    private static List<OnsitePlanItemDto> ParseOnsitePlanItems(JsonElement root)
    {
        var list = new List<OnsitePlanItemDto>();
        var arr = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items))
            arr = items;
        if (arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var what = GetStr(item, "check_what") ?? GetStr(item, "checkWhat") ?? "";
            if (string.IsNullOrWhiteSpace(what)) continue;

            list.Add(new OnsitePlanItemDto
            {
                CheckWhat = what.Trim(),
                CheckMethod = GetStr(item, "check_method") ?? GetStr(item, "checkMethod"),
                TargetDept = GetStr(item, "target_dept") ?? GetStr(item, "targetDept"),
                SampleRequirement = GetStr(item, "sample_requirement") ?? GetStr(item, "sampleRequirement"),
                ExpectedFinding = GetStr(item, "expected_finding") ?? GetStr(item, "expectedFinding"),
                ClauseNumbers = GetStr(item, "clause_numbers") ?? GetStr(item, "clauseNumbers")
            });
        }
        return list;
    }

    private class OnsitePlanItemDto
    {
        public string CheckWhat { get; set; } = "";
        public string? CheckMethod { get; set; }
        public string? TargetDept { get; set; }
        public string? SampleRequirement { get; set; }
        public string? ExpectedFinding { get; set; }
        public string? ClauseNumbers { get; set; }
    }

    // ================================================================
    // 四、辅助
    // ================================================================

    /// <summary>分支名归一：只认 <c>prep</c>，其余一律 <c>onsite</c>（保守：宁可多跑检查，不漏跑）</summary>
    private static string NormalizeStage(string? raw)
        => string.Equals(raw, StagePrep, StringComparison.OrdinalIgnoreCase) ? StagePrep : StageOnsite;

    /// <summary>字符串截断（DB 列宽保护；<c>null</c> 原样返回）</summary>
    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

    /// <summary>
    /// 把 <c>cert_audit_plan_detail.Clauses</c>（逗号串）与 <c>ClauseNumbers</c> 按位置配对。
    /// <para>⚠️ 多条款检查项必须逐条落库（F4）：旧版只取 <c>Split(',')[0]</c>，
    /// 一个检查项挂 4.2.4/4.2.5/4.2.6 时只剩 4.2.4 有结论，另外两条静默丢失。</para>
    /// </summary>
    private static List<(string ClauseCode, string? ClauseNumber)> SplitClauses(
        string? clauses, string? numbers)
    {
        var list = new List<(string, string?)>();
        if (string.IsNullOrWhiteSpace(clauses)) return list;

        var codes = clauses.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nums = string.IsNullOrWhiteSpace(numbers)
            ? Array.Empty<string>()
            : numbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < codes.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(codes[i])) continue;
            list.Add((codes[i], i < nums.Length ? nums[i] : null));
        }
        return list;
    }

    /// <summary>
    /// 把该 (检查项, 分支) 的旧当前分析置为历史（<c>IsCurrent=NULL</c>），并返回本次的 <c>RunNo</c>。
    ///
    /// <para><b>⛔ 必须用 NULL 表历史，不要用 0</b>：唯一键 <c>uk_current (CheckItemCode, StageKind, IsCurrent)</c>
    /// 依赖"MySQL 唯一索引允许多个 NULL"—— 用 0 的话 <c>(item, prep, 0)</c> 只能有一条，
    /// 重跑第二次就唯一键冲突（46 号 R10）。</para>
    /// </summary>
    private async Task<bool> HasCurrentManualDecisionAsync(string checkItemCode, string stageKind)
    {
        var currentQuery = await _db.GetListAsync<CertCheckAnalysis>(a =>
            a.CheckItemCode == checkItemCode && a.StageKind == stageKind
            && a.IsCurrent == 1 && !a.IsDeleted);
        if (!currentQuery.Success) throw new InvalidOperationException(currentQuery.Error ?? "查询当前分析失败");
        var current = currentQuery.Data ?? new List<CertCheckAnalysis>();
        var codes = current.Select(a => a.Code).ToList();
        if (codes.Count == 0) return false;
        var resultsQuery = await _db.GetListAsync<CertCheckResult>(r =>
            codes.Contains(r.AnalysisCode) && !r.IsDeleted);
        if (!resultsQuery.Success) throw new InvalidOperationException(resultsQuery.Error ?? "查询人工判定失败");
        var results = resultsQuery.Data ?? new List<CertCheckResult>();
        return results.Any(r => !string.IsNullOrWhiteSpace(r.ConclusionBy)
            || (!string.IsNullOrWhiteSpace(r.UpdateBy) && !string.IsNullOrWhiteSpace(r.Reason)));
    }

    private async Task<int> DemoteCurrentAnalysisAsync(
        string checkItemCode, string stageKind, string userCode)
    {
        var rows = (await _db.GetListAsync<CertCheckAnalysis>(a =>
            a.CheckItemCode == checkItemCode
            && a.StageKind == stageKind
            && !a.IsDeleted)).Data ?? new List<CertCheckAnalysis>();

        var runNo = rows.Count == 0 ? 1 : rows.Max(r => r.RunNo) + 1;

        foreach (var old in rows.Where(r => r.IsCurrent == 1))
        {
            old.IsCurrent = null;               // ⛔ 不要写 0
            old.UpdateBy = userCode;
            old.UpdateTime = DateTime.Now;
            var updated = await _db.UpdateAsync(old,
                nameof(old.IsCurrent), nameof(old.UpdateBy), nameof(old.UpdateTime));
            if (!updated.Success) throw new InvalidOperationException(updated.Error ?? "更新历史分析失败");
        }

        return runNo;
    }

    /// <summary>
    /// 失败也留一条分析行 —— 否则"跑过但失败"在界面上完全看不见（只有任务项的 failed 状态）。
    /// <para><c>IsCurrent</c> 保持 <c>NULL</c>：失败的分析不是"当前生效"，
    /// 上一次成功的结论仍然是当前结论。</para>
    /// </summary>
    private async Task InsertFailedAnalysisAsync(
        string userCode, CertTaskItem item, CertTask? task, string stageKind,
        string promptType, PromptTemplateView promptView, string fingerprint, string error)
    {
        try
        {
            var rows = (await _db.GetListAsync<CertCheckAnalysis>(a =>
                a.CheckItemCode == item.CheckItemCode
                && a.StageKind == stageKind
                && !a.IsDeleted)).Data ?? new List<CertCheckAnalysis>();

            await _db.InsertAsync(new CertCheckAnalysis
            {
                Code = Guid.NewGuid().ToString("N"),
                CheckItemCode = item.CheckItemCode,
                StageKind = stageKind,
                EnterpriseCode = task?.EnterpriseCode,
                PlanCode = task?.PlanCode,
                TaskCode = item.TaskCode,
                TaskItemCode = item.Code,
                IsCurrent = null,
                RunNo = rows.Count == 0 ? 1 : rows.Max(r => r.RunNo) + 1,
                Status = "failed",
                PromptType = promptType,
                PromptCode = promptView.Code,
                PromptVersion = promptView.Version.ToString(),
                EvidenceFingerprint = fingerprint,
                ErrorMessage = error,
                CreateBy = userCode,
                CreateTime = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CertCheck] 写失败分析行出错 item={Item}", item.CheckItemCode);
        }
    }

    /// <summary>
    /// 写证据链快照（这次用了哪几条证据、当时内容是什么）。
    /// <para>为什么必须有快照：证据是资产、会被改（<c>PrevContentMD5</c> 机制就是干这个的）。
    /// 报告三个月后要能回答「当时凭什么判的」—— 只存 EvidenceCode 不够。</para>
    /// </summary>
    private async Task WriteEvidenceLinksAsync(
        string analysisCode, List<CertEvidence> evidences, string userCode, DateTime now)
    {
        var order = 0;
        foreach (var ev in evidences.GroupBy(e => e.Code).Select(g => g.First()))
        {
            order++;
            var dropped = string.Equals(ev.Status, "dropped", StringComparison.OrdinalIgnoreCase);

            var inserted = await _db.InsertAsync(new CertCheckAnalysisEvidence
            {
                Code = Guid.NewGuid().ToString("N"),
                AnalysisCode = analysisCode,
                EvidenceCode = ev.Code,
                UsedInPrompt = !dropped,   // Status=dropped 的不喂模型，但保留关联（留痕"当时有哪些但没用"）
                SnippetSnapshot = Truncate(ev.LlmSummary ?? ev.Snippet, 2000),
                ContentMD5Snapshot = ev.ContentMD5,
                TokenEstimate = ev.TokenEstimate,
                OrderNo = order,
                CreateBy = userCode,
                CreateTime = now
            });
            if (!inserted.Success)
                throw new InvalidOperationException(inserted.Error ?? "保存证据链失败");
        }
    }

    /// <summary>
    /// 取计划的<b>标准 Code（GUID）</b>，用于提示词按标准级 → 平台级逐层回退。
    ///
    /// <para><b>★ 2026-11-02 修复</b>：旧实现返回的是 <c>plan.EnterpriseCode</c>（企业 Code），
    /// 拿企业 Code 去匹配 <c>wf_prompt_template.StandardCode</c> 永远不命中 ⇒
    /// 每一跑都静默回退到平台级提示词，标准专属提示词（若将来配了）形同不存在。</para>
    ///
    /// <para>多标准计划取第一个：<b>条款级标准</b>由装配上下文逐条给出（<c>ClauseContext.StandardCode</c>），
    /// 落结果行时优先用它 —— 一个计划挂两个标准时，条款 4.1 属于哪个标准不能靠「计划里的第一个」猜。</para>
    /// </summary>
    private async Task<string?> ResolveStandardCodeAsync(string planCode)
    {
        var plan = await _db.GetOneAsync<CertAuditPlan>(p => p.Code == planCode && !p.IsDeleted);
        var standards = plan.Data?.Standards;
        if (string.IsNullOrWhiteSpace(standards)) return null;

        return standards
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private async Task LogAnalysisAsync(CertAnalysisResult result,
        string? analysisCode = null, string? fingerprint = null)
    {
        try
        {
            await _db.InsertAsync(new CertAnalysis
            {
                Code = analysisCode ?? result.AnalysisCode,
                TaskItemCode = result.TaskItemCode,
                EvidenceFingerprint = fingerprint ?? "",
                LockedSkippedCount = result.LockedSkippedCount,
                EvidenceUsedCount = result.EvidenceUsedCount,
                CreateTime = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入 cert_analysis 日志失败");
        }
    }

    /// <summary>从 cert_sys_config 读取 AI 设置（Admin 端同源口径）</summary>
    private async Task<AiSettings> GetAiSettingsAsync()
    {
        const float defaultTemperature = 0.7f;
        const int defaultMaxTokens = 4096;
        const string defaultModel = "qwen-turbo";
        const string defaultBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var s = new AiSettings("", defaultBaseUrl, defaultModel, defaultMaxTokens, defaultTemperature);
        try
        {
            // 使用原生 SqlSugar 查询 cert_sys_config
            var rows = await _db.Client.Queryable<YZH.Core.Api.Models.System.SysConfig>()
                .Where(x => x.Category == "ai_model" && !x.IsDeleted)
                .Select(x => new { x.ConfigKey, x.ConfigValue })
                .ToListAsync();

            string apiKey = "", baseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", model = "qwen-turbo";
            int maxTokens = 4096;
            float temperature = 0.7f;

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

            return new AiSettings(apiKey, baseUrl, model, maxTokens, temperature);
        }
        catch
        {
            return s;
        }
    }
}
