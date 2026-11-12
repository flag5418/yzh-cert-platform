using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CertPlatform.Shared.Entities.Cert;

namespace CertPlatform.Shared.DocExtraction;

/// <summary>
/// 「标准条款 → 企业文档小节」索引（L1）。
///
/// <para>纯函数、无 DB、无 LLM：只解析 Markdown 标题，把条款号映射到 CharStart/CharEnd。</para>
/// <para>★ 试跑与线上必须共用本类，⛔ 不要再在 Admin/Auditor 各写一份。</para>
/// <para>输出 JSON 的字段名与既有 <c>SectionMapJson</c> 逐字一致，旧数据可直接读。</para>
/// </summary>
public static class DocSectionIndex
{
    private const int MaxBodyHeadChars = 300;

    private static readonly Regex HeadingRx = new(@"^(#{1,6})[ \t]+(.+?)[ \t]*$", RegexOptions.Compiled);
    private static readonly Regex NaRx = new(@"(?i)(不适用|n\s*/?\s*a|not\s+applicable)", RegexOptions.Compiled);
    private static readonly Regex MarkdownNoiseRx = new(@"[#>*_`|]", RegexOptions.Compiled);

    /// <summary>把 ISOClause 转成轻量引用（Auditor 端兼容入口）。</summary>
    public static IReadOnlyList<DocClauseRef> FromClauses(IReadOnlyList<ISOClause>? clauses)
        => (clauses ?? Array.Empty<ISOClause>())
            .Select(c => new DocClauseRef(
                c.Code ?? string.Empty,
                c.ClauseNumber ?? string.Empty,
                c.Title ?? string.Empty,
                c.Sort))
            .ToList();

    /// <summary>构建索引模型。</summary>
    public static DocSectionMap Build(
        string markdown,
        IReadOnlyList<DocClauseRef> clauses,
        string standardCode,
        string standardName)
    {
        var text = markdown ?? string.Empty;
        var headings = ParseHeadings(text);

        // 每个标题：条款号 + 祖先路径
        var infos = new List<HeadingInfo>();
        var stack = new SortedDictionary<int, string>();
        foreach (var h in headings)
        {
            var clauseNo = ExtractClauseNo(h.Title);

            // 章节号顶层变化 ⇒ 清空祖先栈（手册 9.1 前面没有 9 标题，
            // 否则 HeadingPath 会错挂到上一章 8.4 下）
            if (clauseNo != null && stack.Count > 0)
            {
                var top = clauseNo.Split('.')[0];
                var parentTop = ExtractClauseNo(stack.First().Value)?.Split('.')[0];
                if (!string.IsNullOrEmpty(parentTop) && parentTop != top)
                    stack.Clear();
            }

            var keysToRemove = stack.Keys.Where(k => k >= h.Level).ToList();
            foreach (var k in keysToRemove) stack.Remove(k);
            stack[h.Level] = h.Title;
            infos.Add(new HeadingInfo
            {
                Heading = h,
                ClauseNo = clauseNo,
                HeadingPath = string.Join(" / ", stack.Values),
            });
        }

        var exactByNo = new Dictionary<string, HeadingInfo>(StringComparer.Ordinal);
        foreach (var info in infos.Where(x => !string.IsNullOrWhiteSpace(x.ClauseNo)))
            exactByNo.TryAdd(info.ClauseNo!, info);

        var sections = new List<DocSectionItem>();

        foreach (var clause in (clauses ?? Array.Empty<DocClauseRef>())
                     .OrderBy(x => x.Sort)
                     .ThenBy(x => x.ClauseNumber, StringComparer.Ordinal))
        {
            var no = NormalizeClauseNo(clause.ClauseNumber);
            HeadingInfo? hit = null;
            var mode = "missing";
            int? virtualEnd = null;
            var virtualChildren = new List<string>();

            if (exactByNo.TryGetValue(no, out var exact))
            {
                hit = exact;
                mode = "exact";
            }
            else
            {
                // folded：找最长的祖先标题（4.4.1 没标题 ⇒ 4.4）
                hit = infos
                    .Where(x => !string.IsNullOrWhiteSpace(x.ClauseNo))
                    .Where(x => no.StartsWith(x.ClauseNo + ".", StringComparison.Ordinal))
                    .OrderByDescending(x => x.ClauseNo!.Length)
                    .FirstOrDefault();
                if (hit != null) mode = "folded";

                // 父条款本身没有标题、但子条款有（如标准 9 绩效评价 ⇒ 手册只有 9.1/9.2/9.3）
                // ⇒ 建一个跨子节的虚拟小节，别把父条款判成 missing
                if (hit == null)
                {
                    var children = infos
                        .Where(x => !string.IsNullOrWhiteSpace(x.ClauseNo))
                        .Where(x => x.ClauseNo!.StartsWith(no + ".", StringComparison.Ordinal))
                        .OrderBy(x => x.Heading.CharStart)
                        .ToList();
                    if (children.Count > 0)
                    {
                        hit = children[0];
                        mode = "folded";
                        virtualEnd = children
                            .Select(x => FindSectionEnd(text, headings, x.Heading))
                            .Max();
                        virtualChildren = children.Select(x => x.ClauseNo!).ToList();
                    }
                }
            }

            var section = new DocSectionItem
            {
                ClauseNo = no,
                ClauseCode = clause.Code,
                ClauseTitle = clause.Title,
                CoverageMode = mode,
                ChildrenCovered = new List<string>(),
            };

            if (hit != null)
            {
                var h = hit.Heading;
                var end = virtualEnd ?? FindSectionEnd(text, headings, h);
                var lineEnd = FindLineEnd(text.Substring(h.CharStart));
                var bodyStart = h.CharStart + lineEnd;
                var body = bodyStart < end ? text.Substring(bodyStart, end - bodyStart) : string.Empty;

                section.HeadingTitle = virtualChildren.Count > 0
                    ? $"{h.Title}（合并 {virtualChildren.Count} 节）"
                    : h.Title;
                section.HeadingPath = hit.HeadingPath;
                section.Level = h.Level;
                section.CharStart = h.CharStart;
                section.CharEnd = end;
                section.TextLength = end - h.CharStart;
                section.BodyHead = Truncate(CleanText(body), MaxBodyHeadChars);
                section.IsNa = body.Length > 0 && NaRx.IsMatch(body);
                section.ChildrenCovered.AddRange(virtualChildren);
            }

            sections.Add(section);
        }

        // 汇总 folded 子条款到父节
        foreach (var s in sections.Where(x => x.CoverageMode == "folded"))
        {
            var best = sections
                .Where(x => x.CoverageMode == "exact"
                            && s.ClauseNo.StartsWith(x.ClauseNo + ".", StringComparison.Ordinal))
                .OrderByDescending(x => x.ClauseNo.Length)
                .FirstOrDefault();
            if (best != null) best.ChildrenCovered.Add(s.ClauseNo);
        }

        return new DocSectionMap
        {
            StandardCode = standardCode ?? string.Empty,
            StandardName = standardName ?? string.Empty,
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalClauses = sections.Count,
            ExactCount = sections.Count(x => x.CoverageMode == "exact"),
            FoldedCount = sections.Count(x => x.CoverageMode == "folded"),
            MissingCount = sections.Count(x => x.CoverageMode == "missing"),
            Sections = sections,
        };
    }

