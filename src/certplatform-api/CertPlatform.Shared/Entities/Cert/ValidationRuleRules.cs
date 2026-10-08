using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// <see cref="ValidationRule"/> 的业务判据 —— <b>全项目唯一口径</b>。
    ///
    /// <para><b>建立背景（2026-10-08）</b>：此前「未配 DAG」在 DB / 实体 / 运行时三处各写各的判断，
    /// 且允许 <c>"{}"</c> 落库，造成"跑通但没配流程"的静默错误。本类把判据与归一化收口到一处。</para>
    ///
    /// <para><b>★ 空值语义（定稿）</b>：<c>RuleJson == NULL</c> ⇒ <b>未配置工作流</b>。
    /// 禁用 <c>""</c> / <c>"{}"</c> / <c>"null"</c> / <c>"[]"</c> 四种字符串表达。</para>
    /// </summary>
    public static class ValidationRuleRules
    {
        /// <summary>
        /// 是否已配置工作流 DAG —— ★ 唯一判据。
        /// <para>NULL / 空串 = 未配置（候选项显示"无工作流"，执行器跳过而非报错）。</para>
        /// </summary>
        public static bool HasWorkflow(this ValidationRule? rule)
            => rule != null && !string.IsNullOrWhiteSpace(rule.RuleJson);

        /// <summary>
        /// 空画布 JSON 归一化 —— <b>落库前必须调用</b>。
        /// <para><c>""</c> / <c>"{}"</c> / <c>"null"</c> / <c>"[]"</c> 一律归一为 <c>null</c>，
        /// 保证 <see cref="HasWorkflow"/> 判据可靠。</para>
        /// </summary>
        public static string? NormalizeDag(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var trimmed = json.Trim();
            return trimmed is "{}" or "null" or "[]" ? null : json;
        }

        /// <summary>
        /// 下一个可用规则编号 —— <c>NC-{StandardCode}-{seq:D3}</c>。
        /// <para>★ 取代原「<c>count + 1</c>」算法：按行数递增在<b>删行后会重号</b>，
        /// 直接撞 <c>uk_rule_code</c> 唯一键。此处对既有编号取 <c>MAX(seq) + 1</c>。</para>
        /// </summary>
        /// <param name="standardCode">标准编码（<c>cert_iso_standard.Code</c>）</param>
        /// <param name="existing">同标准下的既有规则（<b>须包含已禁用行</b>，否则仍会重号）</param>
        public static string NextRuleCode(string standardCode, IEnumerable<ValidationRule>? existing)
        {
            var prefix = $"NC-{standardCode}-";
            var max = (existing ?? Enumerable.Empty<ValidationRule>())
                .Select(r => r.RuleCode ?? string.Empty)
                .Where(c => c.StartsWith(prefix, StringComparison.Ordinal))
                .Select(c => int.TryParse(c.AsSpan(prefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"{prefix}{max + 1:D3}";
        }
    }
}
