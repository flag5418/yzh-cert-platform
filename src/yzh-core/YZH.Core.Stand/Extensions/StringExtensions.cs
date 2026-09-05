using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace YZH.Core.Stand.Extensions;

/// <summary>字符串扩展方法</summary>
public static class StringExtensions
{
    /// <summary>ToInt - 安全转 int</summary>
    public static int ToInt(this string? s, int defaultValue = 0)
    {
        return int.TryParse(s, out var v) ? v : defaultValue;
    }

    /// <summary>ToLong - 安全转 long</summary>
    public static long ToLong(this string? s, long defaultValue = 0L)
    {
        return long.TryParse(s, out var v) ? v : defaultValue;
    }

    /// <summary>ToDateTime - 安全转 DateTime</summary>
    public static DateTime? ToDateTime(this string? s)
    {
        return DateTime.TryParse(s, out var v) ? v : null;
    }

    /// <summary>ToBool - 安全转 bool（1/true/yes 为真）</summary>
    public static bool ToBool(this string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var trimmed = s.Trim().ToLowerInvariant();
        return trimmed == "1" || trimmed == "true" || trimmed == "yes" || trimmed == "on";
    }

    /// <summary>IsNullOrEmpty - 空判断</summary>
    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrEmpty(s);

    /// <summary>IsNotNullOrEmpty - 非空判断</summary>
    public static bool IsNotNullOrEmpty(this string? s) => !string.IsNullOrEmpty(s);

    /// <summary>IsNull - null 或空或纯空白判断</summary>
    public static bool IsNull(this string? s) => string.IsNullOrWhiteSpace(s);

    /// <summary>LimitLength - 限制长度并加省略号</summary>
    public static string LimitLength(this string? s, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(s) || s.Length <= maxLength) return s ?? string.Empty;
        return s[..maxLength] + suffix;
    }

    /// <summary>PascalCase - 转 PascalCase</summary>
    public static string ToPascalCase(this string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var parts = s.Split('-', '_', ' ');
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
                sb.Append(char.ToUpperInvariant(part[0])).Append(part[1..].ToLowerInvariant());
        }
        return sb.ToString();
    }

    /// <summary>CamelCase - 转 camelCase</summary>
    public static string ToCamelCase(this string? s)
    {
        var pascal = s.ToPascalCase();
        return string.IsNullOrEmpty(pascal) ? string.Empty : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>SnakeCase - 转 snake_case</summary>
    public static string ToSnakeCase(this string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return Regex.Replace(s, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
    }

    /// <summary>Mask - 打码（手机号/身份证/邮箱）</summary>
    public static string Mask(this string? s, int start, int end, char maskChar = '*')
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Length <= start + end) return s;
        var masked = new string(maskChar, s.Length - start - end);
        return s[..start] + masked + s[^end..];
    }

    /// <summary>MaskPhone - 手机号打码</summary>
    public static string MaskPhone(this string? phone) => phone.Mask(3, 4);

    /// <summary>MaskEmail - 邮箱打码</summary>
    public static string? MaskEmail(this string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@')) return email;
        var parts = email.Split('@');
        var name = parts[0];
        var maskedName = name.Length <= 2 ? name : name[..2] + new string('*', name.Length - 2);
        return $"{maskedName}@{parts[1]}";
    }

    /// <summary>Base64 编码</summary>
    public static string ToBase64(this string s) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    /// <summary>Base64 解码</summary>
    public static string FromBase64(this string s) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(s));
}