    /// <summary>构建索引并序列化为既有 <c>SectionMapJson</c> 形态。</summary>
    public static string BuildJson(
        string markdown,
        IReadOnlyList<DocClauseRef> clauses,
        string standardCode,
        string standardName)
    {
        var model = Build(markdown, clauses, standardCode, standardName);
        return JsonSerializer.Serialize(model, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    /// <summary>Auditor 端兼容入口：直接用 ISOClause 构建 JSON。</summary>
    public static string BuildJson(
        string markdown,
        IReadOnlyList<ISOClause> clauses,
        string standardCode,
        string standardName)
        => BuildJson(markdown, FromClauses(clauses), standardCode, standardName);

    /// <summary>按条款号读取小节范围；找不到返回 Start/End = -1。</summary>
    public static DocSectionRange ReadRange(string? sectionMapJson, string? clauseNo)
    {
        var no = NormalizeClauseNo(clauseNo);
        if (string.IsNullOrWhiteSpace(sectionMapJson))
            return DocSectionRange.Missing;

        try
        {
            using var doc = JsonDocument.Parse(sectionMapJson);
            if (!doc.RootElement.TryGetProperty("Sections", out var sections)
                || sections.ValueKind != JsonValueKind.Array)
                return DocSectionRange.Missing;

            foreach (var s in sections.EnumerateArray())
            {
                var itemNo = s.TryGetProperty("ClauseNo", out var n) ? n.GetString() : null;
                if (NormalizeClauseNo(itemNo) != no) continue;

                var start = s.TryGetProperty("CharStart", out var st) ? st.GetInt32() : -1;
                var end = s.TryGetProperty("CharEnd", out var en) ? en.GetInt32() : -1;
                var title = s.TryGetProperty("HeadingTitle", out var t) ? t.GetString() : "";
                var path = s.TryGetProperty("HeadingPath", out var p) ? p.GetString() : "";
                var mode = s.TryGetProperty("CoverageMode", out var m) ? m.GetString() : "missing";
                return new DocSectionRange(start, end, title ?? "", path ?? "", mode ?? "missing");
            }
        }
        catch (JsonException)
        {
            // 旧数据损坏按 missing 处理，不阻断调用方
        }

        return DocSectionRange.Missing;
    }

    /// <summary>条款号归一：去尾冒号/顿号，去掉末尾 .0。</summary>
    public static string NormalizeClauseNo(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        s = s.TrimEnd('、', '：', ':');
        s = Regex.Replace(s, @"\.0$", "");
        return s;
    }

    private static int FindSectionEnd(string text, List<Heading> headings, Heading current)
    {
        var index = headings.IndexOf(current);
        var top = ExtractClauseNo(current.Title)?.Split('.')[0];
        for (var j = index + 1; j < headings.Count; j++)
        {
            var nextTop = ExtractClauseNo(headings[j].Title)?.Split('.')[0];
            if (headings[j].Level <= current.Level) return headings[j].CharStart;
            // 手册可能缺父标题（如没有「9」只有「9.1」）⇒ 顶层章节号变了也要收口
            if (!string.IsNullOrEmpty(top) && !string.IsNullOrEmpty(nextTop) && top != nextTop)
                return headings[j].CharStart;
        }
        return text.Length;
    }

    private static string? ExtractClauseNo(string title)
    {
        // 必须一次性吃完整条款号，⛔ 不能回溯成 4（否则 4.1 全被当成 4）
        var m = Regex.Match(title, @"^([0-9]+(?:\.[0-9]+)*)(?=\s|[、：:.]|$|[^\d.])");
        if (!m.Success) return null;
        return NormalizeClauseNo(m.Groups[1].Value);
    }

    private static List<Heading> ParseHeadings(string text)
    {
        var list = new List<Heading>();
        var inFence = false;
        var offset = 0;
        var lineNo = 0;

        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw.TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                inFence = !inFence;
                offset += raw.Length + 1;
                continue;
            }

            if (!inFence)
            {
                var m = HeadingRx.Match(line);
                if (m.Success)
                {
                    var title = m.Groups[2].Value.Trim();
                    if (IsTrustedHeading(title))
                    {
                        list.Add(new Heading(
                            Level: m.Groups[1].Value.Length,
                            Title: title,
                            CharStart: offset,
                            LineNo: lineNo));
                    }
                }
            }

            offset += raw.Length + 1;
        }

        return list;
    }

