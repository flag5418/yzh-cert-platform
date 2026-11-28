using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Domain.Clauses;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Storage;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Auditor.Services.Expert;

// ══════════════════════════════════════════════════════════════════════
// 装配上下文（三模型串联的载体）
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// 一条条款在本次推理中的完整上下文：<b>条款模型 + 文档模型</b>在这一层汇合。
/// </summary>
public sealed class ClauseContext
{
    /// <summary>条款 Code（<c>cert_iso_clause.Code</c>，GUID）</summary>
    public string ClauseCode { get; init; } = string.Empty;

    /// <summary>条款编号（<c>5.5.1</c>）—— 文档切片的匹配键</summary>
    public string ClauseNumber { get; init; } = string.Empty;

    /// <summary>条款标题</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>条款原文</summary>
    public string? Description { get; init; }

    /// <summary>所属标准 Code（GUID）—— 文档切片按标准隔离，⛔ 不能跨标准匹配条款号</summary>
    public string StandardCode { get; init; } = string.Empty;

    /// <summary>9 类条款属性（AttrCode → AttrValue）</summary>
    public Dictionary<string, string?> Attrs { get; init; } = new(StringComparer.Ordinal);

    /// <summary>该条款命中的企业文档切片（来自 <c>cert_document_extraction.ContentJson.Clauses[]</c>）</summary>
    public List<ClauseDocSlice> Slices { get; } = new();

    /// <summary>
    /// 该条款的召回计划（RM 轴清单，唯一判据 = <see cref="RecallPathResolver.Resolve"/>）。
    /// <para>供提示词渲染「轴计划 vs 命中」—— 让模型知道「本该从哪条轴找回、实际有没有找回」，
    /// 而不是只看到命中结果（⛔ 轴 0 命中 ≠ 证据不存在）。</para>
    /// </summary>
    public List<RecallPath> RecallPaths { get; set; } = new();

    /// <summary>取属性值（不存在或空白 ⇒ <c>null</c>）</summary>
    public string? Attr(string code)
        => Attrs.TryGetValue(code, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>该条款是否命中任何企业文档内容</summary>
    public bool HasDocContent => Slices.Count > 0;

    /// <summary>
    /// 该条款所属<b>大章节域</b>（<c>CD_ENV</c>…/<c>CD_UNKNOWN</c>）—— 批3 召回初过滤键。
    /// <para>确定性推导（唯一判据 = <see cref="ChapterDomain.ResolveClause"/>）：属性 <c>ChapterDomain</c> → <c>Role</c> → 条款号。⛔ 端内不得另写映射。</para>
    /// </summary>
    public string ChapterDomainCode => ChapterDomain.ResolveClause(
        Attr("ChapterDomain"), Attr("Role"), ClauseNumber);

    /// <summary>手册纲领：该条款的「事实证据提取属性」（应读章节 / 负责部门 / 前置事实）。</summary>
    public ManualIndexParser.ClauseEvidenceSource? EvidenceSource { get; set; }

    /// <summary>该条款命中的前置事实原文（公司简介 / 方针目标 / 部门职责），供证据发现定向召回。</summary>
    public string FactText { get; set; } = string.Empty;

    /// <summary>章节画像：喂给证据发现提示词的「大章节 + 去哪找 + 谁负责」纲领。</summary>
    public string ChapterProfile => RenderChapterProfile(EvidenceSource, ChapterDomainCode);

    private static string RenderChapterProfile(
        ManualIndexParser.ClauseEvidenceSource? src, string domainCode)
    {
        var sb = new StringBuilder();
        // ★ 大章节置顶：2 段式召回第一段 =「大章节 ∧ 部门」初筛，本条条款的大章节必须先让模型看到，
        //   ⛔ 不能只给手册纲领（后者依赖手册提取，缺失时模型无从得知本条归属哪一大章节）。
        sb.AppendLine($"大章节：{domainCode}（{ChapterDomain.Name(domainCode)}）");

        if (src is null)
        {
            sb.Append("（手册无纲领映射，按条款正文做通用提取）");
            return sb.ToString();
        }

        sb.AppendLine($"应读章节：{string.Join("、", src.Sections)}");
        if (src.PrimaryDepts.Count > 0)
            sb.AppendLine($"主要责任部门：{string.Join("、", src.PrimaryDepts)}");
        if (src.RelatedDepts.Count > 0)
            sb.AppendLine($"相关部门：{string.Join("、", src.RelatedDepts)}");
        if (src.FactSources.Count > 0)
            sb.AppendLine($"前置事实：{string.Join("、", src.FactSources)}");
        if (src.IsFallback)
            sb.AppendLine("（手册无映射，走条款通用提取属性）");
        return sb.ToString();
    }
}

/// <summary>一条「某份文档在某条款上的原文摘录」</summary>
public sealed class ClauseDocSlice
{
    public string FileCode { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? Anchor { get; init; }
    public List<string> Excerpts { get; init; } = new();
    public List<string> ExcerptStatuses { get; init; } = new();
    public decimal? Confidence { get; init; }
}

/// <summary>
/// 一次检查项推理的装配结果。
/// <para><b>为什么单独成类</b>：装配逻辑（取哪些数、怎么裁剪、按什么顺序拼）是本项目的核心资产，
/// ⛔ 不能散落在引擎的字符串拼接里 —— 否则「改一个提示词槽位」要翻三个方法。</para>
/// </summary>
public sealed class CertCheckContext
{
    public List<ClauseContext> Clauses { get; init; } = new();

    /// <summary>审核员补充的、未挂条款的证据（单独成块，⛔ 不能因为没挂条款就丢）</summary>
    public List<CertEvidence> OrphanEvidences { get; init; } = new();

    /// <summary>装配过程中发现的问题（数据缺失 / 降级），写进快照与日志，⛔ 不静默</summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// 召回路径记账（改造4）：逐条款记「RM 计划轴 + 命中份数」，供快照核对「不遗漏、不虚增」。
    /// <para>格式：<c>条款号|RM计划=RM1,RM7|来源份数=N</c>。</para>
    /// </summary>
    public List<string> RecallTrace { get; init; } = new();

    /// <summary>
    /// 全库文档目录（只画像，<b>不含正文</b>）：让模型知道「企业还提交了哪些文档、本次未必都被召回」。
    /// <para>由 <c>LoadDocSlicesAsync</c> 生成，注入 <c>clause_evidence_findings</c> 的 <c>{{doc_catalog}}</c>。
    /// 用途是「知道自己漏了什么」，⛔ 不是把整库正文塞进上下文（正文仍由召回决定）。</para>
    /// </summary>
    public string DocCatalogText { get; init; } = string.Empty;

    /// <summary>命中的文档份数（去重）</summary>
    public int DocHitCount => Clauses
        .SelectMany(c => c.Slices)
        .Select(s => s.FileCode)
        .Distinct(StringComparer.Ordinal)
        .Count();

    /// <summary>摘录总条数</summary>
    public int ExcerptCount => Clauses.Sum(c => c.Slices.Sum(s => s.Excerpts.Count));

    /// <summary>条款属性命中条数（0 ⇒ 条款模型没进装配，必须告警）</summary>
    public int AttrHitCount => Clauses.Sum(c => c.Attrs.Count);

    /// <summary>
    /// 渲染 <c>{{clause_blocks}}</c>：<b>条款正文 → 命中摘录</b>，最后附输出契约。
    /// <para>顺序由程序固定，⛔ 不交给模型自己拼 —— 顺序一乱，模型的注意力分配就跟着乱。</para>
    /// </summary>
    public string RenderClauseBlocks(bool isPrep)
    {
        var sb = new StringBuilder();
        sb.Append(RenderClauseInfo());
        sb.Append(RenderDocHits());
        sb.Append(RenderOutputContract(isPrep));
        return sb.ToString();
    }

    /// <summary>
    /// 渲染「条款清单」：条款标题、原文及属性线索，不含文档命中与输出契约。
    /// <para>★ 单独拆出来是为了兼容旧占位符 <c>{{ClauseInfo}}</c> / <c>{{CheckRequirement}}</c>。</para>
    /// </summary>
    public string RenderClauseInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 条款清单");
        sb.AppendLine($"本次共 {Clauses.Count} 条条款，命中企业文档 {DocHitCount} 份、摘录 {ExcerptCount} 条。");
        sb.AppendLine();

