using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Shared.Entities.Cert;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;

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

    // ── 渲染上限（与装配器一致；⛔ 改这里就是改 token 预算）──

    /// <summary>条款原文上限（最长的一条 15011 字，整段塞进去会淹掉其他所有信息）</summary>
    private const int MaxDescriptionChars = 1200;

    /// <summary><c>Artifacts</c> / <c>EvidenceChain</c> 上限（值类型是 prompt，平均 520 字）</summary>
    private const int MaxPromptAttrChars = 800;

    /// <summary>条款属性显示顺序与标签（对齐 <c>cert_clause_attr_def.Sort</c>）</summary>
    private static readonly (string Code, string Label)[] AttrOrder =
    {
        ("ClauseIntent", "管什么"),
        ("Obligation", "约束强度"),
        ("Applicability", "适用性"),
        ("ApplicabilityCondition", "适用条件"),
        ("Severity", "严重度"),
        ("DocCheckMode", "资料核对方式"),
        ("Process", "涉及过程"),
        ("Artifacts", "企业应提供的资料"),
        ("EvidenceChain", "取证要求")
    };

    /// <summary>
    /// 渲染 <c>{{clause_blocks}}</c>：<b>条款正文 → 9 类属性 → 命中摘录</b>，最后附输出契约。
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
    /// 渲染「条款清单」：条款标题 + 原文 + 9 类属性（<b>不含</b>文档命中与输出契约）。
    /// <para>★ 单独拆出来是为了兼容旧占位符 <c>{{ClauseInfo}}</c> / <c>{{CheckRequirement}}</c> ——
    /// 老的 <c>audit_check</c> 正文只认这三个名字，若只提供 <c>{{clause_blocks}}</c>，
    /// 条款属性会整块丢失（2026-11-02 实测：onsite 分支因此判「无任何证据」）。</para>
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
                sb.AppendLine("【条款原文】");
                sb.AppendLine(Truncate(c.Description!.Trim(), MaxDescriptionChars));
                sb.AppendLine();
            }

            // ② 9 类属性（有才写，⛔ 不写空行占位）
            var attrLines = new List<string>();
            foreach (var (code, label) in AttrOrder)
            {
                var v = c.Attr(code);
                if (v == null) continue;
                var text = code is "Artifacts" or "EvidenceChain"
                    ? Truncate(v, MaxPromptAttrChars)!
                    : v;
                attrLines.Add($"- 【{label}】{text}");
            }

            if (attrLines.Count > 0)
            {
                sb.AppendLine("【条款属性】");
                foreach (var line in attrLines) sb.AppendLine(line);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("【条款属性】（未配置 —— 本条无结构化要求，仅凭条款原文判断）");
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
        sb.AppendLine("## 企业文档命中（按条款聚合的原始证据）");

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
                    foreach (var ex in s.Excerpts)
                        sb.AppendLine($"  摘录：{ex}");
                }
            }
            else
            {
                // ⛔ 这句必须写清楚：模型要能区分「企业没有这份资料」和「我们没给它看」
                sb.AppendLine($"### 条款 {c.ClauseNumber} · {c.Title}");
                sb.AppendLine("（该条款在企业已提交资料中未提取到对应内容）");
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
                    sb.AppendLine($"  摘要：{Truncate(ev.LlmSummary, 500)}");
                if (!string.IsNullOrEmpty(ev.TranscriptText))
                    sb.AppendLine($"  转录：{Truncate(ev.TranscriptText, 500)}");
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
            sb.AppendLine("- missing ：完全找不到所需资料");
            sb.AppendLine("- unknown ：无法判断（资料不足以下判断）—— ⛔ 不要猜，不要当成不符合");
            sb.AppendLine("- na      ：该条款不适用于本企业/本阶段");
            sb.AppendLine("⛔ 你**不能**输出 pass / ofi / nc —— 那是现场检查阶段的事，现在还没到现场。");
            sb.AppendLine();
            sb.AppendLine("{\"prep_state\":\"found/partial/missing/unknown/na\",\"summary\":\"一句话总结\",\"missing_kinds\":[\"缺的材料名称\"],\"conflicts\":[\"文档A说X，文档B说Y\"],\"verify_level\":\"doc/remote/onsite/test\",\"prep_nc_hint\":\"给现场的建议（可为空）\",\"confidence\":0-100}");
        }
        else
        {
            sb.AppendLine("## 判定口径（严格按此执行）");
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
    // ── 裁剪上限：控制单次推理的 token（用户口径「一个检查项需要的信息是有限的」）──

    /// <summary>单条摘录上限</summary>
    private const int MaxExcerptChars = 400;

    /// <summary>单份文档对单条款最多取几条摘录</summary>
    private const int MaxExcerptsPerDoc = 3;

    /// <summary>单条款最多引用几份文档</summary>
    private const int MaxDocsPerClause = 6;

    private readonly IDbOrm _db;

    /// <summary>核心资源（条款 / 标准 / 企业）的唯一读入口 —— 铁律 L5，守卫 A-R6。</summary>
    private readonly CertPlatform.Shared.Domain.Scope.ICoreResourceQuery _core;

    private readonly ILogger<CertCheckContextBuilder> _logger;

    public CertCheckContextBuilder(
        IDbOrm db,
        CertPlatform.Shared.Domain.Scope.ICoreResourceQuery core,
        ILogger<CertCheckContextBuilder> logger)
    {
        _db = db;
        _core = core;
        _logger = logger;
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
        IReadOnlyList<CertEvidence>? evidences = null)
    {
        var warnings = new List<string>();
        var evList = evidences?.ToList() ?? new List<CertEvidence>();

        if (clauseCodes.Count == 0)
        {
            warnings.Add("检查项未配置条款（cert_audit_plan_detail.Clauses 为空）⇒ 无法装配条款块");
            return new CertCheckContext
            {
                OrphanEvidences = evList.Where(e => string.IsNullOrEmpty(e.ClauseCode)).ToList(),
                Warnings = warnings
            };
        }

        // ---- 1. 条款模型：正文（cert_iso_clause）----
        var codeList = clauseCodes.Distinct(StringComparer.Ordinal).ToList();
        var clauseRows = await _core.GetClausesByCodesAsync(codeList);

        if (clauseRows.Count == 0)
        {
            warnings.Add($"条款 Code 在 cert_iso_clause 中全部查无此行：{string.Join(",", codeList)}");
            return new CertCheckContext { Warnings = warnings };
        }

        if (clauseRows.Count < codeList.Count)
        {
            var missing = codeList.Except(clauseRows.Select(c => c.Code), StringComparer.Ordinal);
            warnings.Add($"部分条款查无正文：{string.Join(",", missing)}");
        }

        // ---- 2. 条款模型：9 类属性（cert_clause_attr，长表 → 按条款聚合）----
        var attrRows = (await _db.GetListAsync<ClauseAttrRow>(a =>
            codeList.Contains(a.ClauseCode) && !a.IsDeleted && a.IsValid == 1)).Data
            ?? new List<ClauseAttrRow>();

        var attrsByClause = attrRows
            .GroupBy(a => a.ClauseCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.Sort).GroupBy(x => x.AttrCode, StringComparer.Ordinal)
                      .ToDictionary(x => x.Key, x => x.First().AttrValue, StringComparer.Ordinal),
                StringComparer.Ordinal);

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
                Description = c.Description,
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

        foreach (var ex in extractions)
        {
            if (string.IsNullOrWhiteSpace(ex.ContentJson)) continue;
            if (!wanted.TryGetValue(ex.StandardCode, out var numbers)) continue;

            var slices = ParseClauseSlices(ex.ContentJson!, numbers);
            if (slices.Count == 0) continue;

            foreach (var (clauseNumber, slice) in slices)
            {
                var target = clauses.FirstOrDefault(c =>
                    string.Equals(c.ClauseNumber, clauseNumber, StringComparison.Ordinal)
                    && string.Equals(c.StandardCode, ex.StandardCode, StringComparison.Ordinal));
                if (target == null) continue;

                // 单条款文档份数上限：超出只留前 N 份，且记告警（⛔ 不静默丢）
                if (target.Slices.Count >= MaxDocsPerClause)
                {
                    warnings.Add($"条款 {clauseNumber} 命中文档超过 {MaxDocsPerClause} 份，已截断（丢 {ex.Code}）");
                    continue;
                }

                target.Slices.Add(new ClauseDocSlice
                {
                    FileCode = ex.FileCode,
                    FileName = slice.FileName,
                    Anchor = slice.Anchor,
                    Excerpts = slice.Excerpts,
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
        string contentJson, HashSet<string> wantedNumbers)
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
                if (TryGetArray(el, out var exEl, "Excerpts", "excerpts"))
                {
                    foreach (var x in exEl.EnumerateArray())
                    {
                        if (x.ValueKind != JsonValueKind.String) continue;
                        var s = x.GetString();
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        excerpts.Add(Truncate(s.Trim(), MaxExcerptChars)!);
                        if (excerpts.Count >= MaxExcerptsPerDoc) break;
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
