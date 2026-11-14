using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Entities.Expert;
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

            var slices = ParseClauseSlices(ex.ContentJson!, numbers, warnings);
            if (slices.Count == 0) continue;

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
        }

        var noHit = clauses.Where(c => !c.HasDocContent).Select(c => c.ClauseNumber).ToList();
        if (noHit.Count > 0)
            warnings.Add($"以下条款在企业文档中未提取到对应内容：{string.Join(",", noHit)}");
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

            if (!TryGetArray(root, out var clausesEl, "Clauses", "clauses")) return result;

            foreach (var el in clausesEl.EnumerateArray())
            {
                var number = GetString(el, "ClauseNumber", "clauseNumber");
                if (string.IsNullOrWhiteSpace(number) || !wantedNumbers.Contains(number!)) continue;

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

                result.Add((number!, new ParsedSlice
                {
                    FileName = fileName,
                    Anchor = GetString(el, "Anchor", "anchor"),
                    Excerpts = excerpts,
                    ExcerptStatuses = statuses,
                    Confidence = conf
                }));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[CertCheckContext] ContentJson 解析失败（跳过该文档）");
        }
        return result;
    }

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
