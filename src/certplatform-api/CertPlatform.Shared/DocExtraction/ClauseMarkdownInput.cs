using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Shared.DocExtraction;

/// <summary>读取由标准条款表生成的 Markdown，不回退到原始标准文件。</summary>
public static class ClauseMarkdownInput
{
    private static readonly Regex SectionHeader = new(
        @"(?m)^#{2,6} (?<title>[^\r\n]+)\r?\n- 条款号：(?<number>[^\r\n]+)\r?\n- 父条款：[^\r\n]*\r?\n- 叶子：[^\r\n]*\r?\n- 标准要求：\r?\n",
        RegexOptions.Compiled);

    /// <summary>获取条款 Markdown 路径。</summary>
    public static string SelectPath(ISOStandard standard)
    {
        if (string.IsNullOrWhiteSpace(standard.ClauseMarkdownPath))
            throw new InvalidOperationException("标准缺少条款 Markdown，请先生成或重建条款 Markdown");
        return standard.ClauseMarkdownPath;
    }

    /// <summary>下载条款 Markdown。</summary>
    public static async Task<string> LoadAsync(IObjectStorage storage, ISOStandard standard)
    {
        var path = SelectPath(standard);
        var (stream, _) = await storage.DownloadAsync(path.TrimStart('/'));
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var markdown = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidOperationException("条款 Markdown 为空，请重新生成");
        return markdown;
    }

    /// <summary>读取对应条款段，并检查标题与正文是否与当前条款表一致。</summary>
    public static string ReadSection(string markdown, ISOClause clause)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidOperationException("条款 Markdown 为空，请重新生成");
        var headers = SectionHeader.Matches(markdown).Cast<Match>().ToList();
        var number = (clause.ClauseNumber ?? string.Empty).Trim();
        var matches = headers.Where(h => string.Equals(h.Groups["number"].Value.Trim(), number, StringComparison.Ordinal)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"条款 Markdown 中条款【{number}】缺失或重复，请重新生成");

        var header = matches[0];
        var index = headers.IndexOf(header);
        var end = index + 1 < headers.Count ? headers[index + 1].Index : markdown.Length;
        var section = markdown.Substring(header.Index, end - header.Index);
        var title = (clause.Title ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        var expectedHeading = number + (title.Length == 0 ? string.Empty : " " + title);
        var expectedBody = (clause.Description ?? string.Empty).Replace("\r\n", "\n").Trim();
        var body = markdown.Substring(header.Index + header.Length, end - header.Index - header.Length)
            .Replace("\r\n", "\n").Trim();
        if (expectedBody.Length == 0 || !string.Equals(header.Groups["title"].Value.Trim(), expectedHeading, StringComparison.Ordinal)
            || !string.Equals(body, expectedBody, StringComparison.Ordinal))
            throw new InvalidOperationException($"条款【{number}】正文缺失或条款 Markdown 与当前条款不一致，请核对并重新生成");
        return section.TrimEnd();
    }
}
