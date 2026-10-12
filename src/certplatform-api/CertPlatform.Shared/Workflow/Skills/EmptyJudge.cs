using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CertPlatform.Shared.Workflow.Skills
{
    public static class EmptyJudge
    {
        /// <summary>空值判定结果</summary>
        public sealed class Result
        {
            /// <summary>★ 三态：has_value=有值 | is_empty=有记录但值为空 | not_found=无该记录</summary>
            public string State { get; init; } = "not_found";

            /// <summary>★ 便捷布尔：有值（可用于 compare/branch）</summary>
            public bool HasValue => State == "has_value";

            /// <summary>★ 便捷布尔：空（is_empty 或 not_found 都算"取不到可用值"）</summary>
            public bool IsEmpty => State != "has_value";

            public string? RawValue { get; init; }
            public string? Label { get; init; }
            public string? Message { get; init; }
            public int RecordCount { get; init; }
            public double? Confidence { get; init; }
            public string? SourceFileCode { get; init; }
            public int? SourceVersion { get; init; }

            public Dictionary<string, object> ToOutputs()
            {
                var o = new Dictionary<string, object>
                {
                    // ★ 三个布尔端口：覆盖不同连线性情
                    ["result"]    = HasValue,   // 布尔：可直接喂 branch / compare
                    ["is_empty"]  = IsEmpty,
                    ["has_value"] = HasValue,
                    ["state"]     = State
                };
                if (RawValue     != null) o["raw_value"]       = RawValue;
                if (Label        != null) o["label"]           = Label;
                if (Message      != null) o["message"]         = Message;
                if (Confidence   != null) o["confidence"]      = Confidence.Value;
                if (SourceFileCode != null) o["source_file_code"] = SourceFileCode;
                if (SourceVersion  != null) o["source_version"]   = SourceVersion.Value;
                o["record_count"] = RecordCount;
                return o;
            }
        }

        public const string StateHasValue = "has_value";
        public const string StateIsEmpty  = "is_empty";
        public const string StateNotFound = "not_found";

        /// <summary>文本值是否算「空」</summary>
        public static bool IsBlankText(string? v)
        {
            if (v == null) return true;
            var t = v.Trim();
            if (t.Length == 0) return true;

            // ★ 常见的"有值但等于没有"的表现形态
            var placeholders = new[]
            {
                "-", "--", "—", "－", "/", "\\", "N/A", "n/a", "NA", "na",
                "无", "沒有", "暂无", "待定", "待填写", "未填写", "未提供", "未上传",
                "空", "null", "NULL", "undefined", "{}", "[]", "暂无数据", "无内容"
            };
            if (placeholders.Contains(t, StringComparer.OrdinalIgnoreCase)) return true;

            // ★ 纯符号也算空（★含全角变体：中文文档里 -—／／、· 等很常见）
            if (t.All(c => "-—–－_/\\／|｜·・、,，.．。~～ 　\t".Contains(c))) return true;

            // 明显的占位提示
            if (Regex.IsMatch(t, @"^(待|需要|请)\S{0,6}(填写|补充|上传|提供)", RegexOptions.None)) return true;

            return false;
        }

        /// <summary>把三态结果落成提示文案</summary>
        public static string Describe(string what, Result r) => r.State switch
        {
            StateHasValue => $"{what}：有值（{Truncate(r.RawValue, 60)}）",
            StateIsEmpty  => $"{what}：★已提取但内容为空",
            _              => r.Message ?? $"{what}：★未找到该记录"
        };

        public static string Truncate(string? s, int n)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");
    }
}
