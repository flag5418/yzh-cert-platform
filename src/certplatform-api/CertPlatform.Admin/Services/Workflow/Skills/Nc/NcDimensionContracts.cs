using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CertPlatform.Admin.Services.Workflow.Skills.Nc
{
    #region · 维度判定契约（nc_judge 输出 / nc_validate 校验共用）

    /// <summary>
    /// 单个维度的判定结果（由 nc_judge 节点产出，或由人工录入）
    /// </summary>
    public class DimensionResult
    {
        /// <summary>维度号（对应 cert_validation_rule.dimensions[].No）</summary>
        public int No { get; set; }

        /// <summary>维度名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// ★ 四态（★ 不是 bool）
        ///   conform      符合
        ///   nonconform   不符合
        ///   na           不适用
        ///   unverifiable 无法判断（材料不足）
        /// </summary>
        public string Conformity { get; set; } = DimensionConformity.Unverifiable;

        /// <summary>客观描述</summary>
        public string? Desc { get; set; }

        /// <summary>证据引用（文件名+位置）</summary>
        public string? Evidence { get; set; }

        /// <summary>AI 置信度</summary>
        public double? Confidence { get; set; }

        /// <summary>★ 过程代号（用于跨过程失效升级，无则不参与）</summary>
        public string? ProcessCode { get; set; }

        /// <summary>★ 该维度不符合时的严重度预设（来自 dimensions[].SeverityIfFailed）</summary>
        public string? SeverityIfFailed { get; set; }
    }

    /// <summary>四态枚举常量</summary>
    public static class DimensionConformity
    {
        public const string Conform      = "conform";
        public const string Nonconform   = "nonconform";
        public const string NotApplicable = "na";
        public const string Unverifiable = "unverifiable";

        public static readonly string[] All = { Conform, Nonconform, NotApplicable, Unverifiable };

        public static bool IsValid(string? v) =>
            !string.IsNullOrWhiteSpace(v) && All.Contains(v);
    }

    /// <summary>维度判定结果 JSON 解析（★ 严格模式，解析失败即报错，不静默取第一条）</summary>
    public static class DimensionResultParser
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling        = JsonCommentHandling.Skip,
            AllowTrailingCommas        = true
        };

        /// <summary>
        /// ★ 解析 nc_judge 节点的输出。
        /// <para><b>不合法直接抛异常</b>（18 号 §3.5 C1）—— 绝不静默取第一条，
        /// 那正是原 AggregateNcResult 的缺陷。</para>
        /// </summary>
        public static List<DimensionResult> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("维度判定结果为空");

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip
            });

            var root = doc.RootElement;
            // 兼容两种形态：{ dimensions: [...] } 或直接 [...]
            var arr = root.ValueKind == JsonValueKind.Array
                ? root
                : (root.TryGetProperty("dimensions", out var d) ? d : default);

            if (arr.ValueKind != JsonValueKind.Array)
                throw new FormatException("维度判定结果不是数组（期望 {dimensions:[...]} 或 [...]）");

            var list = new List<DimensionResult>();
            foreach (var item in arr.EnumerateArray())
            {
                var r = new DimensionResult
                {
                    No          = item.TryGetProperty("no", out var no) && no.TryGetInt32(out var n) ? n : 0,
                    Name        = Str(item, "name"),
                    Conformity  = Str(item, "conformity") ?? DimensionConformity.Unverifiable,
                    Desc        = Str(item, "desc"),
                    Evidence    = Str(item, "evidence"),
                    Confidence  = item.TryGetProperty("confidence", out var cf)
                                   && cf.ValueKind == JsonValueKind.Number ? cf.GetDouble() : null,
                    ProcessCode = Str(item, "processCode") ?? Str(item, "process_code"),
                    SeverityIfFailed = Str(item, "severityIfFailed") ?? Str(item, "severity_if_failed"),
                };

                // ★ C3：四态校验，非法值降级为 unverifiable + 保留原值供排查
                if (!DimensionConformity.IsValid(r.Conformity))
                    r.Desc = $"[枚举非法:{r.Conformity}] {r.Desc}";

                list.Add(r);
            }

            if (list.Count == 0)
                throw new FormatException("维度判定结果为空数组");

            return list;
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    #endregion
}
