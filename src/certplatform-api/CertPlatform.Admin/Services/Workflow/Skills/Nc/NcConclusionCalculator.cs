using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CertPlatform.Admin.Services.Workflow.Skills.Nc
{
    #region · 输入 / 输出契约

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

    /// <summary>未检查项处理策略（★ 常量类加 Nc 前缀，避免与属性名冲突）</summary>
    public static class NcUnverifiedPolicy
    {
        /// <summary>封顶到中间档（存在无法判断的维度，不能给「完全符合」）</summary>
        public const string CapBasic = "cap_basic";
        /// <summary>直接判不符合</summary>
        public const string Reject = "reject";
        /// <summary>不处理</summary>
        public const string Ignore = "ignore";
    }

    /// <summary>结论生成模式（★ 常量类加 Nc 前缀）</summary>
    public static class NcConclusionMode
    {
        /// <summary>仅规则计算，不调 AI</summary>
        public const string RuleOnly = "RULE_ONLY";
        /// <summary>规则算结论 + AI 写理由（默认）</summary>
        public const string AiAssisted = "AI_ASSISTED";
        /// <summary>专家全人工</summary>
        public const string ExpertManual = "EXPERT_MANUAL";
    }

    /// <summary>结论计算结果（★ 权威，不含 AI 参与）</summary>
    public class ConclusionResult
    {
        /// <summary>★ 结论词（取自规则的 LevelFull/LevelBasic/LevelReject）</summary>
        public string Conclusion { get; set; } = string.Empty;

        /// <summary>结论档位序号（0=最高档），用于降级比较</summary>
        public int ConclusionLevel { get; set; }

        /// <summary>条款级判定：conform | nonconform | na</summary>
        public string Conformity { get; set; } = DimensionConformity.Conform;

        /// <summary>严重度：major | minor | observation | null</summary>
        public string? Severity { get; set; }

        /// <summary>★ 依据维度号</summary>
        public List<int> Basis { get; set; } = new();

        /// <summary>★ 未检查维度号（unverifiable）</summary>
        public List<int> Unverified { get; set; } = new();

        /// <summary>★ 降档原因（哪条规则导致的）</summary>
        public string? DowngradeReason { get; set; }

        /// <summary>★ 跨过程失效命中详情（过程代号 → 维度号）</summary>
        public Dictionary<string, List<int>> CrossProcessFailures { get; set; } = new();

        /// <summary>统计（供界面与导出）</summary>
        public ConclusionStats Stats { get; set; } = new();

        /// <summary>★ 是否需要专家签署（由 rule.ManualRequired 决定）</summary>
        public bool ManualRequired { get; set; }

        /// <summary>★ 结论生成模式（规则快照，便于事后回溯当时用的哪种模式）</summary>
        public string Mode { get; set; } = NcConclusionMode.AiAssisted;

        /// <summary>★ 规则来源（标准号+条款+版本，审计用）</summary>
        public string? RuleSource { get; set; }
    }

    /// <summary>统计</summary>
    public class ConclusionStats
    {
        public int Total    { get; set; }
        public int Conform  { get; set; }
        public int Nonconform { get; set; }
        public int NotApplicable { get; set; }
        public int Unverifiable  { get; set; }
    }

    #endregion

    /// <summary>
    /// ★ NC 认证结论计算器（★ 纯代码 · 不涉及 AI · 权威结论）
    ///
    /// <para><b>设计原则</b>（18 号 §3.3）：
    /// AI 负责"提取事实"，规则负责"判定结论"。本类是唯一的结论来源，
    /// AI 产出的结论一律不采纳（见 <see cref="NcValidateSkill"/> 的 C7 校验）。</para>
    ///
    /// <para><b>判定顺序（★ 不可颠倒）</b>：
    ///   ① 严重不符合 → 不符合
    ///   ② 一般不符合 ≥ MinorRejectThreshold → 不符合
    ///   ③ 一般不符合 ≥ MinorToBasicThreshold → 基本符合
    ///   ④ 存在未检查项 → 按 UnverifiedPolicy 处理
    ///   ⑤ 全部符合 → 完全符合</para>
    /// </summary>
    public class NcConclusionCalculator
    {
        /// <summary>
        /// ★ 权威结论计算。AI 不得参与此项判定。
        /// </summary>
        /// <param name="dimensions">各维度判定结果（可来自 nc_judge 节点或人工录入）</param>
        /// <param name="rule">判定规则（来自 cert_conclusion_rule）</param>
        public ConclusionResult Calculate(
            IReadOnlyList<DimensionResult> dimensions,
            ConclusionRuleSnapshot rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            var result = new ConclusionResult
            {
                ManualRequired = rule.ManualRequired,
                Mode           = rule.ConclusionMode,
                RuleSource     = rule.SourceRef
            };

            if (dimensions == null || dimensions.Count == 0)
            {
                // ★ 无维度结果 → 不能给任何正向结论
                result.Conclusion      = rule.LevelReject;
                result.ConclusionLevel = 2;
                result.Conformity      = DimensionConformity.Nonconform;
                result.Severity        = "major";
                result.DowngradeReason = "无任何维度判定结果";
                result.Stats           = new ConclusionStats();
                return result;
            }

            // ── ① 统计 ──
            var nonconform   = dimensions.Where(d => d.Conformity == DimensionConformity.Nonconform).ToList();
            var unverifiable = dimensions.Where(d => d.Conformity == DimensionConformity.Unverifiable).ToList();

            result.Stats = new ConclusionStats
            {
                Total          = dimensions.Count,
                Conform        = dimensions.Count(d => d.Conformity == DimensionConformity.Conform),
                Nonconform     = nonconform.Count,
                NotApplicable  = dimensions.Count(d => d.Conformity == DimensionConformity.NotApplicable),
                Unverifiable   = unverifiable.Count
            };
            result.Basis      = nonconform.Select(d => d.No).OrderBy(x => x).ToList();
            result.Unverified = unverifiable.Select(d => d.No).OrderBy(x => x).ToList();

            // ── ② 条款级判定（与认证结论分属两个维度，不要混）──
            //    有任一 nonconform → 条款不符合；否则若全为 na → 不适用；否则符合
            if (nonconform.Count > 0)
            {
                result.Conformity = DimensionConformity.Nonconform;
            }
            else if (result.Stats.Conform == 0 && result.Stats.NotApplicable > 0)
            {
                result.Conformity = DimensionConformity.NotApplicable;
            }
            else
            {
                result.Conformity = DimensionConformity.Conform;
            }

            // ── ③ 严重不符合：显式 major 或跨过程失效 ──
            bool hasExplicitMajor = nonconform.Any(d => d.SeverityIfFailed == SeverityCodes.Major);
            var crossFailures = rule.CrossProcessEnabled
                ? DetectCrossProcessFailure(nonconform, rule.CrossProcessThreshold)
                : new Dictionary<string, List<int>>();

            if (hasExplicitMajor || crossFailures.Count > 0)
            {
                result.CrossProcessFailures = crossFailures;
                return Build(result, rule,
                    rule.LevelReject, 2,
                    severity: "major",
                    reason: hasExplicitMajor
                        ? "存在维度严重不符合"
                        : $"同过程多维度失效（{string.Join("、", crossFailures.Keys)}）");
            }

            // ── ④ 一般不符合累计 ──
            if (nonconform.Count >= rule.MinorRejectThreshold)
            {
                return Build(result, rule, rule.LevelReject, 2,
                    severity: "minor",
                    reason: $"一般不符合 {nonconform.Count} 项，达到拒绝阈值 {rule.MinorRejectThreshold}");
            }

            if (nonconform.Count >= rule.MinorToBasicThreshold)
            {
                return Build(result, rule, rule.LevelBasic, 1,
                    severity: "minor",
                    reason: $"一般不符合 {nonconform.Count} 项，达到降档阈值 {rule.MinorToBasicThreshold}");
            }

            // ── ⑤ 未检查项处理 ──
            if (unverifiable.Count > 0)
            {
                switch (rule.UnverifiedPolicy)
                {
                    case NcUnverifiedPolicy.Reject:
                        return Build(result, rule, rule.LevelReject, 2,
                            severity: "minor",
                            reason: $"存在 {unverifiable.Count} 项无法判断，按规则直接判不符合");

                    case NcUnverifiedPolicy.Ignore:
                        // 不处理，继续往下
                        break;

                    case NcUnverifiedPolicy.CapBasic:
                    default:
                        // ★ 封顶到中间档（不能给「完全符合」）
                        if (nonconform.Count > 0)
                        {
                            return Build(result, rule, rule.LevelBasic, 1,
                                severity: "minor",
                                reason: $"存在 {unverifiable.Count} 项无法判断，结论封顶到中间档");
                        }
                        return Build(result, rule, rule.LevelBasic, 1,
                            severity: null,
                            reason: $"存在 {unverifiable.Count} 项无法判断，无法给出完全符合");
                }
            }

            // ── ⑥ 全部符合 ──
            return Build(result, rule, rule.LevelFull, 0,
                severity: null, reason: "所有检查维度均符合");
        }

        /// <summary>
        /// ★ 跨过程失效检测（★ 单节点多维度范式独有能力）
        /// <para>同一过程（ProcessCode 相同）下有 ≥ N 个维度不符合 → 该过程整体失效 → 升级为严重不符合。</para>
        /// <para>典型场景：7.5 成文信息下「文件审批记录」+「文件发放记录」同时缺失
        /// —— 孤立看是 2 个一般不符合，关联看是★文件控制过程整体失效。</para>
        /// <para>无 ProcessCode 的维度不参与此判定。</para>
        /// </summary>
        public Dictionary<string, List<int>> DetectCrossProcessFailure(
            IReadOnlyList<DimensionResult> nonconform, int threshold)
        {
            var result = new Dictionary<string, List<int>>();
            if (threshold <= 0) return result;

            foreach (var group in nonconform
                         .Where(d => !string.IsNullOrWhiteSpace(d.ProcessCode))
                         .GroupBy(d => d.ProcessCode!))
            {
                if (group.Count() >= threshold)
                    result[group.Key] = group.Select(d => d.No).OrderBy(x => x).ToList();
            }
            return result;
        }

        private static ConclusionResult Build(
            ConclusionResult result, ConclusionRuleSnapshot rule,
            string conclusion, int level, string? severity, string reason)
        {
            result.Conclusion      = conclusion;
            result.ConclusionLevel = level;
            result.Severity        = severity;
            result.DowngradeReason = reason;
            return result;
        }
    }

    /// <summary>严重度常量</summary>
    public static class SeverityCodes
    {
        public const string Major       = "major";
        public const string Minor       = "minor";
        public const string Observation = "observation";
    }

    /// <summary>
    /// 判定规则快照（与实体解耦，便于单测；由 Repository 从 cert_conclusion_rule 投影）
    /// </summary>
    public class ConclusionRuleSnapshot
    {
        public string OrgCode { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public string? PhaseCode { get; set; }

        public string LevelFull   { get; set; } = "完全符合";
        public string LevelBasic  { get; set; } = "基本符合";
        public string LevelReject { get; set; } = "不符合";

        public int MinorToBasicThreshold { get; set; } = 3;
        public int MinorRejectThreshold  { get; set; } = 5;

        public bool CrossProcessEnabled   { get; set; } = true;
        public int  CrossProcessThreshold { get; set; } = 2;

        public string UnverifiedPolicy { get; set; } = NcUnverifiedPolicy.CapBasic;
        public string ConclusionMode  { get; set; } = NcConclusionMode.AiAssisted;
        public bool   ManualRequired  { get; set; }
        public int    OverrideAlertThreshold { get; set; } = 30;

        public string? JudgePromptCode    { get; set; }
        public string? ConcludePromptCode { get; set; }
        public string? SourceRef          { get; set; }
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
}
