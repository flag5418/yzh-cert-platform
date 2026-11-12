using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.DocExtraction;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Ent;

/// <summary>
/// 企业文档「简介 + 目录索引」构建器。
///
/// <para><b>定位</b>：只做路由信息，不做标准符合性判断。产出的 <c>Toc[]</c> 带
/// <c>CharStart/CharEnd</c>，后续按检查项取小节时可直接用偏移回查原始 Markdown，
/// 不必每次把整本 2~3 万字塞进提示词。</para>
///
/// <para><b>为什么确定性生成</b>：标题、偏移、是否空节都是原文客观属性，先用规则做；
/// 简介/目的/范围也只从原文摘，不调用 LLM ⇒ 不增加转换耗时，也不会编造原文没有的内容。
/// 后续若需要更“像人写”的简介，再单独加 LLM 润色，⛔ 不要把它混进结构索引。</p>
/// </summary>
public static class EnterpriseDocProfileBuilder
{
    private const int MaxIntroChars = 400;
    private const int MaxPurposeChars = 240;
    private const int MaxScopeChars = 240;
    private const int MinBodyChars = 30;

    private static readonly Regex HeadingRx = new(@"^(#{1,6})[ \t]+(.+?)[ \t]*$", RegexOptions.Compiled);
    private static readonly Regex NaRx = new(@"(?i)(不适用|n\s*/?\s*a|not\s+applicable)");
    private static readonly Regex MarkdownNoiseRx = new(@"[#>*_`|]", RegexOptions.Compiled);

    /// <summary>构建并落库；失败只记日志，不阻塞转换主链。</summary>
    public static async Task<bool> BuildAndSaveAsync(
        IDbOrm db, EnterpriseDoc row, string markdown, ILogger logger)
    {
        try
        {
            row.DocProfileJson = Build(markdown, row.FileName, row.RelFolderPath, row.Sha256, row.VersionNumber);
            row.DocProfileUpdatedTime = DateTime.Now;
            row.UpdateTime = DateTime.Now;
            await db.UpdateAsync(row,
                nameof(EnterpriseDoc.DocProfileJson),
                nameof(EnterpriseDoc.DocProfileUpdatedTime),
                nameof(EnterpriseDoc.UpdateTime));
            return true;
        }
        catch (Exception ex)
        {
            // 简介失败不能把 Markdown 转换打回 failed：提取仍然可用
            logger.LogWarning(ex, "[文档简介] 生成失败 file={Code} name={Name}", row.Code, row.FileName);
            return false;
        }
    }

