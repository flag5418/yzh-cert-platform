using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// <b>AI 返回 JSON 的读取器</b> —— 三个 AI Skill（<c>src_semantic</c> / <c>src_ai_field</c> /
    /// <c>src_ai_table</c>）<b>共用同一份</b>取值逻辑（39 号 §12.5 的「按段分派」）。
    ///
    /// <para><b>★ 为什么必须唯一</b>：三段（<c>semantic</c> / <c>fields</c> / <c>tables</c>）的
    /// 结构不同（string / 对象 / 数组），若各 Skill 各写一份取值，必然出现
    /// 「A 段能取到、B 段取不到」的静默不一致。</para>
    ///
    /// <para><b>★ 为什么必须有 <see cref="NormalizeJson"/></b>（39 号 §12.1 经验 3）：
    /// <c>SkillExecutor.ConvertValue</c> 只转基础类型，<c>JsonElement</c> 会被<b>原样返回</b> ⇒
    /// 下游 <c>FillValueFactory.Coerce</c> 拿到的还是 <c>JsonElement</c>，<b>取不到值</b>。
    /// 深转换是「反射调用」与「强类型」之间唯一的桥。</para>
    ///
    /// <para><b>★ 键名容错</b>：模型偶发改变大小写（<c>Fields</c> / <c>Value</c>）。
    /// 精确匹配失败后回退<b>大小写不敏感</b>匹配 —— 但<b>只做一次回退</b>，
    /// ⛔ 不做模糊匹配（那会把 <c>source_doc</c> 和 <c>source</c> 混起来）。</para>
    /// </summary>
    public static class AiFillJsonReader
    {
        /// <summary>输出 JSON 的三段名（39 号 §12.4）</summary>
        public const string SectionSemantic = "semantic";
        public const string SectionFields = "fields";
        public const string SectionTables = "tables";

        /// <summary>
        /// <c>JsonElement</c> → CLR 对象（对象 ⇒ <c>Dictionary&lt;string, object?&gt;</c>，
        /// 数组 ⇒ <c>List&lt;object?&gt;</c>，其余 ⇒ 基元）。
        /// </summary>
        public static object? NormalizeJson(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => NormalizeJson(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => e.EnumerateArray().Select(NormalizeJson).ToList(),
            JsonValueKind.String => e.GetString() ?? string.Empty,
            // ★ 必须显式 `(object)` 强转 —— 否则三元表达式的类型会被**提升为 double**，
            //   `TryGetInt64` 成功时 long 也被转成 double 装箱 ⇒ 大整数丢精度且**不报错**
            //   （`LlmExtractSkill.NormalizeJson` 用的就是这个正确写法）。
            JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

        /// <summary>把 <c>JsonDocument</c> 根转成 <c>Dictionary</c>（根不是对象 ⇒ 返回 null）</summary>
        public static Dictionary<string, object>? NormalizeRoot(JsonDocument? doc)
        {
            if (doc == null) return null;
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var p in root.EnumerateObject())
                dict[p.Name] = NormalizeJson(p.Value)!;
            return dict;
        }

        /// <summary>
        /// 读 <c>{section}.{key}</c>（返回<b>对象节点</b>）。
        /// <para>如 <c>ReadObject(root, "fields", "ENT_NAME")</c> ⇒
        /// <c>{ value, confidence, source_doc, note }</c>。</para>
        /// </summary>
        public static Dictionary<string, object>? ReadObject(
            Dictionary<string, object>? root, string section, string key)
        {
            var seg = ReadSection(root, section);
            if (seg == null) return null;

            if (!TryGet(seg, key, out var node)) return null;
            return AsDictionary(node);
        }

        /// <summary>
        /// 读 <c>{section}.{key}</c> 并<b>直接当值</b>用（<c>semantic</c> 段就是这种形态：
        /// <c>{"ENT_NAME": "改写后的文本"}</c>）。
        /// </summary>
        public static string? ReadSectionValue(Dictionary<string, object>? root, string section, string key)
        {
            var seg = ReadSection(root, section);
            if (seg == null) return null;
            return TryGet(seg, key, out var v) ? AsString(v) : null;
        }

        /// <summary>取某一段（<c>semantic</c> / <c>fields</c> / <c>tables</c>）为字典</summary>
        public static Dictionary<string, object>? ReadSection(Dictionary<string, object>? root, string section)
        {
            if (root == null) return null;
            if (!TryGet(root, section, out var seg)) return null;
            return AsDictionary(seg);
        }

        /// <summary>
        /// 读 <c>{section}.{key}</c> 的<b>原始节点</b>（可能是数组 / 对象 / 标量）。
        ///
        /// <para>★ <c>tables</c> 段必须走这个方法：<c>tables.{tag}</c> 是<b>行数组</b>
        /// （39 号 §12.4），而 <see cref="ReadSectionValue"/> 只处理标量、<see cref="ReadObject"/>
        /// 只处理对象 ⇒ 用错方法会得到「取不到」而不是报错。</para>
        /// </summary>
        public static object? ReadSectionRaw(Dictionary<string, object>? root, string section, string key)
        {
            var seg = ReadSection(root, section);
            if (seg == null) return null;
            return TryGet(seg, key, out var v) ? v : null;
        }

        /// <summary>从对象节点里读一个<b>原始值</b>（可能是 string / number / bool / null）</summary>
        public static object? ReadRaw(Dictionary<string, object>? node, string key)
        {
            if (node == null) return null;
            return TryGet(node, key, out var v) ? v : null;
        }

        /// <summary>从对象节点里读一个字符串（数字 / 布尔会被转成字符串；null 原样返回）</summary>
        public static string? ReadString(Dictionary<string, object>? node, string key)
        {
            var raw = ReadRaw(node, key);
            return raw == null ? null : AsString(raw);
        }

        /// <summary>读置信度（<c>confidence</c>，0~1）。缺失 ⇒ <c>null</c>（⛔ 不默认 1.0，那会掩盖模型未给）</summary>
        public static double? ReadConfidence(Dictionary<string, object>? node, string key = "confidence")
        {
            var raw = ReadRaw(node, key);
            if (raw == null) return null;

            if (raw is double d) return Clamp(d);
            if (raw is long l) return Clamp(l);
            if (raw is int i) return Clamp(i);
            if (raw is decimal m) return Clamp((double)m);

            var s = raw.ToString();
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? Clamp(parsed)
                : null;
        }

        /// <summary>值 → 字符串（数值走 InvariantCulture，⛔ 不用当前区域性 —— 否则小数点在德语区会变逗号）</summary>
        public static string AsString(object? raw) => raw switch
        {
            null => string.Empty,
            string s => s,
            bool b => b ? "true" : "false",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            _ => raw.ToString() ?? string.Empty,
        };

        /// <summary>取值为字典（<c>Dictionary&lt;string, object&gt;</c> 与 <c>Dictionary&lt;string, object?&gt;</c> 视为同一物）</summary>
        private static Dictionary<string, object>? AsDictionary(object? node)
        {
            if (node is Dictionary<string, object> d1) return d1;

            if (node is Dictionary<string, object?> d2)
            {
                var copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var kv in d2) copy[kv.Key] = kv.Value!;
                return copy;
            }
            return null;
        }

        /// <summary>
        /// 精确匹配 → 大小写不敏感回退（<b>只回退一次</b>）。
        /// <para>⚠️ 用 <c>OrdinalIgnoreCase</c> 而非区域性比较 —— 锚点编码是 ASCII 标识符。</para>
        /// </summary>
        private static bool TryGet(Dictionary<string, object> dict, string key, out object? value)
        {
            if (dict.TryGetValue(key, out value)) return true;

            foreach (var kv in dict)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static double Clamp(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