        foreach (var c in Clauses)
        {
            sb.AppendLine($"### 条款 {c.ClauseNumber} · {c.Title}");
            sb.AppendLine();

            // ① 条款正文
            if (!string.IsNullOrWhiteSpace(c.Description))
            {
                sb.AppendLine("【条款 Markdown】以下内容由条款表生成，完整性需与原始标准核对。");
                sb.AppendLine(c.Description);
                sb.AppendLine();
            }

            var attributeLabels = new (string Code, string Label)[]
            {
                ("ClauseIntent", "条款意图"),
                ("Obligation", "义务"),
                ("Applicability", "适用性"),
                ("ApplicabilityCondition", "适用条件"),
                ("Process", "过程"),
                ("Severity", "严重度线索"),
                ("EvidenceForm", "证据形式"),
                ("EvidenceFamily", "证据类别"),
                ("Role", "角色"),
                ("ChapterDomain", "大章节")
            };
            var attributes = attributeLabels
                .Select(a => (a.Label, Value: c.Attr(a.Code)))
                .Where(a => a.Value != null)
                .ToList();
            if (attributes.Count > 0)
            {
                sb.AppendLine("【条款属性线索】以标准原文为准；属性不能直接决定适用性、证据范围或审核结论。");
                foreach (var attribute in attributes)
                    sb.AppendLine($"- {attribute.Label}：{attribute.Value}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 渲染「企业文档命中」：按条款列出从 <c>cert_document_extraction</c> 取到的原文摘录。
    /// <para>★ 单独拆出来是为了兼容旧占位符 <c>{{EvidenceList}}</c> ——
    /// 旧实现只列 <c>cert_evidence</c>（审核员手挂，实测 0 行）⇒ 模型永远看到「(无证据)」。</para>
    /// </summary>
    public string RenderDocHits()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 企业文档命中（按条款聚合的提取内容）");
        foreach (var warning in Warnings)
            sb.AppendLine($"注意：{warning}");

        if (Clauses.Count == 0)
        {
            sb.AppendLine("（无条款，无法聚合）");
            return sb.ToString();
        }

        foreach (var c in Clauses)
        {
            if (c.HasDocContent)
            {
                sb.AppendLine($"### 条款 {c.ClauseNumber} · {c.Title}（{c.Slices.Count} 份）");
                foreach (var s in c.Slices)
                {
                    var conf = s.Confidence.HasValue ? $"，置信度 {s.Confidence:0.00}" : "";
                    sb.AppendLine($"· 《{s.FileName}》{conf}");
                    if (!string.IsNullOrWhiteSpace(s.Anchor))
                        sb.AppendLine($"  位置：{Truncate(s.Anchor, 200)}");
                    for (var i = 0; i < s.Excerpts.Count; i++)
                    {
                        var status = i < s.ExcerptStatuses.Count ? s.ExcerptStatuses[i] : "unverified";
                        var label = status == "section_candidate" ? "章节原文候选，已定位转换文本，相关性需核实"
                            : status == "matched" ? "已逐字匹配转换文本"
                            : status == "interview" ? "访谈证据（面谈记录/转录，需与现场记录相互印证）"
                            : "未经逐字核验，需核实";
                        sb.AppendLine($"  摘录（{label}）：{s.Excerpts[i]}");
                    }
                }
            }
            else
            {
                // ⛔ 这句必须写清楚：模型要能区分「企业没有这份资料」和「我们没给它看」
                sb.AppendLine($"### 条款 {c.ClauseNumber} · {c.Title}");
                sb.AppendLine("（本次可用提取结果中未找到对应内容，不代表企业未提供资料）");
            }
            sb.AppendLine();
        }

        // 审核员补充的、未挂条款的证据
        if (OrphanEvidences.Count > 0)
        {
            sb.AppendLine("## 未归属具体条款的证据");
            foreach (var ev in OrphanEvidences)
            {
                sb.AppendLine($"- [{ev.Code}] {ev.SourceName ?? ev.Code}（{ev.EvidenceKind}）");
                if (!string.IsNullOrEmpty(ev.LlmSummary))
                    sb.AppendLine($"  摘要：{ev.LlmSummary}");
                if (!string.IsNullOrEmpty(ev.TranscriptText))
                    sb.AppendLine($"  转录：{ev.TranscriptText}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>渲染输出契约（prep / onsite 两套值域，⛔ 绝不能混）</summary>
    public string RenderOutputContract(bool isPrep)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 输出格式");
        sb.AppendLine("严格输出 JSON 对象（不要任何解释文字）：");
        sb.AppendLine();
        if (isPrep)
        {
            sb.AppendLine("## 判定口径（严格按此执行）");
            sb.AppendLine("- found   ：所需资料齐全，且互相不矛盾");
            sb.AppendLine("- partial ：有资料但缺关键要素，或不同文档之间互相矛盾");
            sb.AppendLine("- missing ：仅在已核实所需资料确实缺失时使用；未检索到不等于企业没有");
            sb.AppendLine("- unknown ：无法判断（资料不足以下判断）—— ⛔ 不要猜，不要当成不符合");
            sb.AppendLine("- na      ：该条款不适用于本企业/本阶段");
            sb.AppendLine("⛔ 你**不能**输出 pass / ofi / nc —— 那是现场检查阶段的事，现在还没到现场。");
            sb.AppendLine();
            sb.AppendLine("{\"prep_state\":\"found/partial/missing/unknown/na\",\"summary\":\"一句话总结\",\"missing_kinds\":[\"缺的材料名称\"],\"conflicts\":[\"文档A说X，文档B说Y\"],\"verify_level\":\"doc/remote/onsite/test\",\"prep_nc_hint\":\"给现场的建议（可为空）\",\"confidence\":0-100}");
        }
        else
        {
            sb.AppendLine("## 判定口径（严格按此执行）");
            sb.AppendLine("资料未检索到或存在疑点只表示需要审核员核实，不可单凭疑点认定不符合；输出仅为建议。");
            sb.AppendLine("- conformant ：符合");
            sb.AppendLine("- minor_nc   ：一般不符合（局部、未影响体系有效性）");
            sb.AppendLine("- major_nc   ：严重不符合（系统性缺失或影响体系有效性）");
            sb.AppendLine("- observation：观察项（未构成不符合，但需提示）");
            sb.AppendLine();
            sb.AppendLine("{\"conclusion\":\"conformant/minor_nc/major_nc/observation\",\"summary\":\"一句话总结\",\"recommendation\":\"建议\",\"confidence\":0-100,\"nc_suggestion\":\"(非符合项时填写)\",\"severity_suggestion\":\"minor/major\"}");
        }
        return sb.ToString();
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}

// ══════════════════════════════════════════════════════════════════════
// 装配器
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// <b>三模型串联的装配器</b>：条款模型（<c>cert_clause_attr</c> + <c>cert_iso_clause</c>）
/// ＋ 文档模型（<c>cert_document_extraction</c>）→ 一段可喂给 LLM 的结构化文本。
///
/// <para><b>★ 取数口径（2026-11-02 用户确认，这是本类的设计依据）</b>：</para>
/// <list type="number">
///   <item>检查项 → 多个条款 Code（<c>cert_audit_plan_detail.Clauses</c>，GUID 逗号串）；</item>
///   <item>条款 Code → 条款编号（<c>cert_iso_clause.ClauseNumber</c>），<b>编号是文档侧的匹配键</b>；</item>
///   <item>按「企业 × 标准 × 条款编号」过滤 <c>cert_document_extraction</c>，
///         从 <c>ContentJson.Clauses[]</c> 里只取<b>该条款的那几段摘录</b>（⛔ 不是整份文件）；</item>
///   <item>按条款聚合摘录 → 作为<b>原始证据</b> → 与条款属性一起交给 LLM 推理。</item>
/// </list>
///
/// <para><b>为什么必须按条款号过滤</b>：一份《管理手册》提取出来 29 条切片、1.7 万字，
/// 而一个检查项只关心其中 2–4 条。整份塞进去会让模型在无关条款里找答案 ——
/// 既贵又不准。用户原话：「一个检查项需要的信息是有限的」。</para>
///
/// <para><b>⛔ 本类不做的三件事</b>：</para>
/// <list type="bullet">
///   <item>不判断符合性（那是 LLM 的事，且结论最终由审核员拍板）；</item>
///   <item>不做条款 ↔ 文档的语义匹配（编号就是匹配键，不需要猜）；</item>
///   <item>不因「没找到内容」而拦截（没找到就写「未提取到」，让模型判 <c>missing</c>/<c>unknown</c>）。</item>
/// </list>
/// </summary>
public sealed class CertCheckContextBuilder
{
    private readonly IDbOrm _db;

    /// <summary>核心资源（条款 / 标准 / 企业）的唯一读入口 —— 铁律 L5，守卫 A-R6。</summary>
    private readonly CertPlatform.Shared.Domain.Scope.ICoreResourceQuery _core;

    private readonly ILogger<CertCheckContextBuilder> _logger;
    private readonly IObjectStorage _storage;

    public CertCheckContextBuilder(
        IDbOrm db,
        CertPlatform.Shared.Domain.Scope.ICoreResourceQuery core,
        ILogger<CertCheckContextBuilder> logger,
        IObjectStorage storage)
    {
        _db = db;
        _core = core;
        _logger = logger;
        _storage = storage;
    }

    // ================================================================
    // 一、装配
    // ================================================================

    /// <summary>
    /// 装配一个检查项的推理上下文。
    /// </summary>
    /// <param name="clauseCodes">条款 Code 列表（来自 <c>cert_audit_plan_detail.Clauses</c> 拆分）</param>
    /// <param name="enterpriseCode">企业 Code（取文档切片的过滤键；空 ⇒ 不装配文档，只装配条款）</param>
    /// <param name="evidences">审核员补充的证据（原样带过，由引擎单独成块）</param>
    /// <param name="deptNames">
    /// 检查项部门（<c>cert_audit_plan_detail.DeptNames</c>，逗号串）—— RM3 部门轴的匹配键（仅 f2 执行族）。
    /// <para>空 ⇒ 部门轴不执行，只走条款轴 / 过程轴。</para>
    /// </param>
    public async Task<CertCheckContext> BuildAsync(
        IReadOnlyList<string> clauseCodes,
        string? enterpriseCode,
        IReadOnlyList<CertEvidence>? evidences = null,
        IReadOnlyList<ClauseRef>? clauseRefs = null,
        string? deptNames = null)
    {
        var warnings = new List<string>();
        var recallTrace = new List<string>();
        var evList = evidences?.ToList() ?? new List<CertEvidence>();

        var codeList = new List<string>();
        var clauseRows = new List<ISOClause>();

        if (clauseRefs != null && clauseRefs.Count > 0)
        {
            // ★ 业务键优先：按 (标准短码, 条款号) 解析当前有效条款。
            // 标准重建后旧 Code 全部软删，只有这里能把计划拉回可用状态。
            var refs = ClauseRefs.Clean(clauseRefs);
            var hasMissing = false;
            foreach (var group in refs.GroupBy(r => (r.StandardNo, r.VersionYear)))
            {
                var rows = await _core.GetClausesByNumbersAsync(
                    group.Key.StandardNo, group.Key.VersionYear, group.Select(r => r.ClauseNumber));
                var foundNumbers = rows.Select(c => ClauseRefs.NormalizeNumber(c.ClauseNumber))
                    .ToHashSet(StringComparer.Ordinal);
                var missing = group.Select(r => ClauseRefs.NormalizeNumber(r.ClauseNumber))
                    .Where(n => !foundNumbers.Contains(n)).ToList();
                if (missing.Count > 0)
                {
                    warnings.Add($"标准【{group.Key.StandardNo}】版本【{group.Key.VersionYear?.ToString() ?? "未指定"}】条款未找到或版本不明确：{string.Join(",", missing)}");
                    hasMissing = true;
                }
                clauseRows.AddRange(rows);
            }
            if (hasMissing)
                return new CertCheckContext { Warnings = warnings };
            clauseRows = clauseRows
                .Where(c => !string.IsNullOrEmpty(c.Code))
                .GroupBy(c => c.Code, StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(c => c.Sort)
                .ToList();
            codeList = clauseRows.Select(c => c.Code!).ToList();

            if (clauseRows.Count == 0)
            {
                warnings.Add($"条款业务键在 cert_iso_clause 中全部查无此行：{ClauseRefs.ToJson(refs)}");
                return new CertCheckContext { Warnings = warnings };
            }
        }
        else
        {
            if (clauseCodes.Count == 0)
            {
                warnings.Add("检查项未配置条款（cert_audit_plan_detail.Clauses 为空）⇒ 无法装配条款块");
                return new CertCheckContext
                {
                    OrphanEvidences = evList.Where(e => string.IsNullOrEmpty(e.ClauseCode)).ToList(),
                    Warnings = warnings
                };
            }

            codeList = clauseCodes.Distinct(StringComparer.Ordinal).ToList();
            clauseRows = await _core.GetClausesByCodesAsync(codeList);

            if (clauseRows.Count == 0)
            {
                warnings.Add($"条款 Code 在 cert_iso_clause 中全部查无此行：{string.Join(",", codeList)}");
                return new CertCheckContext { Warnings = warnings };
            }

            if (clauseRows.Count < codeList.Count)
            {
                var missing = codeList.Except(clauseRows.Select(c => c.Code), StringComparer.Ordinal);
                warnings.Add($"部分条款查无正文：{string.Join(",", missing)}");
                return new CertCheckContext { Warnings = warnings };
            }
        }

        // ---- 1.5 读取条款 Markdown 并核对当前条款 ----
        var clauseSections = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in clauseRows.GroupBy(c => c.StandardCode, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                warnings.Add("条款缺少所属标准，无法读取条款 Markdown");
                return new CertCheckContext { Warnings = warnings };
            }
            try
            {
                var standard = await _core.GetStandardByCodeAsync(group.Key);
                if (standard == null)
                    throw new InvalidOperationException("所属标准不存在或已停用");
                var markdown = await ClauseMarkdownInput.LoadAsync(_storage, standard);
                foreach (var clause in group)
                    clauseSections.Add(clause.Code!, ClauseMarkdownInput.ReadSection(markdown, clause));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add($"标准【{group.Key}】条款 Markdown 不可用：{ex.Message}");
                return new CertCheckContext { Warnings = warnings };
            }
        }

        // ---- 无正文条款不能参与分析 ----
        // 真实审核报告只列有要求的条款（如 4.1），不列只有章标题的 4。
        // 父级条款 Description 为空，不能生成条款结果，也不能让模型拿它做判定。
        var emptyClauses = clauseRows
            .Where(c => string.IsNullOrWhiteSpace(c.Description))
            .Select(c => c.ClauseNumber)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (emptyClauses.Count > 0)
        {
            warnings.Add($"所选条款缺少正文，无法完整分析：{string.Join(",", emptyClauses)}");
            return new CertCheckContext { Warnings = warnings };
        }
        if (clauseRows.Count == 0)
        {
            warnings.Add("检查项条款均为无正文的父级标题 ⇒ 无法装配条款块");
            return new CertCheckContext { Warnings = warnings };
        }

        // ---- 2. 条款模型：9 类属性（cert_clause_attr，长表 → 按条款聚合）----
        var currentClauseCodes = clauseRows
            .Select(c => c.Code)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var attrRows = new List<ClauseAttrRow>();
        if (currentClauseCodes.Count > 0)
        {
            attrRows = (await _db.GetListAsync<ClauseAttrRow>(a =>
                currentClauseCodes.Contains(a.ClauseCode) && !a.IsDeleted && a.IsValid == 1)).Data
                ?? new List<ClauseAttrRow>();
        }

        var attrsByClause = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
        foreach (var c in clauseRows)
        {
            if (string.IsNullOrEmpty(c.Code)) continue;
            var groups = attrRows.Where(a => string.Equals(a.ClauseCode, c.Code, StringComparison.Ordinal))
                .GroupBy(a => a.AttrCode, StringComparer.Ordinal).ToList();
            var duplicates = groups.Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
                warnings.Add($"条款【{c.ClauseNumber}】存在重复属性，已忽略：{string.Join(",", duplicates)}");
            attrsByClause[c.Code] = groups.Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single().AttrValue, StringComparer.Ordinal);
            if (groups.Count == 0)
                warnings.Add($"条款【{c.ClauseNumber}】未配置有效属性");
        }

        if (attrRows.Count == 0)
        {
            // ⛔ 这条必须显式告警：没有属性的装配等于「只给模型一个条款号让它猜」
            warnings.Add($"条款属性缺失（cert_clause_attr 命中 0 行）：{string.Join(",", codeList)}");
            _logger.LogWarning("[CertCheckContext] 条款属性命中 0 行 clauseCodes={Codes}", string.Join(",", codeList));
        }

        // ---- 3. 组装条款上下文 ----
        var clauses = clauseRows
            .OrderBy(c => c.Sort)
            .Select(c => new ClauseContext
            {
                ClauseCode = c.Code,
                ClauseNumber = c.ClauseNumber,
                Title = c.Title,
                Description = clauseSections[c.Code!],
                StandardCode = c.StandardCode ?? string.Empty,
                Attrs = attrsByClause.TryGetValue(c.Code, out var a)
                    ? a
                    : new Dictionary<string, string?>(StringComparer.Ordinal)
            })
            .ToList();

        // ---- 4. 文档模型：按「企业 × 标准 × 条款编号」取切片 ----
        // ★ 顺带产出「全库文档目录」（只画像不含正文）⇒ 注入 clause_evidence_findings 的 {{doc_catalog}}
        var docCatalog = string.Empty;
        if (!string.IsNullOrWhiteSpace(enterpriseCode))
            docCatalog = await LoadDocSlicesAsync(clauses, enterpriseCode!, deptNames, warnings, recallTrace);
        else
            warnings.Add("企业 Code 为空 ⇒ 未装配企业文档（条款块里的资料命中全部为空）");

        // ---- 5. 审核员证据：按条款归位，未挂条款的单独留 ----
        // ★ RM6 访谈轴：条款召回计划含访谈轴（证据形态 interview）时，把访谈类证据挂进该条款块
        //   （真实报告 4.1/4.2 的举证主体正是「面谈最高管理者」）。其余证据留作「未归属证据」，
        //   ⛔ 不能因挂了条款号就静默丢弃（原实现 target != null 时既不挂条款也不入孤儿 ⇒ 证据凭空消失）。
        var recallByClauseForEvidence = ResolveClauseRecall(clauses);
        var orphans = new List<CertEvidence>();
        foreach (var ev in evList)
        {
            var target = string.IsNullOrEmpty(ev.ClauseCode)
                ? null
                : clauses.FirstOrDefault(c => string.Equals(c.ClauseCode, ev.ClauseCode, StringComparison.Ordinal));
            if (target == null)
            {
                orphans.Add(ev);
                continue;
            }

            var interviewText = new[] { ev.LlmSummary, ev.TranscriptText, ev.Snippet }
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            var hasInterviewAxis = recallByClauseForEvidence.TryGetValue(target.ClauseNumber ?? string.Empty, out var paths)
                && paths.Any(p => string.Equals(p.Axis, RecallAxis.Interview, StringComparison.Ordinal));
            if (hasInterviewAxis
                && string.Equals(ev.EvidenceKind, "interview", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(interviewText))
            {
                target.Slices.Add(new ClauseDocSlice
                {
                    FileCode = ev.SourceCode ?? ev.Code,
                    FileName = string.IsNullOrWhiteSpace(ev.SourceName) ? "面谈记录" : ev.SourceName!,
                    Anchor = InterviewAnchor,
                    Excerpts = new List<string> { interviewText! },
                    ExcerptStatuses = new List<string> { "interview" }
                });
            }
            else
            {
                orphans.Add(ev);
            }
        }

        return new CertCheckContext
        {
            Clauses = clauses,
            OrphanEvidences = orphans,
            Warnings = warnings,
            RecallTrace = recallTrace,
            DocCatalogText = docCatalog
        };
    }

    /// <summary>
    /// 逐条款解析召回路径（<b>唯一判据 = <see cref="RecallPathResolver"/></b>，与 <c>CertTaskService.ResolveClauseRecallAsync</c> 同源）。
    /// <para>★ 2 层结构：条款属性已在装配阶段读入 <see cref="ClauseContext.Attrs"/>，此处<b>只匹配已提取字段</b>，
    /// ⛔ 不查库、⛔ 不二次解析企业文档原文。</para>
    /// <para>返回：条款号 → 路径清单（键 = <see cref="ClauseContext.ClauseNumber"/>）。</para>
    /// </summary>
    private static Dictionary<string, List<RecallPath>> ResolveClauseRecall(List<ClauseContext> clauses)
    {
        var result = new Dictionary<string, List<RecallPath>>(StringComparer.Ordinal);
        foreach (var c in clauses)
        {
            if (string.IsNullOrWhiteSpace(c.ClauseNumber)) continue;
            result[c.ClauseNumber] = RecallPathResolver.Resolve(new ClauseRecallInput
            {
                ClauseNumber = ClauseRefs.NormalizeNumber(c.ClauseNumber),
                EvidenceFamily = c.Attr("EvidenceFamily"),
                EvidenceForm = c.Attr("EvidenceForm"),
                Role = c.Attr("Role"),
                ProcessKeys = ProcessAreaRecall.ParseKeys(c.Attr("Process"))
            });
        }
        return result;
    }

    /// <summary>
    /// 取企业文档在本次条款上的原文摘录。
    /// <para>过滤链：<c>EnterpriseCode</c> → <c>StandardCode</c> → <c>ContentJson.Clauses[].ClauseNumber</c>。</para>
    /// </summary>
    /// <returns>「全库文档目录」文本（只画像不含正文）；无可列文档时返回空串。</returns>
    private async Task<string> LoadDocSlicesAsync(
        List<ClauseContext> clauses, string enterpriseCode, string? deptNames,
        List<string> warnings, List<string> recallTrace)
    {
        // 条款号 → 该编号所属标准（⛔ 4.1 在 iso9001 和 iso13485 都存在，不能只按编号匹配）
        var wanted = clauses
            .Where(c => !string.IsNullOrWhiteSpace(c.ClauseNumber) && !string.IsNullOrWhiteSpace(c.StandardCode))
            .GroupBy(c => c.StandardCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.ClauseNumber).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        if (wanted.Count == 0)
        {
            warnings.Add("条款缺少 ClauseNumber 或 StandardCode ⇒ 无法按条款号过滤文档切片");
            return string.Empty;
        }

        var standardGuids = wanted.Keys.ToList();
        var extractions = (await _db.GetListAsync<DocumentExtraction>(e =>
            e.EnterpriseCode == enterpriseCode
            && standardGuids.Contains(e.StandardCode)
            && !e.IsDeleted
            && e.IsValid == 1
            && e.ExtractStatus == "done")).Data
            ?? new List<DocumentExtraction>();

        if (extractions.Count == 0)
        {
            warnings.Add($"企业文档尚未按标准提取（cert_document_extraction 命中 0 行，enterprise={enterpriseCode}）⇒ 条款块里的资料命中为空");
            return string.Empty;
        }

        var staleCount = extractions.Count(e => e.IsStale);
        if (staleCount > 0)
            warnings.Add($"{staleCount} 份文档的提取产物已过期（IsStale=1），内容可能与企业当前文件不一致");

        var fileCodes = extractions.Select(e => e.FileCode).Distinct().ToList();
        var files = (await _db.GetListAsync<EnterpriseDoc>(d =>
            fileCodes.Contains(d.Code) && !d.IsDeleted && d.IsValid == 1)).Data
            ?? new List<EnterpriseDoc>();
        var currentFiles = files.GroupBy(d => d.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // ★ 两段式召回第一段判据来源：文档准入画像（大章节域 + 责任部门 + 语义骨架）。
        var docGates = BuildDocGates(extractions, currentFiles);

        // 轴2 过程轴候选先收集、循环结束后统一分配（记录优先），见 ApplyProcessAxis
        var processAxisCandidates = new List<ProcessAxisCandidate>();
        // 轴3 部门轴候选（RM3）同样先收集再统一分配，见 ApplyDeptAxis
        var deptAxisCandidates = new List<DeptAxisCandidate>();

        // ★ 全库文档目录（只画像不含正文）：让模型知道「企业还提交了哪些文档、本次未必都被召回」。
        var catalog = new List<string>();

        // ★ 改造1（19 号方案）：逐条款解析召回路径（唯一判据 = RecallPathResolver，全仓唯一）。
        //   条款号 → 路径清单；RM1 体裁白名单从中派生（该条款召回不含条款轴 ⇒ 不做体裁过滤，由 RM2/RM3 承担）。
        var recallByClause = ResolveClauseRecall(clauses);

        /// 取某条款的 RM1 体裁白名单（无 RM1 或无 DocTypes ⇒ null ⇒ 不过滤）
        HashSet<string>? WhitelistOf(string clauseNumber)
            => recallByClause.TryGetValue(clauseNumber, out var ps)
                ? ps.Where(p => string.Equals(p.Axis, RecallAxis.Clause, StringComparison.Ordinal))
                    .SelectMany(p => p.DocTypes)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null;

        foreach (var ex in extractions)
        {
            if (ex.IsStale) continue;
            if (!currentFiles.TryGetValue(ex.FileCode, out var matchingFiles) || matchingFiles.Count != 1
                || string.IsNullOrWhiteSpace(ex.SourceSha256)
                || !string.Equals(ex.SourceSha256, matchingFiles[0].Sha256, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"文档【{ex.FileCode}】当前文件不存在、重复或与提取来源不一致，已排除该产物");
                continue;
            }
            if (string.IsNullOrWhiteSpace(ex.ContentJson)) continue;
            if (!wanted.TryGetValue(ex.StandardCode, out var numbers)) continue;

            // ---- 轴1 条款轴：文档自身声明了条款号 ⇒ 精确挂到那条条款 ----
            var slices = ParseClauseSlices(ex.ContentJson!, numbers, warnings, out var exModeRaw);
            var exMode = EnterpriseDocExtractionRouter.NormalizeMode(exModeRaw);
            foreach (var (clauseNumber, slice) in slices)
            {
                var target = clauses.FirstOrDefault(c =>
                    string.Equals(c.ClauseNumber, clauseNumber, StringComparison.Ordinal)
                    && string.Equals(c.StandardCode, ex.StandardCode, StringComparison.Ordinal));
                if (target == null) continue;

                // ★ RM1 体裁白名单：仅当该条款的召回路径含条款轴（RM1）时过滤（15 号 §4.1）。
                //   白名单为空（该条款由 RM2/RM3 承担）⇒ 不过滤，避免把执行类证据误删。
                var allowed = WhitelistOf(target.ClauseNumber);
                if (allowed is { Count: > 0 } && !allowed.Contains(exMode))
                    continue;

                target.Slices.Add(new ClauseDocSlice
                {
                    FileCode = ex.FileCode,
                    FileName = slice.FileName,
                    Anchor = slice.Anchor,
                    Excerpts = slice.Excerpts,
                    ExcerptStatuses = slice.ExcerptStatuses,
                    Confidence = slice.Confidence
                });
            }

            // ---- 轴2 过程轴：记录/程序/作业指导书通常没有条款号，只靠条款轴会整类漏召回 ----
            // 实测 8.1「运行的策划和控制」漏掉设备台账、首件报告、制程巡检报告等实施记录，
            // 导致证据只能写"《X》规定…"。候选先收集，循环结束后统一按"记录优先"分配。
            // ⛔ 只决定"要不要召回这份文档"，条款归属由被匹配的条款决定，永不反推条款号。
            ExtractProcessAxisCandidate(ex, processAxisCandidates);
            ExtractDeptAxisCandidate(ex, deptAxisCandidates);
            AppendDocCatalogEntry(ex, currentFiles, catalog);
        }

        // ---- 轴2 分配：记录优先；仅对「召回计划含 RM2」的条款执行，且受该条款体裁白名单约束 ----
        ApplyProcessAxis(clauses, processAxisCandidates, recallByClause);

        // ---- 轴3 部门轴（RM3）：仅 f2 执行族条款；检查项部门 ∩ 文档部门，记录优先 ----
        ApplyDeptAxis(clauses, deptAxisCandidates, deptNames, recallByClause);

        // ---- 6. 手册纲领：把 EvidenceSources（应读章节/负责部门/前置事实）挂到每条条款 ----
        LoadManualGuidance(clauses, extractions, warnings);

        // ---- 6.5 两段式召回精炼：第一段「章节 + 部门」初过滤 → 第二段「条款语义」二次过滤 ----
        ApplyTwoStageRecall(clauses, docGates, deptNames, recallTrace);

        var noHit = clauses.Where(c => !c.HasDocContent).Select(c => c.ClauseNumber).ToList();
        if (noHit.Count > 0)
            warnings.Add($"以下条款在企业文档中未提取到对应内容：{string.Join(",", noHit)}");

        // ---- 7. 召回路径记账（改造4）：逐条款记「RM 计划轴 + 来源份数」，供快照核对「不遗漏、不虚增」 ----
        foreach (var c in clauses)
        {
            var paths = recallByClause.TryGetValue(c.ClauseNumber, out var ps) ? ps : new List<RecallPath>();
            // ★ 把召回计划挂到条款：提示词据此渲染「轴计划 vs 命中」（轴 0 命中 ≠ 证据不存在）。
            if (paths.Count > 0) c.RecallPaths = paths;

            var axes = paths.Count > 0
                ? string.Join(",", paths.Select(p => p.Axis))
                : "无(条款属性缺失)";
            var docCount = c.Slices.Select(s => s.FileCode).Distinct(StringComparer.Ordinal).Count();
            recallTrace.Add($"{c.ClauseNumber}|RM计划={axes}|来源份数={docCount}");
        }

        // ---- 8. 全库文档目录（只画像不含正文）：告诉模型「企业还提交了哪些文档、本次未召回」 ----
        return BuildDocCatalogText(catalog);
    }

    /// <summary>全库文档目录的总字符上限（超出则截断并注明）。目录是「知道自己漏了什么」，不是正文，几 KB 足够。</summary>
    private const int DocCatalogMaxChars = 12000;

    /// <summary>
    /// 渲染「全库文档目录」：每份文档一行画像（文件名 / 体裁 / 过程域 / 责任部门 / 已提条款），
    /// <b>不含正文</b>。目的是让模型知道「本次召回之外，企业还提交了哪些文档」，
    /// ⛔ 不得据此推断未召回文档的内容，也不得写成「企业未提供」。
    /// </summary>
    private static string BuildDocCatalogText(List<string> catalog)
    {
        if (catalog.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## 企业已提交文档目录（本次未必都被召回；只有目录，不含正文）");
        sb.AppendLine($"共 {catalog.Count} 份：");
        foreach (var line in catalog)
        {
            if (sb.Length > DocCatalogMaxChars)
            {
                sb.AppendLine("（目录过长，其余条目已省略）");
                break;
            }
            sb.AppendLine("- " + line);
        }
        sb.AppendLine("（说明：某文档未出现在上文「文档原文摘录」中 = 本次未召回，不得据此推断其内容或声称「企业未提供」。）");
        return sb.ToString();
    }

    /// <summary>
    /// 追加一条「全库文档目录」条目：只写画像（文件名 / 体裁 / 过程域 / 责任部门 / 已提条款），
    /// ⛔ 不含正文 —— 目的是让模型知道「企业还有哪些文档本次未被召回」，而不是把整库塞进上下文。
    /// </summary>
    private void AppendDocCatalogEntry(
        DocumentExtraction ex, Dictionary<string, List<EnterpriseDoc>> files, List<string> catalog)
    {
        try
        {
            using var doc = JsonDocument.Parse(ex.ContentJson!);
            var root = doc.RootElement;
            var rawMode = GetString(root, "ExtractionMode", "extractionMode") ?? "";
            var normMode = EnterpriseDocExtractionRouter.NormalizeMode(rawMode);

            var fileName = GetObject(root, "DocMeta", "docMeta") is { } meta
                ? GetString(meta, "FileName", "fileName") ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(fileName)
                && files.TryGetValue(ex.FileCode, out var f) && f.Count == 1)
                fileName = f[0].FileName;

            var parts = new List<string> { $"《{fileName}》", $"体裁={normMode}" };
            if (GetObject(root, "ModeOutput", "modeOutput") is { } output)
            {
                output = ModeOutputProjector.ForProjection(output, rawMode);
                var processArea = GetString(output, "ProcessArea", "processArea");
                if (!string.IsNullOrWhiteSpace(processArea)) parts.Add($"过程域={processArea}");
                var depts = DocDeptNames.Collect(output);
                if (depts.Count > 0) parts.Add($"责任部门={string.Join("/", depts)}");
            }
            var numbers = (ex.ClauseNumbers ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal).ToList();
            if (numbers.Count > 0) parts.Add($"条款={string.Join(",", numbers)}");

            catalog.Add(string.Join("｜", parts));
        }
        catch (JsonException)
        {
            // 产物损坏 ⇒ 目录跳过该份，不影响其它轴
        }
    }

    /// <summary>过程轴每条款最多补充的文档数（记录优先）。⛔ 不设上限时 8.1 这类多过程条款会把整库文档卷进上下文。</summary>
    private const int ProcessAxisCap = 10;

    /// <summary>访谈轴（RM6）锚点：面谈类证据（面谈最高管理者/负责人）在条款块中的定位标签。</summary>
    private const string InterviewAnchor = "interview";

    /// <summary>过程轴候选：一份文档按 <c>ProcessArea</c> 可能被召回到多个条款，先收集再统一分配。</summary>
    private readonly record struct ProcessAxisCandidate(
        string FileCode, string FileName, string ProcessArea, string Excerpt, string Mode, bool IsRecord);

    /// <summary>
    /// 解析一份文档的过程轴候选。记录 / 程序 / 作业指导书这类文档不带条款号，条款轴召不回；
    /// 只有带 <c>ModeOutput.ProcessArea</c> 的文档才产生候选，分配与限额见 <see cref="ApplyProcessAxis"/>。
    /// </summary>
    private void ExtractProcessAxisCandidate(DocumentExtraction ex, List<ProcessAxisCandidate> sink)
    {
        try
        {
            using var doc = JsonDocument.Parse(ex.ContentJson!);
            var root = doc.RootElement;
            var mode = GetString(root, "ExtractionMode", "extractionMode") ?? "";
            if (GetObject(root, "ModeOutput", "modeOutput") is not { } output) return;
            output = ModeOutputProjector.ForProjection(output, mode);
            var processArea = GetString(output, "ProcessArea", "processArea") ?? "";
            if (string.IsNullOrWhiteSpace(processArea)) return;
            var excerpt = BuildModeExcerpt(output, mode);
            if (string.IsNullOrWhiteSpace(excerpt)) return;
            var fileName = GetObject(root, "DocMeta", "docMeta") is { } meta
                ? GetString(meta, "FileName", "fileName") ?? ""
                : "";
            // ★ P2：候选携带归一后的体裁（与条款轴 axis1 同源提取器），供 ApplyProcessAxis 按该条款 RM2 白名单收敛，
            //   避免把与该条款体裁不符的文档卷进来。
            var normMode = EnterpriseDocExtractionRouter.NormalizeMode(mode);
            sink.Add(new ProcessAxisCandidate(
                ex.FileCode, fileName, processArea, excerpt, normMode,
                string.Equals(normMode, "record_profile", StringComparison.OrdinalIgnoreCase)));
        }
        catch (JsonException)
        {
            // 产物损坏 ⇒ 跳过该文档的过程轴候选，不影响条款轴
        }
    }

    /// <summary>
    /// 把过程轴候选分配到各条款：把「条款属性 <c>Process</c>」翻译成中文关键词，匹配文档 <c>ProcessArea</c>。
    /// <para><b>记录先于程序/指导书</b>（实施证据优先于制度证据），每条条款最多 <see cref="ProcessAxisCap"/> 份：
    /// 记录族挂"同过程记录"，其余挂"同过程程序/指导书"。</para>
    /// <para>⛔ 条款归属由「被匹配的条款」决定，<b>永不从文档反推条款号</b>（见 <see cref="ProcessAreaRecall"/> 硬约束）。</para>
    /// <para>★ 仅对「召回计划含 RM2（过程轴）」的条款执行；候选体裁须命中该条款 RM2 白名单（唯一判据 = <see cref="RecallPathResolver"/>）。</para>
    /// </summary>
    private static void ApplyProcessAxis(
        List<ClauseContext> clauses, List<ProcessAxisCandidate> candidates,
        Dictionary<string, List<RecallPath>> recallByClause)
    {
        if (candidates.Count == 0) return;

        // 记录优先：同过程下"实际怎么做的"比"文件怎么规定的"更能支撑/反驳结论
        var ordered = candidates.OrderByDescending(c => c.IsRecord).ToList();

        foreach (var clause in clauses)
        {
            // ★ P2 收敛：仅当该条款的召回计划含过程轴（RM2）时才执行 —— 轴由解析器给，⛔ 不自己猜。
            //   （修 R2 根因：原实现无条件执行，与 ApplyDeptAxis / 15 号 Resolve 公式矛盾。）
            if (!recallByClause.TryGetValue(clause.ClauseNumber, out var paths)) continue;
            var processPath = paths.FirstOrDefault(p =>
                string.Equals(p.Axis, RecallAxis.Process, StringComparison.Ordinal));
            if (processPath == null) continue;

            // ★ 候选体裁须命中该条款 RM2 白名单（与条款轴 axis1 同一来源 Resolve 产出，空 ⇒ 不过滤）
            var allowed = processPath.DocTypes is { Count: > 0 }
                ? new HashSet<string>(processPath.DocTypes, StringComparer.OrdinalIgnoreCase)
                : null;

            // ★ RM2 归一：条款 Process 码 ∩ 文档 ProcessArea 归一码（不再靠中文关键词包含碰运气）
            var processKeys = ProcessAreaRecall.ParseKeys(clause.Attr("Process"));
            if (processKeys.Count == 0) continue;

            foreach (var cand in ordered)
            {
                if (allowed is { Count: > 0 } && !allowed.Contains(cand.Mode)) continue;
                if (!ProcessAreaRecall.MatchesProcessAxis(cand.ProcessArea, processKeys)) continue;
                // 同一份文档对同一条款已由条款轴挂上 ⇒ 不重复
                if (clause.Slices.Any(s => string.Equals(s.FileCode, cand.FileCode, StringComparison.Ordinal)))
                    continue;
                // 本条款的过程轴补充已达上限 ⇒ 停（已按记录优先排序，先到的是记录）
                if (clause.Slices.Count(s => s.Anchor == ProcessAreaRecall.ProgramAnchor
                        || s.Anchor == ProcessAreaRecall.RecordAnchor) >= ProcessAxisCap)
                    break;

                clause.Slices.Add(new ClauseDocSlice
                {
                    FileCode = cand.FileCode,
                    FileName = cand.FileName,
                    Anchor = cand.IsRecord ? ProcessAreaRecall.RecordAnchor : ProcessAreaRecall.ProgramAnchor,
                    Excerpts = new List<string> { cand.Excerpt },
                    ExcerptStatuses = new List<string> { "matched" }
                });
            }
        }
    }

    /// <summary>部门轴（RM3）每条款最多补充的文档数（记录优先）。</summary>
    private const int DeptAxisCap = 6;

    /// <summary>部门轴候选：一份文档按其责任部门可能被召回到多个条款，先收集再统一分配。</summary>
    private readonly record struct DeptAxisCandidate(
        string FileCode, string FileName, List<string> Depts, string Excerpt, bool IsRecord);

    /// <summary>
    /// 解析一份文档的部门轴候选（RM3）：读文档自身已提取的 <c>ModeOutput.DeptNames</c>（缺则退回 <c>OwnerDept</c>）。
    /// <para>★ 2 层结构：只在文档内部取已提取字段，⛔ 不在装配阶段二次解析企业文档原文。</para>
    /// </summary>
    private void ExtractDeptAxisCandidate(DocumentExtraction ex, List<DeptAxisCandidate> sink)
    {
        try
        {
            using var doc = JsonDocument.Parse(ex.ContentJson!);
            var root = doc.RootElement;
            var mode = GetString(root, "ExtractionMode", "extractionMode") ?? "";
            if (GetObject(root, "ModeOutput", "modeOutput") is not { } output) return;
            output = ModeOutputProjector.ForProjection(output, mode);

            var depts = DocDeptNames.Collect(output);
            if (depts.Count == 0) return;

            var excerpt = BuildModeExcerpt(output, mode);
            if (string.IsNullOrWhiteSpace(excerpt)) return;
            var fileName = GetObject(root, "DocMeta", "docMeta") is { } meta
                ? GetString(meta, "FileName", "fileName") ?? ""
                : "";
            sink.Add(new DeptAxisCandidate(
                ex.FileCode, fileName, depts, excerpt,
                string.Equals(mode, "record_profile", StringComparison.OrdinalIgnoreCase)));
        }
        catch (JsonException)
        {
            // 产物损坏 ⇒ 跳过该文档的部门轴候选，不影响条款轴 / 过程轴
        }
    }

    /// <summary>
    /// 把部门轴候选分配到各条款：<b>检查项部门 ∩ 文档责任部门</b>，命中即召回。
    /// <para>仅对「召回计划含 RM3（部门轴）」的条款执行（唯一判据 = <see cref="RecallPathResolver"/>，f2 执行族）。</para>
    /// <para><b>记录先于程序 / 文件</b>（实施证据优先于制度证据），每条条款最多 <see cref="DeptAxisCap"/> 份。</para>
    /// <para>⛔ 条款归属由「被匹配的条款」决定，永不从文档反推条款号；部门名精确匹配（⛔ 不模糊，不虚增）。</para>
    /// </summary>
    private static void ApplyDeptAxis(
        List<ClauseContext> clauses, List<DeptAxisCandidate> candidates,
        string? deptNames, Dictionary<string, List<RecallPath>> recallByClause)
    {
        if (candidates.Count == 0) return;

        var wantedDepts = new HashSet<string>(StringComparer.Ordinal);
        DocDeptNames.AddText(deptNames, wantedDepts);
        if (wantedDepts.Count == 0) return;

        var ordered = candidates.OrderByDescending(c => c.IsRecord).ToList();

        foreach (var clause in clauses)
        {
            // ★ 仅当该条款的召回计划含部门轴（RM3）时才执行 —— 轴由解析器给，⛔ 不自己猜。
            if (!recallByClause.TryGetValue(clause.ClauseNumber, out var paths)
                || !paths.Any(p => string.Equals(p.Axis, RecallAxis.Dept, StringComparison.Ordinal)))
                continue;

            foreach (var cand in ordered)
            {
                if (!cand.Depts.Any(wantedDepts.Contains)) continue;
                // 同一份文档对本条款已挂过（条款轴 / 过程轴）⇒ 不重复
                if (clause.Slices.Any(s => string.Equals(s.FileCode, cand.FileCode, StringComparison.Ordinal)))
                    continue;
                // 本条款的部门轴补充已达上限 ⇒ 停（已按记录优先排序，先到的是记录）
                if (clause.Slices.Count(s => s.Anchor == DocDeptNames.DeptRecordAnchor
                                            || s.Anchor == DocDeptNames.DeptFileAnchor) >= DeptAxisCap)
                    break;

                clause.Slices.Add(new ClauseDocSlice
                {
                    FileCode = cand.FileCode,
                    FileName = cand.FileName,
                    Anchor = cand.IsRecord ? DocDeptNames.DeptRecordAnchor : DocDeptNames.DeptFileAnchor,
                    Excerpts = new List<string> { cand.Excerpt },
                    ExcerptStatuses = new List<string> { "matched" }
                });
            }
        }
    }

    /// <summary>两段式召回锚点：文档经「章节 + 部门 初筛 → 条款语义 二次筛」入池（23 号 §七/§八）。</summary>
    private const string TwoStageAnchor = "同章域语义";

    /// <summary>两段式召回：每条条款最终保留的候选文档上限（对齐 23 号 §8.4「约剩 6 份」）。</summary>
    private const int TwoStageKeepCap = 6;

    /// <summary>
    /// 文档准入画像 —— 两段式召回的<b>第一段（章节 + 部门）判据来源</b>。
    /// <para>大章节域取文档画像 <c>DocProfileJson.ChapterDomains</c>（批3 产出）；
    /// 责任部门优先取画像 <c>DeptCodes</c>、缺失退回 <c>ModeOutput.DeptNames/OwnerDept</c>；
    /// 语义骨架优先取画像 <c>Abstract</c>、缺失退回 <c>ModeOutput</c> 摘录。</para>
    /// </summary>
    private readonly record struct DocGate(
        string FileCode, string FileName, string StandardCode, string Mode,
        List<string> ChapterDomains, List<string> DeptCodes,
        string SemanticText, string Excerpt, bool IsRecord);

    /// <summary>
    /// 构建每份文档的「准入画像」（两段式召回第一段判据来源）。
    /// <para>★ 只读<b>已落库的画像与已提取字段</b>：⛔ 不在装配阶段跑语义、⛔ 不查库、⛔ 不二次解析企业文档原文。</para>
    /// </summary>
    private static List<DocGate> BuildDocGates(
        List<DocumentExtraction> extractions, Dictionary<string, List<EnterpriseDoc>> files)
    {
        var gates = new List<DocGate>();
        foreach (var ex in extractions)
        {
            if (ex.IsStale || string.IsNullOrWhiteSpace(ex.ContentJson)) continue;
            if (!files.TryGetValue(ex.FileCode, out var f) || f.Count != 1) continue;
            var doc = f[0];
            // 与主循环同源判据：产物指纹须与当前文件一致（否则该产物已过期，不参与召回）
            if (string.IsNullOrWhiteSpace(ex.SourceSha256)
                || !string.Equals(ex.SourceSha256, doc.Sha256, StringComparison.OrdinalIgnoreCase))
                continue;

            // ---- 大章节域 / 责任部门 / 语义骨架 / 体裁：优先取画像 DocProfileJson ----
            var domains = new List<string>();
            var deptCodes = new List<string>();
            var abstractText = string.Empty;
            var profileMode = string.Empty;
            if (!string.IsNullOrWhiteSpace(doc.DocProfileJson))
            {
                try
                {
                    using var pdoc = JsonDocument.Parse(doc.DocProfileJson);
                    var root = pdoc.RootElement;
                    if (TryGetArray(root, out var darr2, "ChapterDomains", "chapterDomains"))
                        foreach (var it in darr2.EnumerateArray())
                        {
                            if (it.ValueKind != JsonValueKind.String) continue;
                            var d = ChapterDomain.Normalize(it.GetString());
                            if (d != ChapterDomain.Unknown && !domains.Contains(d)) domains.Add(d);
                        }
                    if (TryGetArray(root, out var dcArr, "DeptCodes", "deptCodes"))
                        foreach (var it in dcArr.EnumerateArray())
                        {
                            if (it.ValueKind != JsonValueKind.String) continue;
                            var s = (it.GetString() ?? string.Empty).Trim();
                            if (s.Length > 0 && !deptCodes.Contains(s)) deptCodes.Add(s);
                        }
                    abstractText = GetString(root, "Abstract", "abstract") ?? string.Empty;
                    profileMode = GetString(root, "ExtractionMode", "extractionMode") ?? string.Empty;
                }
                catch (JsonException)
                {
                    // 画像损坏 ⇒ 降级只用提取产物（不影响其它文档）
                }
            }

            // ---- 体裁 / 责任部门 / 摘录：取自提取产物（画像缺失时的回退来源） ----
            var mode = profileMode;
            var excerpt = string.Empty;
            try
            {
                using var cdoc = JsonDocument.Parse(ex.ContentJson!);
                var croot = cdoc.RootElement;
                var rawMode = GetString(croot, "ExtractionMode", "extractionMode") ?? string.Empty;
                if (mode.Length == 0) mode = rawMode;
                if (GetObject(croot, "ModeOutput", "modeOutput") is { } output)
                {
                    output = ModeOutputProjector.ForProjection(output, rawMode);
                    if (deptCodes.Count == 0) deptCodes = DocDeptNames.Collect(output);
                    excerpt = BuildModeExcerpt(output, rawMode);
                }
            }
            catch (JsonException)
            {
                // 产物损坏 ⇒ 只靠画像（不影响其它文档）
            }

            var normMode = EnterpriseDocExtractionRouter.NormalizeMode(mode);
            var semantic = abstractText.Length > 0 ? abstractText : excerpt;
            if (semantic.Length == 0) continue; // 既无画像骨架又无提取摘录 ⇒ 无准入依据，不参与两段式
            var sliceExcerpt = excerpt.Length > 0 ? excerpt : abstractText;

            gates.Add(new DocGate(
                ex.FileCode, doc.FileName, ex.StandardCode, normMode,
                domains, deptCodes, doc.FileName + "\n" + semantic, sliceExcerpt,
                string.Equals(normMode, "record_profile", StringComparison.OrdinalIgnoreCase)));
        }
        return gates;
    }

    /// <summary>
    /// 两段式召回（23 号 §七 / §八）：
    /// <para><b>第一段</b>「章节 + 部门」初过滤 —— <c>doc.ChapterDomains ∩ clause.ChapterDomainCode</c>
    /// ∧（检查项部门 ∩ <c>doc.DeptCodes</c>；检查项无部门 ⇒ 不限部门）。</para>
    /// <para><b>第二段</b>「条款语义」二次过滤 —— 仅对需语义的条款（<see cref="ClauseCategoryResolver.NeedsSemantic"/>）
    /// 用 BM25（<see cref="ClauseTextMatcher"/>，RM7 唯一内核）按条款释义对上一步候选打分，保留相关者。</para>
    /// <para>★ 与 RM 各轴<b>取并集</b>（⛔ 不淘汰既有召回、⛔ 不误杀）；章域未判定（<c>CD_UNKNOWN</c>）只记账不参与。</para>
    /// </summary>
    private static void ApplyTwoStageRecall(
        List<ClauseContext> clauses, List<DocGate> gates, string? deptNames, List<string> recallTrace)
    {
        if (gates.Count == 0) return;

        var checkDepts = new HashSet<string>(StringComparer.Ordinal);
        DocDeptNames.AddText(deptNames, checkDepts);

        foreach (var clause in clauses)
        {
            var domain = clause.ChapterDomainCode;
            if (string.Equals(domain, ChapterDomain.Unknown, StringComparison.Ordinal))
            {
                recallTrace.Add($"{clause.ClauseNumber}|两段式=跳过（章域未判定）");
                continue;
            }

            // ---- 第一段：章节 + 部门 初过滤 ----
            var stage1 = gates.Where(g =>
                string.Equals(g.StandardCode, clause.StandardCode, StringComparison.Ordinal)
                && g.ChapterDomains.Contains(domain)
                && (checkDepts.Count == 0 || g.DeptCodes.Any(checkDepts.Contains))).ToList();

            if (stage1.Count == 0)
            {
                recallTrace.Add($"{clause.ClauseNumber}|两段式=章域{domain}∧部门 初筛0份");
                continue;
            }

            // ---- 第二段：条款语义二次过滤 ----
            List<DocGate> survivors;
            if (ClauseCategoryResolver.NeedsSemantic(clause.Attr("Role"), clause.ClauseNumber))
            {
                var matcher = ClauseTextMatcher.Build(stage1.Select(g => (g.FileCode, g.SemanticText)));
                var ranked = matcher.Rank(BuildClauseSemanticText(clause), TwoStageKeepCap);
                var keep = new HashSet<string>(
                    ranked.Where(r => r.Score > 0).Select(r => r.ClauseNumber), StringComparer.Ordinal);
                survivors = stage1.Where(g => keep.Contains(g.FileCode)).ToList();
            }
            else
            {
                // 不需语义的条款（框架 / 承诺）：第一段命中即保留，仅按上限截断
                survivors = stage1;
            }

            survivors = survivors.OrderByDescending(g => g.IsRecord).Take(TwoStageKeepCap).ToList();

            var added = 0;
            foreach (var g in survivors)
            {
                if (clause.Slices.Any(s => string.Equals(s.FileCode, g.FileCode, StringComparison.Ordinal)))
                    continue;
                clause.Slices.Add(new ClauseDocSlice
                {
                    FileCode = g.FileCode,
                    FileName = g.FileName,
                    Anchor = TwoStageAnchor,
                    Excerpts = new List<string> { g.Excerpt },
                    ExcerptStatuses = new List<string> { "matched" }
                });
                added++;
            }

            recallTrace.Add(
                $"{clause.ClauseNumber}|两段式=章域{domain}∧部门 初筛{stage1.Count}份→保留{survivors.Count}份|新增{added}份");
        }
    }

    /// <summary>条款释义文本（二次筛选的查询串）：标题 + 条款正文（<c>ClauseText</c> 拍平）。</summary>
    private static string BuildClauseSemanticText(ClauseContext clause)
    {
        var title = clause.Title ?? string.Empty;
        var body = ClauseTextMatcher.FlattenClauseText(clause.Description);
        return body.Length == 0 ? title : title + "\n" + body;
    }

    /// <summary>
    /// 从提取产物 <c>ContentJson.ManualIndex / EvidenceSources</c> 读出「手册纲领」，
    /// 并挂到每条条款：应读章节、负责部门、前置事实原文。零 LLM，确定性解析。
    /// </summary>
    private void LoadManualGuidance(
        List<ClauseContext> clauses, List<DocumentExtraction> extractions, List<string> warnings)
    {
        ManualIndexParser.ManualIndex? index = null;
        var sources = new List<ManualIndexParser.ClauseEvidenceSource>();

        foreach (var ex in extractions)
        {
            if (string.IsNullOrWhiteSpace(ex.ContentJson)) continue;
            try
            {
                using var doc = JsonDocument.Parse(ex.ContentJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("ManualIndex", out var mi) && mi.ValueKind == JsonValueKind.Object)
                    index ??= JsonSerializer.Deserialize<ManualIndexParser.ManualIndex>(mi.GetRawText());
                if (root.TryGetProperty("EvidenceSources", out var es) && es.ValueKind == JsonValueKind.Array)
                {
                    var parsed = JsonSerializer.Deserialize<List<ManualIndexParser.ClauseEvidenceSource>>(es.GetRawText());
                    if (parsed is { Count: > 0 }) sources.AddRange(parsed);
                }
            }
            catch (JsonException ex2)
            {
                _logger.LogWarning(ex2, "[CertCheckContext] ContentJson 的 ManualIndex/EvidenceSources 解析失败");
            }
        }

        if (sources.Count == 0)
        {
            warnings.Add("手册纲领（EvidenceSources）未解析到 ⇒ 证据发现按条款通用提取（无应读章节/负责部门/前置事实）");
            return;
        }

        foreach (var c in clauses)
        {
            var src = MatchEvidenceSource(c.ClauseNumber, sources);
            c.EvidenceSource = src;
            c.FactText = RenderFactText(src, index?.FrontMatter);
        }
    }

    /// <summary>最长前缀匹配：条款 <c>4.4.1</c> 回退到手册纲领里的 <c>4.4</c>（不跨编号段）。</summary>
    private static ManualIndexParser.ClauseEvidenceSource? MatchEvidenceSource(
        string clauseNumber, List<ManualIndexParser.ClauseEvidenceSource> sources)
    {
        if (string.IsNullOrWhiteSpace(clauseNumber)) return null;
        ManualIndexParser.ClauseEvidenceSource? best = null;
        foreach (var s in sources)
        {
            if (string.IsNullOrWhiteSpace(s.Clause)) continue;
            if (s.Clause == clauseNumber || clauseNumber.StartsWith(s.Clause + ".", StringComparison.Ordinal))
            {
                if (best is null || s.Clause.Length > best.Clause.Length) best = s;
            }
        }
        return best;
    }

    /// <summary>把命中的前置事实渲染成原文文本（公司简介 / 方针目标 / 部门职责）。</summary>
    private static string RenderFactText(
        ManualIndexParser.ClauseEvidenceSource? src, ManualIndexParser.FrontMatter? fm)
    {
        if (src is null || fm is null || src.FactSources.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("（以下为质量手册前置章节的原文事实，不是独立文件；写证据时直接引用内容，勿把【】当文件名编造）");
        if (fm.ManualMeta is { } mm)
        {
            var metaParts = new List<string> { $"编号 {mm.DocCode}" };
            if (!string.IsNullOrWhiteSpace(mm.Version)) metaParts.Add($"版本 {mm.Version}");
            if (!string.IsNullOrWhiteSpace(mm.PreparedBy)) metaParts.Add($"编制 {mm.PreparedBy}");
            if (!string.IsNullOrWhiteSpace(mm.ReviewedBy)) metaParts.Add($"审核 {mm.ReviewedBy}");
            if (!string.IsNullOrWhiteSpace(mm.ApprovedBy)) metaParts.Add($"批准 {mm.ApprovedBy}");
            if (!string.IsNullOrWhiteSpace(mm.IssuedDate)) metaParts.Add($"发布日期 {mm.IssuedDate}");
            if (!string.IsNullOrWhiteSpace(mm.EffectiveDate)) metaParts.Add($"实施日期 {mm.EffectiveDate}");
            sb.AppendLine($"【手册受控信息】{string.Join("，", metaParts)}");
        }
        foreach (var fact in src.FactSources)
        {
            switch (fact)
            {
                case "任命书" when fm.Appointment is not null:
                    sb.AppendLine("【手册·任命书】");
                    AppendField(sb, "管理者代表", fm.Appointment.ManagerRep);
                    AppendField(sb, "总经理", fm.Appointment.GeneralManager);
                    AppendField(sb, "任命日期", fm.Appointment.Date);
                    break;

                case "公司简介" when fm.Company is not null:
                    sb.AppendLine("【手册·公司简介】");
                    AppendField(sb, "公司名称", fm.Company.Name);
                    AppendField(sb, "成立日期", fm.Company.Established);
                    AppendField(sb, "法定代表人", fm.Company.LegalRepresentative);
                    AppendField(sb, "注册地址", fm.Company.RegisteredAddress);
                    AppendField(sb, "公司地址", fm.Company.Address);
                    AppendField(sb, "经营范围", fm.Company.BusinessScope);
                    break;

                case "方针目标" when fm.PolicyObjective is not null:
                    sb.AppendLine("【手册·质量方针】");
                    sb.AppendLine(fm.PolicyObjective.Policy);
                    if (fm.PolicyObjective.Objectives.Count > 0)
                    {
                        sb.AppendLine("【手册·质量目标】");
                        foreach (var o in fm.PolicyObjective.Objectives)
                            sb.AppendLine($"- {o}");
                    }
                    break;

                case "部门职责" when fm.DeptDuties.Count > 0:
                    sb.AppendLine("【手册·部门职责】");
                    foreach (var dd in fm.DeptDuties)
                    {
                        sb.AppendLine($"- {dd.Dept}：");
                        foreach (var d in dd.Duties)
                            sb.AppendLine($"  · {d}");
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    private static void AppendField(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine($"{label}：{value}");
    }

    /// <summary>解析 <c>ContentJson</c>，只挑出 <paramref name="wantedNumbers"/> 里的条款切片。</summary>
    /// <param name="mode">带出该文档的提取体裁（<c>ExtractionMode</c>，未归一），供 RM1 体裁白名单过滤。</param>
    private List<(string ClauseNumber, ParsedSlice Slice)> ParseClauseSlices(
        string contentJson, HashSet<string> wantedNumbers, List<string> warnings, out string mode)
    {
        mode = string.Empty;
        var result = new List<(string, ParsedSlice)>();
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            mode = GetString(root, "ExtractionMode", "extractionMode") ?? "";

            var fileName = GetObject(root, "DocMeta", "docMeta") is { } meta
                ? GetString(meta, "FileName", "fileName") ?? ""
                : "";

            if (!TryGetArray(root, out var clausesEl, "Clauses", "clauses"))
                return ParseModeOutputSlices(root, wantedNumbers);

            foreach (var el in clausesEl.EnumerateArray())
            {
                var number = GetString(el, "ClauseNumber", "clauseNumber");
                if (string.IsNullOrWhiteSpace(number)) continue;
                // ★ 父子回退：请求 4.4 时，提取产物里的 4.4.1 也要命中（归到 4.4）；
                //   请求 5.1 时，5.1.1 / 5.1.2 都归到 5.1。与 CertTaskService 口径一致。
                var requested = ResolveRequestedClause(number!, wantedNumbers);
                if (requested == null) continue;

                var excerpts = new List<string>();
                var statuses = new List<string>();
                if (TryGetArray(el, out var exEl, "Excerpts", "excerpts"))
                {
                    var locations = new Dictionary<int, string>();
                    if (TryGetArray(el, out var locEl, "ExcerptLocations", "excerptLocations"))
                    {
                        foreach (var location in locEl.EnumerateArray())
                        {
                            if (location.ValueKind != JsonValueKind.Object) continue;
                            if ((!location.TryGetProperty("ExcerptIndex", out var idx)
                                && !location.TryGetProperty("excerptIndex", out idx))
                                || idx.ValueKind != JsonValueKind.Number || !idx.TryGetInt32(out var position)) continue;
                            var status = GetString(location, "Status", "status") ?? "unverified";
                            var kind = GetString(location, "Kind", "kind");
                            locations[position] = status == "matched" && kind == "section_candidate"
                                ? "section_candidate" : status;
                        }
                    }
                    var index = -1;
                    foreach (var x in exEl.EnumerateArray())
                    {
                        index++;
                        if (x.ValueKind != JsonValueKind.String) continue;
                        var s = x.GetString();
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        excerpts.Add(s);
                        statuses.Add(locations.GetValueOrDefault(index, "unverified"));
                    }
                }
                if (excerpts.Count == 0) continue;

                decimal? conf = null;
                if (el.TryGetProperty("Confidence", out var cv) && cv.ValueKind == JsonValueKind.Number)
                    conf = cv.GetDecimal();

                result.Add((requested, new ParsedSlice
                {
                    FileName = fileName,
                    Anchor = GetString(el, "Anchor", "anchor"),
                    Excerpts = excerpts,
                    ExcerptStatuses = statuses,
                    Confidence = conf
                }));
            }

            // ★ 类型化提示词（2026-11-13 起）只写 ModeOutput，Clauses 可能为空。
            // 旧链路仍优先用 Clauses；为空时按 PrimaryClauses/SupportClauses + 类型摘要合成切片。
            if (result.Count == 0)
                result.AddRange(ParseModeOutputSlices(root, wantedNumbers));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[CertCheckContext] ContentJson 解析失败（跳过该文档）");
        }
        return result;
    }

    /// <summary>
    /// 把提取产物里的条款号，对应回「请求的条款号」。
    /// <para>精确命中优先；否则按父子回退：请求 4.4 ⇒ 4.4.1 命中（返回 "4.4"），
    /// 请求 5.1 ⇒ 5.1.1 / 5.1.2 命中（都归到 "5.1"）。</para>
    /// <para>与 <see cref="CertTaskService.IsClauseOrDescendantMatch"/> 口径一致：只做后代回退，不做祖先回退。</para>
    /// </summary>
    private static string? ResolveRequestedClause(string number, HashSet<string> wantedNumbers)
    {
        if (wantedNumbers.Contains(number)) return number;
        foreach (var root in wantedNumbers)
        {
            if (root.Length > 0
                && number.Length > root.Length
                && number.StartsWith(root + ".", StringComparison.Ordinal))
                return root;
        }
        return null;
    }

    /// <summary>
    /// 从 <c>ContentJson.ModeOutput</c> 合成条款切片。
    /// <para>只做“报告可用摘要”级别的投影，⛔ 不把整份文档塞回检查上下文。</para>
    /// </summary>
    private List<(string ClauseNumber, ParsedSlice Slice)> ParseModeOutputSlices(
        JsonElement root, HashSet<string> wantedNumbers)
    {
        var result = new List<(string, ParsedSlice)>();
        if (GetObject(root, "ModeOutput", "modeOutput") is not { } output) return result;

        // 存量记录产物没跑过护栏：读时校正一次，避免把模型自报条数/空表格送进模型上下文
        var mode = GetString(root, "ExtractionMode", "extractionMode") ?? "";
        output = ModeOutputProjector.ForProjection(output, mode);

        var fileName = "";
        if (GetObject(root, "DocMeta", "docMeta") is { } meta)
            fileName = GetString(meta, "FileName", "fileName") ?? "";

        var directNumbers = ReadClauseNumbers(output, "PrimaryClauses", "primaryClauses")
            .Concat(ReadClauseNumbers(output, "SupportClauses", "supportClauses"))
            .Distinct(StringComparer.Ordinal)
            .Where(wantedNumbers.Contains)
            .ToList();

        // ★ 章级召回（用户 2026-11-24）：政策/程序类文档常只有 Chapters（如 ["5","7.2"]），
        //   PrimaryClauses 不全（如岗位职责 KSY-QMS-WI-01 PrimaryClauses=["7.2"]）。
        //   若不按 Chapters 做章级匹配，第 5 章（5.2 方针/5.3 职责）永远召不回这份文档，
        //   进而误报"批准人身份未明示"。chapter "5" 覆盖 5/5.1/5.2/5.3，chapter "7.2" 覆盖 7.2/7.2.1。
        var chapterHits = new List<string>();
        foreach (var chapter in ReadClauseNumbers(output, "Chapters", "chapters"))
        {
            foreach (var wanted in wantedNumbers)
            {
                if (wanted == chapter
                    || wanted.StartsWith(chapter + ".", StringComparison.Ordinal)
                    || chapter.StartsWith(wanted + ".", StringComparison.Ordinal))
                    chapterHits.Add(wanted);
            }
        }

        var clauseNumbers = directNumbers.Concat(chapterHits).Distinct(StringComparer.Ordinal).ToList();
        if (clauseNumbers.Count == 0) return result;

        var excerpt = BuildModeExcerpt(output, mode);
        if (string.IsNullOrWhiteSpace(excerpt)) return result;

        foreach (var number in clauseNumbers)
        {
            result.Add((number, new ParsedSlice
            {
                FileName = fileName,
                Anchor = GetString(output, "ReportUse", "reportUse"),
                Excerpts = new List<string> { excerpt },
                ExcerptStatuses = new List<string> { "matched" }
            }));
        }
        return result;
    }

    private static List<string> ReadClauseNumbers(JsonElement output, params string[] names)
    {
        var result = new List<string>();
        foreach (var name in names)
        {
            if (!output.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                foreach (var part in (item.GetString() ?? "")
                    .Split(new[] { ',', '，', '、', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = part.Trim();
                    if (trimmed.Length > 0) result.Add(trimmed);
                }
            }
        }
        return result;
    }

    /// <summary>模式产物摘录：统一走 <see cref="ModeOutputProjector"/>（证据行与模型上下文同源）。</summary>
    private static string BuildModeExcerpt(JsonElement output, string mode)
        => ModeOutputProjector.Excerpt(output, mode);

    // ================================================================
    // 二、渲染
    // ================================================================

    // ================================================================
    // 三、小工具
    // ================================================================

    private sealed class ParsedSlice
    {
        public string FileName { get; init; } = string.Empty;
        public string? Anchor { get; init; }
        public List<string> Excerpts { get; init; } = new();
        public List<string> ExcerptStatuses { get; init; } = new();
        public decimal? Confidence { get; init; }
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    /// <summary>取子对象（<c>ContentJson</c> 历史上 PascalCase / camelCase 两种都出现过，⛔ 不能只认一种）</summary>
    private static JsonElement? GetObject(JsonElement el, string a, string b)
    {
        foreach (var name in new[] { a, b })
            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object)
                return v;
        return null;
    }

    /// <summary>取字符串属性（同上，双写法兼容）</summary>
    private static string? GetString(JsonElement el, string a, string b)
    {
        foreach (var name in new[] { a, b })
            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    private static bool TryGetArray(JsonElement el, out JsonElement arr, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
            {
                arr = v;
                return true;
            }
        }
        arr = default;
        return false;
    }
}
