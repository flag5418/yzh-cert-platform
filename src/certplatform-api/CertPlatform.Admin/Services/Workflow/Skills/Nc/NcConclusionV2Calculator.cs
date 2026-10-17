using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace CertPlatform.Admin.Services.Workflow.Skills.Nc
{
    /// <summary>
    /// ★ NC 多 judge 结论判定 v2（纯代码 · 决策矩阵 · ★ 不涉及 AI）
    ///
    /// <para><b>背景（2026-10-14 用户裁决）</b>：NC 检查由**多个确定性 nc_judge 节点**
    /// （引用文档字段 / 材料存在性 / 规则计算）分别产出判定，经
    /// <c>stamp_judge_level</c>（盖级别）+ <c>collect_judges</c>（聚合）送入本计算器。
    /// 计算器读 <c>cert_conclusion_rule</c> 的 v2 配置列（CoreLevels / MinorRejectThreshold /
    /// MinorTolerated / ConclusionLabels / UnverifiedPolicy）算出<b>权威总结论</b>。</para>
    ///
    /// <para><b>为什么必须是本地代码而不是 AI</b>：「哪些级别影响结论、几条不过判什么」
    /// 是专家确定的<b>决策矩阵</b> ⇒ 落成配置表，机器执行。让 AI 读提示词做这件事，
    /// 同输入两次可能出不同结论，认证审计无法接受。本类是链路唯一的结论来源
    /// （v1 维度级计算器 NcConclusionCalculator 已于 2026-10-16 随 v1 Skill 一并删除）。</para>
    ///
    /// <para><b>判定矩阵（★ 顺序不可颠倒）</b>：</para>
    /// <list type="number">
    /// <item>存在 major（严重不符合）fail ⇒ <c>fail</c>（不通过 · 取消审核）</item>
    /// <item>unverifiable &gt; 0 且 UnverifiedPolicy=reject ⇒ <c>fail</c></item>
    /// <item>core 级（CoreLevels 命中，不含 major）fail ≥ MinorRejectThreshold ⇒ <c>improvement_required</c>（需改进后复审）</item>
    /// <item>core 级 fail ≥ 1 ⇒ <c>pass_with_nc</c>（通过 · 带不符合项）</item>
    /// <item>仅 observation 级 fail，且 MinorTolerated ≥ 0 且超过容忍数 ⇒ <c>pass_with_nc</c>
    ///       （MinorTolerated = -1 默认不限量 ⇒ 2 条轻微不过仍 <c>pass</c>，用户 2026-10-14 示例）</item>
    /// <item>unverifiable &gt; 0 且 UnverifiedPolicy=cap_basic ⇒ 封顶：结论不得优于 <c>pass_with_nc</c></item>
    /// <item>其余 ⇒ <c>pass</c>（推荐认证）</item>
    /// </list>
    ///
    /// <para><b>缺省方向（安全侧）</b>：judge 未盖级别（Level 空/未知）⇒ 按 <c>major</c> 计算
    /// 并在 stats.levels_defaulted 计数 —— 漏配一条 = 按最严处理，⛔ 不能按「不影响」漏过。</para>
    /// </summary>

    #region 常量

    /// <summary>v2 结论代码 / 级别 / 判定 常量</summary>
    public static class NcJudgeConstants
    {
        // ── 总结论代码（与结论字典 Code 对齐）──
        public const string ConclusionPass = "pass";                            // 通过（推荐认证）
        public const string ConclusionPassWithNc = "pass_with_nc";             // 通过（带不符合项）
        public const string ConclusionImprovementRequired = "improvement_required"; // 待改进后复审
        public const string ConclusionFail = "fail";                           // 不通过（严重不符合）

        // ── 级别（judge 盖章用；critical 并入 major 计算，单独计数展示）──
        public const string LevelCritical = "critical";   // 极严重（并入 major 判定）
        public const string LevelMajor = "major";         // 严重不符合
        public const string LevelMinor = "minor";         // 一般不符合
        public const string LevelObservation = "observation"; // 轻微/观察项
        public const string LevelNone = "none";           // 显式「不影响最终结论」

        /// <summary>默认核心级（CoreLevels 未配置时的安全侧缺省）</summary>
        public static readonly string[] DefaultCoreLevels = { LevelMajor, LevelMinor };

        // ── 单条 judge 判定 ──
        public const string VerdictPass = "pass";
        public const string VerdictFail = "fail";
        public const string VerdictUnverifiable = "unverifiable";

        /// <summary>缺省级别（judge 漏盖级别时按此计算）</summary>
        public const string DefaultLevel = LevelMajor;

        /// <summary>UnverifiedPolicy（复用 v1 常量值域）</summary>
        public const string UnverifiedReject = "reject";
        public const string UnverifiedCap = "cap_basic";
        public const string UnverifiedIgnore = "ignore";
    }

    #endregion

    #region 契约

    /// <summary>单条 judge 结果（stamp_judge_level 盖章后的标准形状，collect_judges 聚合为数组）</summary>
    public class JudgeResult
    {
        /// <summary>judge 标识（如 J1 / rule_xxx），审计展示用</summary>
        public string JudgeCode { get; set; } = string.Empty;

        /// <summary>级别：critical / major / minor / observation / none（★ 缺省按 major 算）</summary>
        public string Level { get; set; } = NcJudgeConstants.DefaultLevel;

        /// <summary>判定：pass / fail / unverifiable</summary>
        public string Verdict { get; set; } = NcJudgeConstants.VerdictUnverifiable;

        /// <summary>失败原因 / 证据（报告阶段叙述素材）</summary>
        public string? Reason { get; set; }

        /// <summary>★ 上游 nc_judge 的原始结果（标量/对象/数组原样保留，报告阶段可回看）</summary>
        public object? Payload { get; set; }

        /// <summary>是否按缺省级别计算（漏盖级别 = true，供审计排查）</summary>
        public bool LevelDefaulted { get; set; }
    }

    /// <summary>统计（供界面与导出）</summary>
    public class JudgeStats
    {
        public int Total { get; set; }
        public int Pass { get; set; }
        public int Fail { get; set; }
        public int Unverifiable { get; set; }

        /// <summary>按级别计数的 fail（critical / major / minor / observation / none）</summary>
        public Dictionary<string, int> FailByLevel { get; set; } = new();

        /// <summary>核心级（CoreLevels 命中，含 major）fail 数</summary>
        public int CoreFail { get; set; }

        /// <summary>漏盖级别按缺省 major 计算的条数（>0 = 配置有缺口）</summary>
        public int LevelsDefaulted { get; set; }
    }

    /// <summary>v2 计算结果（权威 · 纯函数产出 · 不含 AI）</summary>
    public class JudgeConclusionResult
    {
        /// <summary>★ 权威总结论代码（pass / pass_with_nc / improvement_required / fail）</summary>
        public string Conclusion { get; set; } = NcJudgeConstants.ConclusionFail;

        /// <summary>结论档位序号（0=最优，用于降级比较）</summary>
        public int ConclusionLevel { get; set; }

        /// <summary>结论词（取自规则 ConclusionLabels，缺省用内置中文词）</summary>
        public string ConclusionLabel { get; set; } = string.Empty;

        /// <summary>★ 降档原因（哪一步规则触发的，审计可读）</summary>
        public string? DowngradeReason { get; set; }

        /// <summary>原样 judge 数组（★ nc_conclude / 报告阶段直接消费）</summary>
        public List<JudgeResult> Judges { get; set; } = new();

        /// <summary>统计</summary>
        public JudgeStats Stats { get; set; } = new();

        /// <summary>★ 规则快照（当时用的哪版判定矩阵，事后可回溯）</summary>
        public Dictionary<string, object?> RuleSnapshot { get; set; } = new();

        /// <summary>规则来源（cert_conclusion_rule.SourceRef）</summary>
        public string? RuleSource { get; set; }

        /// <summary>是否需专家签署（沿用规则 ManualRequired）</summary>
        public bool ManualRequired { get; set; }
    }

    /// <summary>v2 判定矩阵（与规则实体解耦，便于单测；由 skill 从 cert_conclusion_rule 投影）</summary>
    public class JudgeConclusionMatrix
    {
        /// <summary>结论词映射：pass/pass_with_nc/improvement_required/fail → 中文词；缺省用内置词</summary>
        public Dictionary<string, string>? ConclusionLabels { get; set; }

        /// <summary>哪些级别算「核心、影响结论」（缺省 = [major, minor]）</summary>
        public List<string> CoreLevels { get; set; } = new(NcJudgeConstants.DefaultCoreLevels);

        /// <summary>核心级 fail ≥ N ⇒ 升级 improvement_required</summary>
        public int MinorRejectThreshold { get; set; } = 2;

        /// <summary>非核心（observation）fail 容忍数；-1 = 不限量（默认）</summary>
        public int MinorTolerated { get; set; } = -1;

        /// <summary>unverifiable 处理：reject / cap_basic / ignore（缺省 cap_basic）</summary>
        public string UnverifiedPolicy { get; set; } = NcJudgeConstants.UnverifiedCap;

        /// <summary>是否需专家签署</summary>
        public bool ManualRequired { get; set; }

        /// <summary>规则来源</summary>
        public string? RuleSource { get; set; }
    }

    /// <summary>judges_json 严格解析（★ 解析失败 / 枚举非法直接抛 FormatException，不静默）</summary>
    public static class JudgeResultParser
    {
        private static readonly string[] Levels =
            { NcJudgeConstants.LevelCritical, NcJudgeConstants.LevelMajor,
              NcJudgeConstants.LevelMinor, NcJudgeConstants.LevelObservation, NcJudgeConstants.LevelNone };
        private static readonly string[] Verdicts =
            { NcJudgeConstants.VerdictPass, NcJudgeConstants.VerdictFail, NcJudgeConstants.VerdictUnverifiable };

        /// <summary>
        /// 解析 collect_judges 的 <c>judges_json</c>（数组 或 {judges:[...]}）。
        /// 兼容元素为「原生字段」或「JSON 字符串」两种形态。
        /// </summary>
        public static List<JudgeResult> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("judges 数组为空（collect_judges 未产出任何 judge 结果）");

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var root = doc.RootElement;
            var arr = root.ValueKind == JsonValueKind.Array
                ? root
                : (root.TryGetProperty("judges", out var j) && j.ValueKind == JsonValueKind.Array ? j : default);

            if (arr.ValueKind != JsonValueKind.Array)
                throw new FormatException("judges 不是数组（期望 [...] 或 {judges:[...]}）");

            var list = new List<JudgeResult>();
            foreach (var item in arr.EnumerateArray())
            {
                // 兼容：元素本身是字符串（嵌套 JSON）
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (string.IsNullOrWhiteSpace(s))
                        throw new FormatException("judges 中存在空字符串元素");
                    list.Add(ParseOne(JsonDocument.Parse(s).RootElement));
                    continue;
                }
                if (item.ValueKind != JsonValueKind.Object)
                    throw new FormatException($"judges 元素既非对象也非 JSON 字符串（实际 {item.ValueKind}）");
                list.Add(ParseOne(item));
            }

            if (list.Count == 0)
                throw new FormatException("judges 数组为空");

            return list;
        }

        private static JudgeResult ParseOne(JsonElement e)
        {
            var level = Str(e, "level");
            var verdict = (Str(e, "verdict") ?? string.Empty).Trim().ToLowerInvariant();
            if (!Levels.Contains(level, StringComparer.OrdinalIgnoreCase))
                throw new FormatException($"judge '{Str(e, "judge_code")}' 的级别非法：{level ?? "(空)"}（允许 {string.Join("/", Levels)}）");
            if (!Verdicts.Contains(verdict))
                throw new FormatException($"judge '{Str(e, "judge_code")}' 的判定非法：{verdict ?? "(空)"}（允许 pass/fail/unverifiable）");

            return new JudgeResult
            {
                JudgeCode    = Str(e, "judge_code") ?? Str(e, "judgeCode") ?? string.Empty,
                Level        = level!,
                Verdict      = verdict,
                Reason       = Str(e, "reason"),
                LevelDefaulted = false
            };
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
    }

    #endregion

    /// <summary>★ 纯函数计算器（同输入必同输出 · 可复现 · 审计可信）</summary>
    public class NcConclusionV2Calculator
    {
        /// <summary>结论档位顺序（0=最优）</summary>
        public static readonly Dictionary<string, int> ConclusionRank = new()
        {
            [NcJudgeConstants.ConclusionPass] = 0,
            [NcJudgeConstants.ConclusionPassWithNc] = 1,
            [NcJudgeConstants.ConclusionImprovementRequired] = 2,
            [NcJudgeConstants.ConclusionFail] = 3
        };

        /// <summary>缺省结论词（规则未配 ConclusionLabels 时用）</summary>
        public static readonly Dictionary<string, string> DefaultConclusionLabels = new()
        {
            [NcJudgeConstants.ConclusionPass] = "通过（推荐认证）",
            [NcJudgeConstants.ConclusionPassWithNc] = "通过（带不符合项）",
            [NcJudgeConstants.ConclusionImprovementRequired] = "待改进后复审",
            [NcJudgeConstants.ConclusionFail] = "不通过"
        };

        /// <summary>
        /// ★ 判定兜底推断（★ 确定性，不引入 AI）—— stamp_judge_level 未显式给 verdict 时、
        /// 以及 collect_judges 拿到裸值（未盖 stamp）时，据「值本身」推断单条 judge 的判定：
        /// <list type="bullet">
        /// <item>null / 空白串 / 空集合 / JsonNull ⇒ <c>unverifiable</c>（取不到材料）</item>
        /// <item>true / 布尔真 / JsonTrue ⇒ <c>pass</c></item>
        /// <item>false / JsonFalse ⇒ <c>fail</c></item>
        /// <item>其余非空值 ⇒ <c>fail</c>（有内容 = 检出了问题）</item>
        /// </list>
        /// 同一值必同判定，保证可复现。
        /// </summary>
        public static string InferVerdict(object? value)
        {
            if (value == null) return NcJudgeConstants.VerdictUnverifiable;
            if (value is string s)
                return string.IsNullOrWhiteSpace(s) ? NcJudgeConstants.VerdictUnverifiable
                                                     : NcJudgeConstants.VerdictFail;
            if (value is bool b)
                return b ? NcJudgeConstants.VerdictPass : NcJudgeConstants.VerdictFail;
            if (value is System.Collections.ICollection c)
                return c.Count == 0 ? NcJudgeConstants.VerdictUnverifiable
                                    : NcJudgeConstants.VerdictFail;
            if (value is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.Null => NcJudgeConstants.VerdictUnverifiable,
                    JsonValueKind.False => NcJudgeConstants.VerdictFail,
                    JsonValueKind.True => NcJudgeConstants.VerdictPass,
                    JsonValueKind.String => string.IsNullOrWhiteSpace(je.GetString())
                        ? NcJudgeConstants.VerdictUnverifiable
                        : NcJudgeConstants.VerdictFail,
                    _ => NcJudgeConstants.VerdictFail
                };
            }
            return NcJudgeConstants.VerdictFail;
        }

        /// <summary>
        /// ★ 权威结论计算。AI 不得参与。
        /// </summary>
        /// <param name="judges">经 stamp_judge_level 盖章的 judge 结果数组</param>
        /// <param name="matrix">判定矩阵（cert_conclusion_rule 投影，null = 全缺省）</param>
        public JudgeConclusionResult Calculate(IReadOnlyList<JudgeResult> judges, JudgeConclusionMatrix? matrix = null)
        {
            matrix ??= new JudgeConclusionMatrix();

            var result = new JudgeConclusionResult
            {
                ManualRequired = matrix.ManualRequired,
                RuleSource     = matrix.RuleSource,
                Judges         = judges?.ToList() ?? new List<JudgeResult>()
            };

            if (judges == null || judges.Count == 0)
            {
                result.Conclusion      = NcJudgeConstants.ConclusionFail;
                result.ConclusionLevel = ConclusionRank[result.Conclusion];
                result.ConclusionLabel = Label(matrix, result.Conclusion);
                result.DowngradeReason = "无任何 judge 结果，不能给出正向结论";
                return result;
            }

            // ── ① 级别缺省补章（安全侧：漏盖 = 按 major）──
            foreach (var j in result.Judges)
            {
                if (string.IsNullOrWhiteSpace(j.Level))
                {
                    j.Level = NcJudgeConstants.DefaultLevel;
                    j.LevelDefaulted = true;
                }
            }

            // ── ② 统计 ──
            var stats = result.Stats;
            stats.Total = result.Judges.Count;
            stats.Pass = result.Judges.Count(j => j.Verdict == NcJudgeConstants.VerdictPass);
            stats.Fail = result.Judges.Count(j => j.Verdict == NcJudgeConstants.VerdictFail);
            stats.Unverifiable = result.Judges.Count(j => j.Verdict == NcJudgeConstants.VerdictUnverifiable);
            stats.LevelsDefaulted = result.Judges.Count(j => j.LevelDefaulted);

            foreach (var j in result.Judges.Where(j => j.Verdict == NcJudgeConstants.VerdictFail))
                stats.FailByLevel[j.Level] = stats.FailByLevel.GetValueOrDefault(j.Level) + 1;

            // major 判定 = level ∈ {critical, major}
            var majorFails = result.Judges.Count(j =>
                j.Verdict == NcJudgeConstants.VerdictFail &&
                (j.Level == NcJudgeConstants.LevelMajor || j.Level == NcJudgeConstants.LevelCritical));

            // 核心级 fail（CoreLevels 命中；major/critical 天然在核心内）
            var coreSet = (matrix.CoreLevels ?? new(NcJudgeConstants.DefaultCoreLevels))
                .Select(x => x.Trim().ToLowerInvariant()).ToHashSet();
            stats.CoreFail = result.Judges.Count(j =>
                j.Verdict == NcJudgeConstants.VerdictFail &&
                (j.Level == NcJudgeConstants.LevelMajor || j.Level == NcJudgeConstants.LevelCritical || coreSet.Contains(j.Level)));

            // observation fail（非核心、非 major/critical 的剩余）
            var obsFails = stats.Fail - stats.CoreFail;

            result.RuleSnapshot = new Dictionary<string, object?>
            {
                ["core_levels"]           = (matrix.CoreLevels ?? new(NcJudgeConstants.DefaultCoreLevels)).ToArray(),
                ["minor_reject_threshold"]= matrix.MinorRejectThreshold,
                ["minor_tolerated"]       = matrix.MinorTolerated,
                ["unverified_policy"]     = matrix.UnverifiedPolicy,
                ["manual_required"]       = matrix.ManualRequired
            };

            // ── ③ 判定矩阵（★ 顺序不可颠倒：先判阈值，再一票否决）──
            // 3.1 核心级 fail 达到升级阈值（含 major/critical；阈值命中 = 复审，不直接 fail）
            if (stats.CoreFail >= matrix.MinorRejectThreshold)
                return Finish(result, matrix, NcJudgeConstants.ConclusionImprovementRequired,
                    $"核心级不符合 {stats.CoreFail} 项，达到复审阈值 {matrix.MinorRejectThreshold}");

            // 3.2 严重不符合（major/critical fail）且未达复审阈值 ⇒ 直接不通过
            if (majorFails > 0)
                return Finish(result, matrix, NcJudgeConstants.ConclusionFail,
                    $"存在 {majorFails} 项严重不符合（major/critical），结论为不通过");

            // 3.3 unverifiable 且策略=reject
            if (stats.Unverifiable > 0 && matrix.UnverifiedPolicy == NcJudgeConstants.UnverifiedReject)
                return Finish(result, matrix, NcJudgeConstants.ConclusionFail,
                    $"存在 {stats.Unverifiable} 项无法判断，按规则直接判不通过");

            // 3.4 核心级 fail ≥ 1 ⇒ 带不符合项通过
            if (stats.CoreFail >= 1)
                return Finish(result, matrix, NcJudgeConstants.ConclusionPassWithNc,
                    $"核心级不符合 {stats.CoreFail} 项（未达复审阈值），带不符合项通过");

            // 3.5 仅 observation 级 fail 超过容忍数
            if (obsFails > 0 && matrix.MinorTolerated >= 0 && obsFails > matrix.MinorTolerated)
                return Finish(result, matrix, NcJudgeConstants.ConclusionPassWithNc,
                    $"轻微不符合 {obsFails} 项超过容忍数 {matrix.MinorTolerated}，带不符合项通过");

            // 3.6 unverifiable 封顶（cap_basic：结论不得优于 pass_with_nc）
            if (stats.Unverifiable > 0 && matrix.UnverifiedPolicy == NcJudgeConstants.UnverifiedCap)
                return Finish(result, matrix, NcJudgeConstants.ConclusionPassWithNc,
                    $"存在 {stats.Unverifiable} 项无法判断，结论封顶到带不符合项通过");

            // 3.7 其余 ⇒ 推荐认证
            return Finish(result, matrix, NcJudgeConstants.ConclusionPass,
                obsFails > 0 ? $"全部通过（{obsFails} 项轻微不符合在容忍范围内）" : "全部 judge 通过，推荐认证");
        }

        private static JudgeConclusionResult Finish(JudgeConclusionResult r, JudgeConclusionMatrix m, string code, string reason)
        {
            r.Conclusion      = code;
            r.ConclusionLevel = ConclusionRank[code];
            r.ConclusionLabel = Label(m, code);
            r.DowngradeReason = reason;
            return r;
        }

        private static string Label(JudgeConclusionMatrix m, string code) =>
            m.ConclusionLabels != null && m.ConclusionLabels.TryGetValue(code, out var word) && !string.IsNullOrWhiteSpace(word)
                ? word
                : DefaultConclusionLabels[code];
    }
}
