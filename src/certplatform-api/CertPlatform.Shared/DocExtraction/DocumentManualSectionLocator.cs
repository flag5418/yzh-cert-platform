using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CertPlatform.Shared.DocExtraction;

/// <summary>在唯一标题和唯一语义锚点同时吻合时，定位 Markdown 章节候选。</summary>
public static class DocumentManualSectionLocator
{
    private static readonly Regex Heading = new(@"^ {0,3}(#{1,6})[ \t]+(.+?)\s*#*\s*$", RegexOptions.Compiled);
    private static readonly Regex Fence = new(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.Compiled);

    public static (int Start, int End)? Locate(string markdown, string clause, string anchor)
    {
        if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(clause)) return null;
        var anchorStart = DocumentSourceLocator.LocateUnique(markdown, anchor);
        if (!anchorStart.HasValue) return null;

        var number = new Regex(@"^" + Regex.Escape(clause.Trim()) + @"(?=$|[\s、：:])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var headings = new List<(int Start, int Level, bool Match)>();
        char fenceChar = '\0';
        var fenceLength = 0;
        for (var offset = 0; offset < markdown.Length;)
        {
            var newline = markdown.IndexOf('\n', offset);
            var end = newline < 0 ? markdown.Length : newline + 1;
            var line = markdown.Substring(offset, (newline < 0 ? markdown.Length : newline) - offset).TrimEnd('\r');
            var fence = Fence.Match(line);
            if (fence.Success)
            {
                var marker = fence.Groups[1].Value;
                if (fenceChar == '\0') { fenceChar = marker[0]; fenceLength = marker.Length; }
                else if (marker[0] == fenceChar && marker.Length >= fenceLength
                    && string.IsNullOrWhiteSpace(line[(fence.Index + fence.Length)..])) fenceChar = '\0';
            }
            else if (fenceChar == '\0')
            {
                var heading = Heading.Match(line);
                if (heading.Success)
                    headings.Add((offset, heading.Groups[1].Length, number.IsMatch(heading.Groups[2].Value)));
            }
            offset = end;
        }

        var matching = headings.FindAll(h => h.Match);
        if (matching.Count != 1) return null;
        var current = matching[0];
        var sectionEnd = markdown.Length;
        foreach (var heading in headings)
        {
            if (heading.Start > current.Start && heading.Level <= current.Level)
            {
                sectionEnd = heading.Start;
                break;
            }
        }
        return anchorStart.Value >= current.Start && anchorStart.Value + anchor.Length <= sectionEnd
            ? (current.Start, sectionEnd) : null;
    }
}
