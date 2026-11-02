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
    public string Recommendation { get; set; } = "";
    public int Confidence { get; set; } = 0;
    public List<string> EvidenceCodes { get; set; } = new();
    public string? NcSuggestion { get; set; }
    public string? SeveritySuggestion { get; set; }
    public string? ClauseCodes { get; set; }

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
            await _db.UpdateAsync(item);

            await LogAnalysisAsync(result);
            return Result<CertAnalysisResult>.Ok(result);
        }

        // ---- L2：取检查项明细 + 关联证据 ----
        var detail = (await _db.GetOneAsync<CertAuditPlanDetail>(d =>
            d.Code == item.CheckItemCode && !d.IsDeleted)).Data;

        var evidences = (await _db.GetListAsync<CertEvidence>(e =>
            e.CheckItemCode == item.CheckItemCode && !e.IsDeleted)).Data
            ?? new List<CertEvidence>();

        // ---- L2.5：装配三模型（★ 条款模型 + 文档模型 → 原始证据）----
        // 用户口径（2026-11-02）：检查项 → 多个条款 Code → 按条款编号过滤企业文档 →
        // 按条款聚合摘录 → 作为原始证据交给 LLM。一个检查项一次推理，信息量天然有界。
        var planClauseCodes = SplitClauses(detail?.Clauses, detail?.ClauseNumbers)
            .Select(c => c.ClauseCode).ToList();
        var context = await _ctxBuilder.BuildAsync(
            planClauseCodes, task?.EnterpriseCode, evidences);

        foreach (var w in context.Warnings)
            _logger.LogWarning("[CertCheck] 装配告警 item={Item} {Warn}", item.CheckItemCode, w);

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
        var prompt = BuildPrompt(promptView.Template, stageKind, detail, context, evidences);

        // ---- L5：计算证据指纹（只用于过期告警，不用于跳过）----
        var fingerprint = string.Join("|", evidences
            .Where(e => !string.IsNullOrEmpty(e.ContentMD5))
            .Select(e => e.ContentMD5!)
            .OrderBy(m => m));

        // ---- L6：调 LLM ----
        var aiSettings = await GetAiSettingsAsync();
        var llmResponse = await _llm.CompleteAsync(new LlmInvokeRequest
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

        result.LlmCalled = true;
        result.DurationMs = llmResponse.DurationMs;
        result.EvidenceUsedCount = evidences.Count;

        if (!llmResponse.Success)
        {
            item.AnalysisState = "failed";
            item.Status = "failed";
            item.Message = Truncate($"LLM 调用失败：{llmResponse.Message}", 500);
            item.UpdateBy = userCode;
            item.UpdateTime = DateTime.Now;
            await _db.UpdateAsync(item);

            // 失败也留一条分析行（否则"跑过但失败"在界面上完全看不见）
            await InsertFailedAnalysisAsync(userCode, item, task, stageKind, promptType,
                promptView, fingerprint, $"LLM 调用失败：{llmResponse.Message}");

            result.Message = $"LLM 调用失败：{llmResponse.Message}";
            await LogAnalysisAsync(result);
            return Result<CertAnalysisResult>.Fail(result.Message);
        }

        // ---- L7：解析 LLM JSON ----
        var conclusion = ParseConclusion(llmResponse);
        result.ConclusionData = conclusion;
        // 返回值里的 Conclusion 也用落库口径（onsite 的 major_nc → nc），
        // 原始值保留在 ConclusionData 里供排查
        result.Conclusion = isPrep
            ? (CertVerdictMapper.NormalizePrepState(conclusion?.PrepState) ?? "unknown")
            : (CertVerdictMapper.ToConclusion(conclusion?.Conclusion) ?? "unknown");

        // ---- L8：落库（分析行 + 逐条款结果 + 证据链）----
        // ★ 条款来源优先用装配上下文（条款号取自 cert_iso_clause，权威）；
        //   计划明细的 ClauseNumbers 是导入时的手填串，实测有 "m 1" 这类脏前缀。
        var clausePairs = context.Clauses.Count > 0
            ? context.Clauses.Select(c => (c.ClauseCode, (string?)c.ClauseNumber)).ToList()
            : SplitClauses(detail?.Clauses, detail?.ClauseNumbers);
        var now = DateTime.Now;

        using var tran = _db.BeginTransaction();
        try
        {
            // ③ 闸门：写入前再断言一次（防「入队后、执行前被锁上」的时间窗，§10.2）
            if (await _lock.IsLockedAsync(item.CheckItemCode, stageKind))
                throw new InvalidOperationException(
                    $"检查项已锁定，拒绝写入（CheckItemCode={item.CheckItemCode}, StageKind={stageKind}）");

            // 8.1 旧当前分析置 IsCurrent=NULL，算本次 RunNo
            var runNo = await DemoteCurrentAnalysisAsync(item.CheckItemCode, stageKind, userCode);
            result.RunNo = runNo;

            // 8.2 插分析行（IsCurrent=1）
            await _db.InsertAsync(new CertCheckAnalysis
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
                InputSnapshotJson = Truncate(prompt, 60000),   // 装配包快照（回放用）
                ResultSummaryJson = JsonSerializer.Serialize(new
                {
                    stage_kind = stageKind,
                    clause_count = clausePairs.Count,
                    evidence_count = evidences.Count,
                    // ★ 装配质量指标：这三项为 0 就说明「三模型没串上」，⛔ 不能只看结论
                    attr_hit_count = context.AttrHitCount,
                    doc_hit_count = context.DocHitCount,
                    excerpt_count = context.ExcerptCount,
                    assembly_warnings = context.Warnings,
                    prep_state = conclusion?.PrepState,
                    conclusion = conclusion?.Conclusion,
                    // 原始值 vs 落库值并存：排查「模型说 major_nc，库里却是 nc」时不用再猜
                    conclusion_mapped = isPrep
                        ? CertVerdictMapper.NormalizePrepState(conclusion?.PrepState)
                        : CertVerdictMapper.ToConclusion(conclusion?.Conclusion),
                    severity = isPrep
                        ? null
                        : CertVerdictMapper.ToSeverity(conclusion?.Conclusion, conclusion?.SeveritySuggestion),
                    confidence = conclusion?.Confidence
                }),
                AnalyzedAt = now,
                DurationMs = (int)llmResponse.DurationMs,
                InputTokenCount = (int)llmResponse.PromptTokens,
                OutputTokenCount = (int)llmResponse.CompletionTokens,
                CreateBy = userCode,
                CreateTime = now
            });

            // 8.3 逐条款写结果行（F4：全部条款，不是首条）
            var written = 0;
            foreach (var (clauseCode, clauseNumber) in clausePairs)
            {
                // 多标准计划：条款自己的标准优先（⛔ 不能用「计划里的第一个标准」覆盖所有条款）
                var clauseStandard = context.Clauses
                    .FirstOrDefault(c => string.Equals(c.ClauseCode, clauseCode, StringComparison.Ordinal))
                    ?.StandardCode;
                await _db.InsertAsync(new CertCheckResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    AnalysisCode = result.AnalysisCode,
                    StageKind = stageKind,
                    CheckItemCode = item.CheckItemCode,
                    ClauseCode = clauseCode,
                    ClauseNumber = clauseNumber,
                    StandardCode = string.IsNullOrWhiteSpace(clauseStandard) ? standardCode : clauseStandard,

                    // ★ 一行只填一段（§4.5.4）：prep 只写 Prep 组、onsite 只写 Check 组
                    PrepState = isPrep ? CertVerdictMapper.NormalizePrepState(conclusion?.PrepState) : null,
                    MissingKindsJson = isPrep ? conclusion?.MissingKindsJson : null,
                    ConflictsJson = isPrep ? conclusion?.ConflictsJson : null,
                    VerifyLevel = isPrep ? conclusion?.VerifyLevel : null,
                    PrepNcHint = isPrep ? conclusion?.PrepNcHint : null,

                    // ★ 词表映射（V3 §5.6）：LLM 的 conformant/observation/minor_nc/major_nc
                    //   ⛔ 不能直存，Conclusion 字段的值域是 pass/ofi/nc/na
                    Conclusion = isPrep ? null : CertVerdictMapper.ToConclusion(conclusion?.Conclusion),
                    NcSuggested = isPrep
                        ? null
                        : CertVerdictMapper.SuggestsNc(conclusion?.Conclusion, conclusion?.NcSuggestion),
                    Severity = isPrep
                        ? null
                        : CertVerdictMapper.ToSeverity(conclusion?.Conclusion, conclusion?.SeveritySuggestion),
                    SeveritySource = isPrep ? null : "ai",

                    Reason = conclusion?.Summary,
                    RollupTo = true,
                    CreateBy = userCode,
                    CreateTime = now
                });
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
            await _db.UpdateAsync(item);

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
            ? $"分析完成（{stageKind}）→ {result.Conclusion}；⚠️ 该检查项未配置条款，未写结论行"
            : $"分析完成（{stageKind}）→ {result.Conclusion}，{result.ClauseResultCount} 个条款";
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
                sb.AppendLine($"  转录：{Truncate(ev.TranscriptText, 500)}");
            if (!string.IsNullOrEmpty(ev.CapturedPlace))
                sb.AppendLine($"  地点：{ev.CapturedPlace}");
            if (!string.IsNullOrEmpty(ev.PresentPersons))
                sb.AppendLine($"  在场人员：{ev.PresentPersons}");
        }
        return sb.ToString();
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
                Recommendation = root.TryGetProperty("recommendation", out var r) ? r.GetString() ?? "" : "",
                Confidence = root.TryGetProperty("confidence", out var f) ? f.GetInt32() : 0,
                NcSuggestion = root.TryGetProperty("nc_suggestion", out var n) ? n.GetString() : null,
                SeveritySuggestion = root.TryGetProperty("severity_suggestion", out var sv) ? sv.GetString() : null,
                EvidenceCodes = root.TryGetProperty("evidence_codes", out var evs) && evs.ValueKind == JsonValueKind.Array
                    ? evs.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                    : new List<string>(),

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

    private static string? GetStr(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>把 JSON 数组属性序列化回字符串（落 <c>MissingKindsJson</c> / <c>ConflictsJson</c>）；非数组返回 null</summary>
    private static string? GetJsonArrayText(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Null) return null;
        return v.ValueKind == JsonValueKind.Array ? v.GetRawText() : null;
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
            await _db.UpdateAsync(old);
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

            await _db.InsertAsync(new CertCheckAnalysisEvidence
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


