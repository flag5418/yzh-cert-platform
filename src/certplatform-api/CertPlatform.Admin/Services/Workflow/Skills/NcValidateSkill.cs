using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ NC 输出校验（★ 纯代码，★ 链路最后一道关）
    ///
    /// <para><b>防什么</b>：前面的 nc_judge / nc_conclude 都是 AI，可能幻觉。
    /// 本 Skill 把不合法的输出拦下来并明确报出（18 号 §3.5 C1-C7）。</para>
    ///
    /// <para><b>7 条校验</b>：C1 数组非空 / C2 维度覆盖 / C3 四态枚举 /
    /// C4 不符合须有描述 / <b>C5 ★证据文件交叉验证（反幻觉）</b> /
    /// C6 理由不得越界 / <b>C7 ★AI 结论一律以规则为准</b>。</para>
    ///
    /// <para><b>两种模式</b>（同一 Skill，两种入参）：
    ///   · <c>stage=dimensions</c> 校验 nc_judge 的维度列表
    ///   · <c>stage=conclusion</c> 校验 nc_conclude 的理由 + 结论一致性</para>
    /// </summary>
    [Skill(
        Code = "nc_validate",
        Name = "NC输出校验",
        ReturnType = "json",
        Description = "★ 校验 AI 产出的 NC 维度结果/结论理由：四态枚举、维度覆盖、证据真实性（反幻觉）、结论权威性。共 7 条校验"
    )]
    public static class NcValidateSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★校验阶段：dimensions=校验维度列表 | conclusion=校验结论理由",
                        BindMode = SkillParamBindMode.Enum, EnumSource = "nc_validate_stage")]
            string? stage,

            [SkillParam(Description = "★阶段=dimensions：维度判定结果 JSON（来自 nc_judge 节点）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? dimensions_json,

            [SkillParam(Description = "★阶段=conclusion：AI 写的理由（来自 nc_conclude 节点）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? reason,

            [SkillParam(Description = "★阶段=conclusion：AI 若自行产出了结论（★多数应为空，规则为准）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? ai_conclusion,

            [SkillParam(Description = "★规则算出的权威结论（来自 nc_conclusion_calc 节点）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? rule_conclusion,

            [SkillParam(Description = "★规则算出的依据维度号（JSON 数组，如 [2,3]）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? allowed_basis_json,

            [SkillParam(Description = "★本次执行任务编码（用于查 wf_node_execution 的真实来源文件，做 C5 交叉验证）",
                        BindMode = SkillParamBindMode.LinkOrConstant)]
            string? wf_task_code,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            var validator = new NcOutputValidator();
            ValidationResult result;

            switch ((stage ?? "dimensions").Trim().ToLowerInvariant())
            {
                case "conclusion":
                {
                    if (string.IsNullOrWhiteSpace(rule_conclusion))
                        return SkillResult.Fail("rule_conclusion 不能为空（★权威结论缺失，无法校验 C7）");

                    var basis = ParseIntArray(allowed_basis_json);

                    // ★ C5 前置：拉取本次执行真实记录到的来源文件
                    if (!string.IsNullOrWhiteSpace(wf_task_code) && db != null)
                    {
                        var files = await LoadSourceFilesAsync(db, wf_task_code!);
                        validator.KnownSourceFiles = files;
                    }

                    result = validator.ValidateConclusion(reason, basis, ai_conclusion, rule_conclusion!);
                    break;
                }

                case "dimensions":
                default:
                {
                    List<DimensionResult> dims;
                    try
                    {
                        dims = DimensionResultParser.Parse(dimensions_json);
                    }
                    catch (FormatException ex)
                    {
                        // ★ C1：解析失败 = 阻断（不是放过）
                        return SkillResult.Ok(new Dictionary<string, object>
                        {
                            ["passed"]  = false,
                            ["blocked"] = true,
                            ["errorCount"] = 1, ["warnCount"] = 0, ["infoCount"] = 0,
                            ["items"] = new object[]
                            {
                                new Dictionary<string, object?>
                                {
                                    ["code"] = "C1", ["name"] = "维度结果非空", ["level"] = "error",
                                    ["message"] = $"★ 维度判定结果解析失败：{ex.Message}"
                                }
                            }
                        });
                    }

                    // ★ 规则配置的维度清单（C2 基准）—— 从 nc_conclusion_calc 的快照取
                    if (!string.IsNullOrWhiteSpace(wf_task_code) && db != null)
                    {
                        var expected = await LoadExpectedDimensionsAsync(db, wf_task_code!);
                        if (expected.Count > 0) validator.ExpectedDimensionNos = expected;
                        validator.KnownSourceFiles = await LoadSourceFilesAsync(db, wf_task_code!);
                    }

                    result = validator.ValidateDimensions(dims, dimensions_json);
                    break;
                }
            }

            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["passed"]     = result.Passed,
                ["blocked"]    = result.Blocked,
                ["errorCount"] = result.ErrorCount,
                ["warnCount"]  = result.WarnCount,
                ["infoCount"]  = result.InfoCount,
                ["items"]      = result.Items.Select(i => new Dictionary<string, object?>
                {
                    ["dimensionNo"]   = i.DimensionNo,
                    ["dimensionName"] = i.DimensionName,
                    ["code"]          = i.Code,
                    ["name"]          = i.Name,
                    ["level"]         = i.Level,
                    ["message"]       = i.Message
                }).ToArray(),
                // ★ C5 的判据集合（便于界面显示"已核对哪些文件"）
                ["knownSourceFiles"] = result.KnownSourceFiles.ToArray(),
                // ★ C7 结论以规则为准
                ["finalConclusion"]  = rule_conclusion
            });
        }

        /// <summary>
        /// ★ 拉取本次执行真实记录到的来源文件（C5 的判据）
        /// <para>来源：<c>wf_node_execution.SourceFileCode</c>（引擎自动记录，17 号 §三）。</para>
        /// <para>⚠️ 依赖 B4 修复。修复前该列恒为 NULL → 集合为空 → C5 自动跳过（不误伤但失效）。</para>
        /// </summary>
        private static async Task<HashSet<string>> LoadSourceFilesAsync(IDbOrm db, string wfTaskCode)
        {
            var rows = await db.Client.Queryable<CertPlatform.Shared.Entities.Wf.WfNodeExecution>()
                .Where(x => x.TaskCode == wfTaskCode
                         && x.SourceFileCode != null && x.SourceFileCode != "")
                .Select(x => x.SourceFileCode)
                .ToListAsync();

            return new HashSet<string>(rows ?? new List<string?>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 拉取规则配置的维度号全集（C2 基准）
        /// <para>来源：<c>wf_execution_task_item.OutputJson</c> 里 <c>nc_conclusion_calc</c> 的 ruleSnapshot
        /// —— 但那是规则阈值，不含维度清单。</para>
        /// <para>⚠️ 当前实现：nc_judge 节点配置里带 dimensions 时的兜底；若无则 C2 跳过（不误伤）。</para>
        /// </summary>
        private static async Task<HashSet<int>> LoadExpectedDimensionsAsync(IDbOrm db, string wfTaskCode)
        {
            // 暂不从 wf_execution_task_item 反查（其 OutputJson 不含维度清单）。
            // 待 16 号 N1 的 dimensions 落 cert_validation_rule.RuleJson 后，
            // 改为按 RuleCode 读 RuleJson.dimensions[].No —— 见文档 TODO。
            await Task.CompletedTask;
            return new HashSet<int>();
        }

        private static List<int> ParseIntArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<int>();
                return doc.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.Number)
                    .Select(e => e.GetInt32())
                    .ToList();
            }
            catch
            {
                return new List<int>();
            }
        }
    }
}