    private static bool IsTrustedHeading(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (title.Length > 28) return false;

        var last = title[^1];
        if (last is '。' or '，' or '；' or '、') return false;
        if (last == '：')
        {
            // 真条款号标题可能带冒号（如「8.3.2 产品设计开发的策划 ：」）；
            // 中文序号「1、质量目标：」或「总经理：」是正文/列表，不是章节。
            return Regex.IsMatch(title, @"^\d+(\.\d+)*\s+");
        }

        if (title.Contains("组织应") || title.Contains("公司应")) return false;
        return true;
    }

    private static int FindLineEnd(string body)
    {
        var idx = body.IndexOf('\n');
        return idx < 0 ? body.Length : idx + 1;
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value.Replace("\r", "");
        var lines = s.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => !Regex.IsMatch(l, @"^[-=_*]{3,}$"))
            .Where(l => !l.StartsWith("|") && !l.StartsWith("!["))
            .Select(l => MarkdownNoiseRx.Replace(l, " "))
            .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
            .Where(l => l.Length > 0)
            .ToList();
        return string.Join(" ", lines).Trim();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max) + "…";

    private sealed record Heading(int Level, string Title, int CharStart, int LineNo);

    private sealed class HeadingInfo
    {
        public Heading Heading { get; set; } = null!;
        public string? ClauseNo { get; set; }
        public string HeadingPath { get; set; } = string.Empty;
    }
}

/// <summary>轻量条款引用：Admin 端不需要构造完整 ISOClause 实体。</summary>
public sealed record DocClauseRef(string Code, string ClauseNumber, string Title, int Sort);

/// <summary>章节索引模型（字段名与既有 SectionMapJson 一致）。</summary>
public sealed class DocSectionMap
{
    public string StandardCode { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public int TotalClauses { get; set; }
    public int ExactCount { get; set; }
    public int FoldedCount { get; set; }
    public int MissingCount { get; set; }
    public List<DocSectionItem> Sections { get; set; } = new();
}

/// <summary>单个条款对应的小节。</summary>
public sealed class DocSectionItem
{
    public string ClauseNo { get; set; } = string.Empty;
    public string ClauseCode { get; set; } = string.Empty;
    public string ClauseTitle { get; set; } = string.Empty;
    public string CoverageMode { get; set; } = "missing";
    public string HeadingTitle { get; set; } = string.Empty;
    public string HeadingPath { get; set; } = string.Empty;
    public int Level { get; set; }
    public int CharStart { get; set; }
    public int CharEnd { get; set; }
    public int TextLength { get; set; }
    public bool IsNa { get; set; }
    public string BodyHead { get; set; } = string.Empty;
    public List<string> ChildrenCovered { get; set; } = new();
}

/// <summary>小节范围读取结果。</summary>
public readonly record struct DocSectionRange(
    int Start,
    int End,
    string Title,
    string Path,
    string Mode)
{
    public static DocSectionRange Missing => new(-1, -1, string.Empty, string.Empty, "missing");
    public bool HasRange => Start >= 0 && End > Start;
}