    /// <summary>纯函数：Markdown → DocProfileJson（便于单测/回填）。</summary>
    public static string Build(
        string markdown, string fileName, string relFolderPath, string sha256, int versionNumber)
    {
        var text = markdown ?? string.Empty;
        var headings = ParseHeadings(text);
        var docType = GuessDocType(fileName, relFolderPath);
        var intro = BuildIntro(text, headings);
        var purpose = ExtractSectionText(text, headings, "目的");
        var scope = ExtractSectionText(text, headings, "适用范围") ?? ExtractSectionText(text, headings, "范围");

        var toc = BuildToc(text, headings);

        var profile = new DocProfile
        {
            DocType = docType,
            Intro = intro,
            Purpose = purpose,
            Scope = scope,
            Confidence = headings.Count >= 3 ? 0.8 : (headings.Count > 0 ? 0.65 : 0.45),
            SourceSha256 = sha256 ?? string.Empty,
            VersionNumber = versionNumber,
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Toc = toc,
        };

        return JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    /// <summary>判断简介是否已过期（文件内容/版本变了 ⇒ 必须重建）。</summary>
    public static bool IsStale(string? profileJson, string sha256, int versionNumber)
    {
        if (string.IsNullOrWhiteSpace(profileJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(profileJson);
            var root = doc.RootElement;
            var hash = root.TryGetProperty("SourceSha256", out var h) ? h.GetString() : null;
            var ver = root.TryGetProperty("VersionNumber", out var v) ? v.GetInt32() : 0;
            return !string.Equals(hash, sha256, StringComparison.OrdinalIgnoreCase)
                   || ver != versionNumber;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    // ──── 结构解析 ────

    private sealed record Heading(int Level, string Title, int CharStart, int LineNo);

    /// <summary>
    /// L0 标题可信度：标题文本过长、以句读结尾、明显是正文续写 ⇒ 不当作章节标题。
    /// <para>本手册实测 6 条正文被 anydoc 升成标题；这些不进目录，正文仍留在父节 CharEnd 内。</para>
    /// </summary>
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

    private static List<TocItem> BuildToc(string text, List<Heading> headings)
    {
        var toc = new List<TocItem>(headings.Count);
        for (var i = 0; i < headings.Count; i++)
        {
            var h = headings[i];
            var end = text.Length;
            for (var j = i + 1; j < headings.Count; j++)
            {
                if (headings[j].Level <= h.Level)
                {
                    end = headings[j].CharStart;
                    break;
                }
            }

            var body = end > h.CharStart ? text.Substring(h.CharStart, end - h.CharStart) : string.Empty;
            // 去掉标题行本身再判“有没有正文”
            var bodyWithoutHeading = body.Length > 0
                ? body.Substring(Math.Min(body.Length, FindLineEnd(body)))
                : string.Empty;
            var bodyText = MarkdownNoiseRx.Replace(bodyWithoutHeading, " ");
            var bodyLen = Regex.Replace(bodyText, @"\s+", " ").Trim().Length;

            toc.Add(new TocItem
            {
                Title = h.Title,
                Level = h.Level,
                CharStart = h.CharStart,
                CharEnd = end,
                Line = h.LineNo,
                HasContent = bodyLen >= MinBodyChars,
                IsNa = bodyLen > 0 && bodyLen <= 300 && NaRx.IsMatch(bodyWithoutHeading),
                ChildCount = CountDirectChildren(headings, i),
            });
        }
        return toc;
    }

    private static int CountDirectChildren(List<Heading> headings, int index)
    {
        var level = headings[index].Level;
        var count = 0;
        for (var j = index + 1; j < headings.Count; j++)
        {
            if (headings[j].Level <= level) break;
            if (headings[j].Level == level + 1) count++;
        }
        return count;
    }

    private static int FindLineEnd(string body)
    {
        var idx = body.IndexOf('\n');
        return idx < 0 ? body.Length : idx + 1;
    }

    private static string BuildIntro(string text, List<Heading> headings)
    {
        // 优先取真正的「简介/前言/概述」小节，避免把封面、目录对应表当成简介
        foreach (var keyword in new[] { "公司简介", "简介", "前言", "概述" })
        {
            var section = ExtractSectionText(text, headings, keyword, MaxIntroChars);
            if (!string.IsNullOrWhiteSpace(section))
                return Truncate(section!, MaxIntroChars);
        }

        var firstHeading = headings.Count > 0 ? headings[0].CharStart : text.Length;
        var head = text.Substring(0, Math.Min(firstHeading, text.Length));
        var cleaned = CleanText(head);

        // 封面往往只有标题/公司名/日期，太短就借第一节正文
        if (cleaned.Length < 80 && headings.Count > 0)
        {
            var first = headings[0];
            var end = first.CharStart + 1200;
            if (headings.Count > 1) end = Math.Min(end, headings[1].CharStart);
            var bodyStart = first.CharStart + FindLineEnd(text.Substring(first.CharStart));
            if (bodyStart < end)
                cleaned = CleanText(text.Substring(bodyStart, end - bodyStart));
        }

        return Truncate(cleaned, MaxIntroChars);
    }

    private static string? ExtractSectionText(
        string text, List<Heading> headings, string keyword, int maxChars = MaxPurposeChars)
    {
        foreach (var (h, i) in headings.Select((h, i) => (h, i)))
        {
            if (!h.Title.Contains(keyword, StringComparison.Ordinal)) continue;
            var end = text.Length;
            for (var j = i + 1; j < headings.Count; j++)
            {
                if (headings[j].Level <= h.Level) { end = headings[j].CharStart; break; }
            }

            // 去掉标题行本身，只留正文
            var lineEnd = FindLineEnd(text.Substring(h.CharStart));
            var bodyStart = h.CharStart + lineEnd;
            if (bodyStart >= end) continue;

            var cleaned = CleanText(text.Substring(bodyStart, end - bodyStart));
            if (cleaned.Length > 0) return Truncate(cleaned, MaxPurposeChars);
        }
        return null;
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value.Replace("\r", "");
        // 去表格分隔线 / 纯符号行 / 图片
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

    private static string GuessDocType(string fileName, string folder)
    {
        var s = $"{folder}/{fileName}";
        if (s.Contains("手册")) return "质量手册";
        if (s.Contains("程序")) return "程序文件";
        if (s.Contains("作业指导") || s.Contains("操作规程") || s.Contains("指导书")) return "作业指导书";
        if (s.Contains("记录") || s.Contains("表单") || s.Contains("表格")) return "记录表单";
        if (s.Contains("方针") || s.Contains("目标")) return "方针目标";
        if (s.Contains("管理评审") || s.Contains("内审") || s.Contains("审核报告")) return "体系记录";
        return "体系文件";
    }

    // ──── L1 章节索引（标准条款 → 手册小节）────

    /// <summary>
    /// 构建「标准条款 → 手册小节」索引：能精确命中的标 exact，无独立标题但被父节吸收的标 folded。
    /// <para>只做映射，不做判定；输出带 CharStart/CharEnd，可直接回查原文。</para>
    /// </summary>
    public static string BuildSectionIndex(
        string markdown, IReadOnlyList<ISOClause> clauses,
        string standardCode, string standardName)
        // ★ 2026-11-12：构建逻辑已迁到 CertPlatform.Shared.DocExtraction.DocSectionIndex，
        //   Admin 试跑与 Auditor 线上提取共用同一份；此处仅保留兼容入口。
        => DocSectionIndex.BuildJson(markdown, clauses, standardCode, standardName);

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

    private static string NormalizeClauseNumber(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        s = s.TrimEnd('、', '：', ':');
        s = Regex.Replace(s, @"\.0$", "");
        return s;
    }

    private static string? ExtractClauseNo(string title)
    {
        // 必须一次性吃完整条款号，⛔ 不能回溯成 4（否则 4.1 全被当成 4）
        var m = Regex.Match(title, @"^([0-9]+(?:\.[0-9]+)*)(?=\s|[、：:.]|$|[^\d.])");
        if (!m.Success) return null;
        return NormalizeClauseNumber(m.Groups[1].Value);
    }

    // ──── JSON 模型（PascalCase，与前端字段逐字一致）────

    private sealed class HeadingInfo
    {
        public Heading Heading { get; set; } = null!;
        public string? ClauseNo { get; set; }
        public string HeadingPath { get; set; } = string.Empty;
    }

    private sealed class SectionMap
    {
        public string StandardCode { get; set; } = string.Empty;
        public string StandardName { get; set; } = string.Empty;
        public string GeneratedAt { get; set; } = string.Empty;
        public int TotalClauses { get; set; }
        public int ExactCount { get; set; }
        public int FoldedCount { get; set; }
        public int MissingCount { get; set; }
        public List<SectionItem> Sections { get; set; } = new();
    }

    private sealed class SectionItem
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

    private sealed class DocProfile
    {        public string DocType { get; set; } = "体系文件";
        public string Intro { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string? Scope { get; set; }
        public double Confidence { get; set; }
        public string SourceSha256 { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string GeneratedAt { get; set; } = string.Empty;
        public List<TocItem> Toc { get; set; } = new();
    }

    private sealed class TocItem
    {
        public string Title { get; set; } = string.Empty;
        public int Level { get; set; }
        public int CharStart { get; set; }
        public int CharEnd { get; set; }
        public int Line { get; set; }
        public bool HasContent { get; set; }
        public bool IsNa { get; set; }
        public int ChildCount { get; set; }
    }
}
