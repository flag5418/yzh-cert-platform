using System;

namespace CertPlatform.Shared.DocExtraction;

/// <summary>对转换后的原文进行逐字定位，偏移量使用 UTF-16 字符索引。</summary>
public static class DocumentSourceLocator
{
    /// <summary>仅在原文中恰好出现一次时返回起始位置，否则返回空。</summary>
    public static int? LocateUnique(string? source, string? quote)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(quote)) return null;
        var first = source.IndexOf(quote, StringComparison.Ordinal);
        if (first < 0) return null;
        return source.IndexOf(quote, first + 1, StringComparison.Ordinal) >= 0 ? null : first;
    }
}
