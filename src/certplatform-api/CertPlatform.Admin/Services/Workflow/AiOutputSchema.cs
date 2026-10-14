using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// AI 节点「结构化输出」的<b>轻量 Schema 校验器</b>（第 5 批 · 设计 §4 表「outputType 增 json + Schema 校验」）。
    ///
    /// <para><b>为什么是轻量而不是完整 JSON Schema</b>：完整校验器（draft 2020-12 全量）依赖重、
    /// 报错信息对「提示词调优」场景噪音大。本仓只需约束「AI 返回的 JSON 字段齐不齐、类型对不对」，
    /// 故采用<b>扁平映射</b>格式（口径对齐 `规则Skill与证据链-功能设计-V1.md` §3.3 的 outputSchema）：
    /// <c>{ "字段名": "类型表达式" }</c>。</para>
    ///
    /// <para><b>类型表达式</b>（只认下表，⛔ 不支持嵌套对象 / $ref / patternProperties）：</para>
    /// <list type="bullet">
    /// <item><c>string</c> / <c>number</c> / <c>boolean</c> / <c>any</c> —— 基础类型</item>
    /// <item><c>string[]</c> / <c>number[]</c> / <c>boolean[]</c> / <c>any[]</c> —— 数组（元素同基础类型）</item>
    /// <item><c>pass|fail|unknown</c> —— 字符串枚举（含 <c>|</c> 即视为枚举，元素须逐字相等）</item>
    /// <item>末尾 <c>?</c> 表示<b>可选</b>（如 <c>string?</c> / <c>string[]?</c>）；缺字段时不报错</item>
    /// </list>
    ///
    /// <para><b>Schema 来源</b>：节点 <c>config.outputSchema</c>（存 <c>rule_json</c>，与 <c>params</c> 同处）。
    /// 可落库为 JSON 对象（<c>JsonElement</c> / <c>Dictionary</c>）或 JSON 字符串 —— 两种形态都要吃得下
    /// （与 <see cref="AiParams.ParseDeclaredParams"/> 同口径教训：只吃一种形态 = 静默解析成空表）。</para>
    ///
    /// <para><b>校验失败语义</b>：返回<b>违规清单</b>（非 null 即有违规）。调用方（AiNodeExecutor）
    /// 据此抛 <c>AiNodeException("JSON_SCHEMA_VIOLATION")</c> —— ⛔ 不静默放行：
    /// 下游按「字段已存在」读取却拿到 null，是本仓最忌讳的「静默坏数据」。</para>
    /// </summary>
    public static class AiOutputSchema
    {
        /// <summary>
        /// 校验 <paramref name="value"/> 是否符合 <paramref name="schema"/>。
        /// <para>返回违规清单；<b>空清单 = 通过</b>。schema 为空 / 缺失 ⇒ 恒空清单（不校验）。</para>
        /// </summary>
        /// <param name="schemaObj">节点 config.outputSchema —— JsonElement / Dictionary / JSON 字符串 均可</param>
        /// <param name="value">已解析的 AI 输出（Dictionary&lt;string,object&gt; / List / 原始值）</param>
        public static IReadOnlyList<string> Validate(object? schemaObj, object? value)
        {
            var schema = NormalizeSchema(schemaObj);
            if (schema == null || schema.Count == 0) return Array.Empty<string>();

            var violations = new List<string>();

            // 根必须是对象（JSON object）才能按字段校验
            if (value is not IDictionary<string, object> dict)
            {
                violations.Add($"根类型不符：schema 按对象字段校验，实际输出是 {DescribeType(value)}");
                return violations;
            }

            foreach (var (field, typeExpr) in schema)
            {
                var optional = typeExpr.EndsWith("?", StringComparison.Ordinal);
                var expr = optional ? typeExpr[..^1] : typeExpr;

                if (!dict.TryGetValue(field, out var fieldValue) || fieldValue is null)
                {
                    if (!optional)
                        violations.Add($"缺少必填字段 `{field}`（期望类型 {expr}）");
                    continue;
                }

                var err = CheckType(field, expr, fieldValue);
                if (err != null) violations.Add(err);
            }

            return violations;
        }

        // ─────────────────────────────────────────────
        // schema 规范化：JsonElement / Dictionary / 字符串 → Dictionary<string,string>
        // ─────────────────────────────────────────────

        /// <summary>
        /// 把 schema 的多种落库形态统一成 <c>字段 → 类型表达式</c>；解析不出 / 非对象 ⇒ null（视为「未配 schema」）。
        /// <para><b>字符串两种形态都要吃得下</b>（与 <see cref="AiParams.ParseDeclaredParams"/> 同口径教训）：</para>
        /// <list type="bullet">
        /// <item><b>JSON 对象</b>：<c>{"verdict":"pass|fail|unknown"}</c> —— 存 rule_json 的原始形态</item>
        /// <item><b>逐行「字段:类型」</b>（前端 textarea 存的形态）：<c>verdict:pass|fail|unknown\nconfidence:number</c></item>
        /// </list>
        /// 只吃一种 = 静默解析成「未配 schema」，校验形同虚设。
        /// </summary>
        private static Dictionary<string, string>? NormalizeSchema(object? schemaObj)
        {
            switch (schemaObj)
            {
                case null:
                    return null;

                case string s:
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    // 形态一：JSON 对象
                    try
                    {
                        using var doc = JsonDocument.Parse(s);
                        var fromJson = FromElement(doc.RootElement);
                        if (fromJson != null) return fromJson;
                    }
                    catch
                    {
                        // 非 JSON ⇒ 落到形态二（逐行「字段:类型」）
                    }
                    // 形态二：逐行「字段:类型」（前端 textarea 存的形态）
                    return FromLines(s);

                case JsonElement je:
                    return FromElement(je);

                case IDictionary<string, object> dict:
                    var result = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var (k, v) in dict)
                    {
                        var expr = v?.ToString();
                        if (!string.IsNullOrWhiteSpace(expr)) result[k] = expr!.Trim();
                    }
                    return result;

                default:
                    return null;
            }
        }

        /// <summary>逐行「字段:类型」→ Dictionary（空行 / 无冒号的行跳过；第一个冒号分隔）。</summary>
        private static Dictionary<string, string> FromLines(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue; // 空行 / 注释行
                var colon = line.IndexOf(':');
                if (colon <= 0 || colon >= line.Length - 1) continue;
                var field = line[..colon].Trim();
                var expr = line[(colon + 1)..].Trim();
                if (field.Length > 0 && expr.Length > 0) result[field] = expr;
            }
            return result;
        }

        private static Dictionary<string, string>? FromElement(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in el.EnumerateObject())
            {
                var expr = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.GetRawText(); // 非字符串值（少见）兜底取原文
                if (!string.IsNullOrWhiteSpace(expr)) result[prop.Name] = expr!.Trim();
            }
            return result;
        }

        // ─────────────────────────────────────────────
        // 类型判定
        // ─────────────────────────────────────────────

        /// <summary>按类型表达式校验单个字段值；返回违规描述，null = 通过。</summary>
        private static string? CheckType(string field, string expr, object value)
        {
            // 枚举：含 '|' ⇒ 字符串枚举，元素须逐字相等
            if (expr.Contains('|', StringComparison.Ordinal))
            {
                var allowed = expr.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var s = ValueAsString(value);
                if (s == null || !allowed.Contains(s, StringComparer.Ordinal))
                    return $"字段 `{field}` 越界：允许 [{string.Join(", ", allowed)}]，实际 {JsonFriendly(value)}";
                return null;
            }

            // 数组：以 '[]' 结尾
            if (expr.EndsWith("[]", StringComparison.Ordinal))
            {
                var elemExpr = expr[..^2];
                if (value is not IEnumerable enumerable || value is string)
                    return $"字段 `{field}` 类型不符：期望 {expr}，实际 {DescribeType(value)}";

                var idx = 0;
                foreach (var item in enumerable)
                {
                    if (item is null) { idx++; continue; }
                    var elemErr = CheckType($"{field}[{idx}]", elemExpr, item);
                    if (elemErr != null) return elemErr;
                    idx++;
                }
                return null;
            }

            // 基础类型
            return expr.ToLowerInvariant() switch
            {
                "string" => value is string ? null : Mismatch(field, expr, value),
                "number" or "int" or "integer" or "float" or "double" => IsNumber(value) ? null : Mismatch(field, expr, value),
                "boolean" or "bool" => value is bool ? null : Mismatch(field, expr, value),
                "any" => null,
                _ => $"字段 `{field}` 的类型表达式 `{expr}` 无法识别（只认 string/number/boolean/any + [] 数组 + a|b 枚举 + ? 可选）"
            };
        }

        private static string? Mismatch(string field, string expr, object value)
            => $"字段 `{field}` 类型不符：期望 {expr}，实际 {DescribeType(value)}（值 {JsonFriendly(value)}）";

        private static bool IsNumber(object value)
            => value is byte or sbyte or short or ushort or int or uint or long or ulong
               or float or double or decimal;

        /// <summary>
        /// 值 → 字符串（供枚举比对）。<c>JsonElement</c>（运行期最常见的装箱形态）必须能取出字符串。
        /// </summary>
        private static string? ValueAsString(object value)
        {
            if (value is string s) return s;
            if (value is JsonElement je)
                return je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText();
            return value?.ToString();
        }

        private static string DescribeType(object? value) => value switch
        {
            null => "null",
            string => "string",
            bool => "boolean",
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.Number => "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                _ => "unknown"
            },
            IDictionary<string, object> => "object",
            IEnumerable => "array",
            _ when IsNumber(value!) => "number",
            _ => value!.GetType().Name
        };

        /// <summary>违规信息里展示值：截断长文本，避免把整个大 JSON 塞进错误消息。</summary>
        private static string JsonFriendly(object value)
        {
            string text;
            try
            {
                text = value is JsonElement je ? je.GetRawText() : value.ToString() ?? "null";
            }
            catch
            {
                text = "(无法序列化)";
            }
            return text.Length <= 80 ? text : text[..77] + "...";
        }
    }
}
