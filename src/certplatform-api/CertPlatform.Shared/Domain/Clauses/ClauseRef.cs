using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CertPlatform.Shared.Domain.Clauses
{
    /// <summary>
    /// 条款业务键（稳定键）：<c>StandardNo + ClauseNumber</c>。
    ///
    /// <para><b>为什么不用 <c>cert_iso_clause.Code</c></b>：标准条款被重新生成时旧 Code 会软删，
    /// 计划 / 属性 / 检查 / 准备按 Code 关联会静默失效。条款号在同一标准内稳定不变。</para>
    ///
    /// <para><b>为什么必须带 <c>StandardNo</c></b>：ISO9001 / ISO14001 / ISO45001 都有 4.1，
    /// 只存编号会跨标准串条款。</para>
    /// </summary>
    public sealed class ClauseRef
    {
        /// <summary>标准短码（iso9001 / iso14001 / iso45001 / iso13485）</summary>
        public string StandardNo { get; set; } = string.Empty;

        /// <summary>条款编号（4.1 / 7.1.2），统一归一为纯编号，不带 e / m 等展示前缀</summary>
        public string ClauseNumber { get; set; } = string.Empty;
    }

    /// <summary>条款业务键的归一、解析与序列化。</summary>
    public static class ClauseRefs
    {
        private static readonly Regex NumberPattern = new Regex(@"\d+(?:\.\d+)*", RegexOptions.Compiled);

        /// <summary>
        /// 把 <c>e 4.1</c> / <c>m 1</c> / <c>4.1 理解组织及其环境</c> 统一成纯条款号 <c>4.1</c>。
        /// <para>找不到数字时返回 Trim 后的原值，供调用方继续报错而不是静默丢弃。</para>
        /// </summary>
        public static string NormalizeNumber(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var text = raw.Trim();
            var m = NumberPattern.Match(text);
            return m.Success ? m.Value : text;
        }

        /// <summary>解析 <c>ClauseRefsJson</summary>，兼容大小写字段名与空值。</summary>
        public static List<ClauseRef> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<ClauseRef>();
            try
            {
                var rows = JsonSerializer.Deserialize<List<ClauseRef>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ClauseRef>();
                return Clean(rows);
            }
            catch (JsonException)
            {
                return new List<ClauseRef>();
            }
        }

        /// <summary>序列化为业务键 JSON（中文不转义，排障可直接读）。</summary>
        public static string ToJson(IEnumerable<ClauseRef>? refs)
        {
            var rows = Clean(refs);
            return JsonSerializer.Serialize(rows, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        /// <summary>去空、归一、按 (标准, 编号) 去重，并保持原顺序。</summary>
        public static List<ClauseRef> Clean(IEnumerable<ClauseRef>? refs)
        {
            var result = new List<ClauseRef>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in refs ?? Enumerable.Empty<ClauseRef>())
            {
                var std = (r.StandardNo ?? string.Empty).Trim();
                var no = NormalizeNumber(r.ClauseNumber);
                if (std.Length == 0 || no.Length == 0) continue;
                var key = std + "|" + no;
                if (!seen.Add(key)) continue;
                result.Add(new ClauseRef { StandardNo = std, ClauseNumber = no });
            }
            return result;
        }

        /// <summary>
        /// 旧数据兼容：<c>ClauseNumbers</c> 是逗号串（常带 <c>e 4.1</c> 前缀），
        /// 在不知道每段标准时，用同一 <paramref name="standardNo"/> 补齐。
        /// </summary>
        public static List<ClauseRef> FromLegacyNumbers(string? clauseNumbers, string standardNo)
        {
            if (string.IsNullOrWhiteSpace(clauseNumbers)) return new List<ClauseRef>();
            return Clean(clauseNumbers
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => new ClauseRef { StandardNo = standardNo, ClauseNumber = n }));
        }
    }
}
