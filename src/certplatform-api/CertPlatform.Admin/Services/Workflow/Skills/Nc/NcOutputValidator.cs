using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CertPlatform.Admin.Services.Workflow.Skills.Nc
{
    /// <summary>校验结果（一条 = 一个检查项）</summary>
    public class ValidationItem
    {
        /// <summary>维度号（0 表示整体级检查）</summary>
        public int DimensionNo { get; set; }

        /// <summary>维度名</summary>
        public string? DimensionName { get; set; }

        /// <summary>命中的校验编号（C1-C7）</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>校验项名</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>严重级别：error=阻断 | warn=告警 | info=提示</summary>
        public string Level { get; set; } = "warn";

        /// <summary>说明</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>校验汇总</summary>
    public class ValidationResult
    {
        /// <summary>★ 是否通过（无 error 级即通过）</summary>
        public bool Passed { get; set; }

        /// <summary>★ 是否★阻断</summary>
        public bool Blocked { get; set; }

        public int ErrorCount { get; set; }
        public int WarnCount  { get; set; }
        public int InfoCount  { get; set; }

        public List<ValidationItem> Items { get; set; } = new();

        /// <summary>★ 真实文件集合（来自 wf_node_execution.SourceFileCode），C5 交叉验证用</summary>
        public HashSet<string> KnownSourceFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ★ 输出校验器（★ 纯代码 · 不涉及 AI）
    ///
    /// <para><b>在链路中的位置</b>（18 号 §3.5）：链路的最后一道关。
    /// 前面的节点都是 AI，可能幻觉；本 Skill 负责把不合法的输出拦下来。</para>
    ///
    /// <para><b>7 条校验</b>：</para>
    /// <list type="table">
    ///   <item><b>C1</b> 维度数组非空 → error（阻断）</item>
    ///   <item><b>C2</b> 维度号 ⊇ 规则配置的维度清单 → error（防漏判）</item>
    ///   <item><b>C3</b> conformity ∈ 四态 → warn + 降级为 unverifiable</item>
    ///   <item><b>C4</b> nonconform 的 desc 非空 → warn</item>
    ///   <item><b>C5</b> ★ evidence 里的文件在真实来源集合中 → warn（★零成本反幻觉）</item>
    ///   <item><b>C6</b> nc结论 理由里出现依据外的维度号 → warn + 剥离</item>
    ///   <item><b>C7</b> ★ AI 产出的结论 vs 规则结论不一致 → ★以规则为准</item>
    /// </list>
    /// </summary>
    public class NcOutputValidator
    {
        /// <summary>★ 维度的严重度预设（来自规则配置），key=维度号</summary>
        public Dictionary<int, string> DimensionSeverity { get; set; } = new();

        /// <summary>★ 规则配置里的维度号全集（C2 校验用）</summary>
        public HashSet<int> ExpectedDimensionNos { get; set; } = new();

        /// <summary>
        /// ★ C5 用：本次执行真实记录到的来源文件 Code 集合
        /// <para>来源：<c>wf_node_execution.SourceFileCode</c>（引擎自动记录）。
        /// ⚠️ 依赖 B4 修复——修复前该字段恒为 NULL，本项校验会全部放行（不会误伤，但失效）。</para>
        /// </summary>
        public HashSet<string> KnownSourceFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // 证据文本里可能的文件名（中文/英文/数字/点/下划线/短横线/空格）
        private static readonly Regex FileNamePattern =
            new(@"[\w\u4e00-\u9fa5][\w\u4e00-\u9fa5\.\- ]{1,60}\.(pdf|docx?|xlsx?|pptx?|md|txt|jpg|png)", RegexOptions.IgnoreCase);

        /// <summary>
        /// 校验 ①：nc_judge 的输出（维度列表）
        /// </summary>
        public ValidationResult ValidateDimensions(
            IReadOnlyList<DimensionResult> dimensions,
            string? dimensionsRawJson = null)
        {
            var r = new ValidationResult { KnownSourceFiles = KnownSourceFiles };

            // ── C1：数组非空 ──
            if (dimensions == null || dimensions.Count == 0)
            {
                r.Items.Add(Err(0, "C1", "维度结果非空", "error",
                    "★ nc_judge 未产出任何维度结果"));
                return Summarize(r);
            }

            // ── C2：维度号覆盖（★ 规则里有但 AI 没输出 → 漏判）──
            if (ExpectedDimensionNos.Count > 0)
            {
                var got = dimensions.Select(d => d.No).ToHashSet();
                var missing = ExpectedDimensionNos.Where(n => !got.Contains(n)).OrderBy(n => n).ToList();
                if (missing.Count > 0)
                {
                    r.Items.Add(Err(0, "C2", "维度覆盖完整性", "error",
                        $"★ 规则配置了 {ExpectedDimensionNos.Count} 个维度，AI 只判了 {got.Count} 个，" +
                        $"漏判：{string.Join("、", missing)}（漏判维度将被视为 unverifiable）"));
                }
                // 多出的维度号（规则里没有）→ warn
                var extra = got.Where(n => !ExpectedDimensionNos.Contains(n)).OrderBy(n => n).ToList();
                if (extra.Count > 0)
                {
                    r.Items.Add(Err(0, "C2", "维度越界", "warn",
                        $"AI 输出了规则中不存在的维度号：{string.Join("、", extra)}（将被忽略）"));
                }
            }

            foreach (var d in dimensions)
            {
                // ── C3：四态枚举 ──
                if (!DimensionConformity.IsValid(d.Conformity))
                {
                    r.Items.Add(Err(d.No, "C3", "四态枚举合法性", "warn",
                        $"维度{d.No}「{d.Name}」的 conformity=\"{d.Conformity}\" 不在四态枚举内" +
                        $"，已降级为 unverifiable（{string.Join("/", DimensionConformity.All)}）"));
                }

                // ── C4：不符合必须有描述 ──
                if (d.Conformity == DimensionConformity.Nonconform && string.IsNullOrWhiteSpace(d.Desc))
                {
                    r.Items.Add(Err(d.No, "C4", "不符合描述", "warn",
                        $"维度{d.No}「{d.Name}」判为不符合但没有描述，无法作为 NC 依据"));
                }

                // ── ★ C5：证据文件交叉验证（反幻觉）──
                if (!string.IsNullOrWhiteSpace(d.Evidence)
                    && KnownSourceFiles.Count > 0        // ★ 依赖 B4 修复；未修复时跳过
                    && !IsEvidenceFileKnown(d.Evidence!))
                {
                    r.Items.Add(Err(d.No, "C5", "证据真实性", "warn",
                        $"维度{d.No}「{d.Name}」引用的文件「{ExtractFirstFile(d.Evidence!)}」" +
                        "不在本次执行的真实来源文件中，★可能是 AI 编造，需人工核实"));
                }
            }

            return Summarize(r);
        }

        /// <summary>
        /// 校验 ②：nc_conclude 的输出（理由）
        /// </summary>
        /// <param name="reason">AI 写的理由</param>
        /// <param name="allowedBasis">规则算出的依据维度号（C6 校验基准）</param>
        /// <param name="aiConclusion">★ AI 若自行产出了结论（可选，多数情况下为空）</param>
        /// <param name="ruleConclusion">★ 规则算出的权威结论</param>
        public ValidationResult ValidateConclusion(
            string? reason,
            IReadOnlyList<int> allowedBasis,
            string? aiConclusion,
            string ruleConclusion)
        {
            var r = new ValidationResult { KnownSourceFiles = KnownSourceFiles };

            // ── C6：理由里不得出现依据外的维度号 ──
            if (!string.IsNullOrWhiteSpace(reason) && allowedBasis.Count > 0)
            {
                var cited = ExtractDimensionNos(reason!);
                var illegal = cited.Where(n => !allowedBasis.Contains(n)).OrderBy(n => n).ToList();
                if (illegal.Count > 0)
                {
                    r.Items.Add(Err(0, "C6", "依据越界", "warn",
                        $"AI 理由中引用了依据清单外的维度号：{string.Join("、", illegal)}" +
                        $"（合法依据：{string.Join("、", allowedBasis)}），★已剥离"));
                }
            }

            if (string.IsNullOrWhiteSpace(reason))
                r.Items.Add(Err(0, "C6", "理由非空", "warn", "AI 未产出结论理由"));

            // ── ★ C7：AI 结论 vs 规则结论 ──
            if (!string.IsNullOrWhiteSpace(aiConclusion)
                && !string.Equals(aiConclusion!.Trim(), ruleConclusion?.Trim(), StringComparison.Ordinal))
            {
                r.Items.Add(Err(0, "C7", "结论权威性", "warn",
                    $"★ AI 产出结论「{aiConclusion}」与规则结论「{ruleConclusion}」不一致，" +
                    "★已按规则结论为准，AI 结论仅记录不采纳"));
            }

            return Summarize(r);
        }

        // ─────────────────────────────────────────────
        // 工具方法
        // ─────────────────────────────────────────────

        private static ValidationItem Err(int no, string code, string name, string level, string msg)
            => new() { DimensionNo = no, Code = code, Name = name, Level = level, Message = msg };

        private static ValidationResult Summarize(ValidationResult r)
        {
            r.ErrorCount = r.Items.Count(i => i.Level == "error");
            r.WarnCount  = r.Items.Count(i => i.Level == "warn");
            r.InfoCount  = r.Items.Count(i => i.Level == "info");
            r.Blocked    = r.ErrorCount > 0;
            r.Passed     = !r.Blocked;
            return r;
        }

        /// <summary>从理由文本里抽取被引用的维度号（如「维度2」「维度 2」「第2项」）</summary>
        public static List<int> ExtractDimensionNos(string text)
        {
            var result = new HashSet<int>();
            foreach (Match m in Regex.Matches(text, @"维度\s*(\d+)|第\s*(\d+)\s*[项条]"))
            {
                var g = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (int.TryParse(g, out var n)) result.Add(n);
            }
            return result.OrderBy(x => x).ToList();
        }

        /// <summary>从证据文本里抽出第一个文件名</summary>
        public static string? ExtractFirstFile(string evidence)
        {
            var m = FileNamePattern.Match(evidence);
            return m.Success ? m.Value.Trim() : null;
        }

        /// <summary>证据里的文件名是否都能在真实来源中找到（★找不到即为可疑）</summary>
        private bool IsEvidenceFileKnown(string evidence)
        {
            var files = FileNamePattern.Matches(evidence).Select(m => m.Value.Trim()).ToList();
            if (files.Count == 0) return true;    // 提不出文件名 → 不判（可能只写了位置描述）

            // ★ 全部命中才算通过（部分命中说明至少有一个是编造的）
            return files.All(f => KnownSourceFiles.Contains(f)
                                  || KnownSourceFiles.Any(k => k.EndsWith(f, StringComparison.OrdinalIgnoreCase)
                                                             || f.EndsWith(k, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
