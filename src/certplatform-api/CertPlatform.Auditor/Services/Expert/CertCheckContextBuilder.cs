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

    /// <summary>取属性值（不存在或空白 ⇒ <c>null</c>）</summary>
    public string? Attr(string code)
        => Attrs.TryGetValue(code, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>该条款是否命中任何企业文档内容</summary>
    public bool HasDocContent => Slices.Count > 0;

    /// <summary>手册纲领：该条款的「事实证据提取属性」（应读章节 / 负责部门 / 前置事实）。</summary>
    public ManualIndexParser.ClauseEvidenceSource? EvidenceSource { get; set; }

    /// <summary>该条款命中的前置事实原文（公司简介 / 方针目标 / 部门职责），供证据发现定向召回。</summary>
    public string FactText { get; set; } = string.Empty;

    /// <summary>章节画像：喂给证据发现提示词的「去哪找 + 谁负责」纲领。</summary>
    public string ChapterProfile => RenderChapterProfile(EvidenceSource);

    private static string RenderChapterProfile(ManualIndexParser.ClauseEvidenceSource? src)
    {
        if (src is null)
            return "（手册无纲领映射，按条款正文做通用提取）";

        var sb = new StringBuilder();
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
                ("Role", "角色")
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
                            : status == "matched" ? "已逐字匹配转换文本" : "未经逐字核验，需核实";
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
    public async Task<CertCheckContext> BuildAsync(
        IReadOnlyList<string> clauseCodes,
        string? enterpriseCode,
        IReadOnlyList<CertEvidence>? evidences = null,
        IReadOnlyList<ClauseRef>? clauseRefs = null)
    {
        var warnings = new List<string>();
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
        if (!string.IsNullOrWhiteSpace(enterpriseCode))
            await LoadDocSlicesAsync(clauses, enterpriseCode!, warnings);
        else
            warnings.Add("企业 Code 为空 ⇒ 未装配企业文档（条款块里的资料命中全部为空）");

        // ---- 5. 审核员证据：按条款归位，未挂条款的单独留 ----
        var orphans = new List<CertEvidence>();
        foreach (var ev in evList)
        {
            var target = string.IsNullOrEmpty(ev.ClauseCode)
                ? null
                : clauses.FirstOrDefault(c => string.Equals(c.ClauseCode, ev.ClauseCode, StringComparison.Ordinal));
            if (target == null) orphans.Add(ev);
        }

        return new CertCheckContext
        {
            Clauses = clauses,
            OrphanEvidences = orphans,
            Warnings = warnings
        };
    }

    /// <summary>
    /// 取企业文档在本次条款上的原文摘录。
    /// <para>过滤链：<c>EnterpriseCode</c> → <c>StandardCode</c> → <c>ContentJson.Clauses[].ClauseNumber</c>。</para>
    /// </summary>
    private async Task LoadDocSlicesAsync(
        List<ClauseContext> clauses, string enterpriseCode, List<string> warnings)
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
            return;
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
            return;
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

        // 轴2 过程轴候选先收集、循环结束后统一分配（记录优先），见 ApplyProcessAxis
        var processAxisCandidates = new List<ProcessAxisCandidate>();

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
            var slices = ParseClauseSlices(ex.ContentJson!, numbers, warnings);
            foreach (var (clauseNumber, slice) in slices)
            {
                var target = clauses.FirstOrDefault(c =>
                    string.Equals(c.ClauseNumber, clauseNumber, StringComparison.Ordinal)
                    && string.Equals(c.StandardCode, ex.StandardCode, StringComparison.Ordinal));
                if (target == null) continue;

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
        }

        // ---- 轴2 分配：记录优先，每条条款最多 ProcessAxisCap 份 ----
        ApplyProcessAxis(clauses, processAxisCandidates);

        // ---- 6. 手册纲领：把 EvidenceSources（应读章节/负责部门/前置事实）挂到每条条款 ----
        LoadManualGuidance(clauses, extractions, warnings);

        var noHit = clauses.Where(c => !c.HasDocContent).Select(c => c.ClauseNumber).ToList();
        if (noHit.Count > 0)
            warnings.Add($"以下条款在企业文档中未提取到对应内容：{string.Join(",", noHit)}");
    }

    /// <summary>过程轴每条款最多补充的文档数（记录优先）。⛔ 不设上限时 8.1 这类多过程条款会把整库文档卷进上下文。</summary>
    private const int ProcessAxisCap = 10;

    /// <summary>过程轴候选：一份文档按 <c>ProcessArea</c> 可能被召回到多个条款，先收集再统一分配。</summary>
    private readonly record struct ProcessAxisCandidate(
        string FileCode, string FileName, string ProcessArea, string Excerpt, bool IsRecord);

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
            sink.Add(new ProcessAxisCandidate(
                ex.FileCode, fileName, processArea, excerpt,
                string.Equals(mode, "record_profile", StringComparison.OrdinalIgnoreCase)));
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
    /// </summary>
    private static void ApplyProcessAxis(List<ClauseContext> clauses, List<ProcessAxisCandidate> candidates)
    {
        if (candidates.Count == 0) return;

        // 记录优先：同过程下"实际怎么做的"比"文件怎么规定的"更能支撑/反驳结论
        var ordered = candidates.OrderByDescending(c => c.IsRecord).ToList();

        foreach (var clause in clauses)
        {
            var keywords = ProcessAreaRecall.KeywordsFor(ProcessAreaRecall.ParseKeys(clause.Attr("Process")));
            if (keywords.Count == 0) continue;

            foreach (var cand in ordered)
            {
                if (!ProcessAreaRecall.Matches(cand.ProcessArea, keywords)) continue;
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
    private List<(string ClauseNumber, ParsedSlice Slice)> ParseClauseSlices(
        string contentJson, HashSet<string> wantedNumbers, List<string> warnings)
    {
        var result = new List<(string, ParsedSlice)>();
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;

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
