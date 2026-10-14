using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Shared.Workflow.Models;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ NC 多 judge 结论判定 v2（★ 纯代码 · 决策矩阵 · ★ 不涉及 AI）
    ///
    /// <para><b>在 v2 链路中的位置</b>（2026-10-14 用户裁决）：</para>
    /// <code>
    ///   nc_judge_A → stamp_judge_level(J1,critical) ─┐
    ///   nc_judge_B → stamp_judge_level(J2,none)      ─┼→ collect_judges ─→ ★nc_conclusion_v2★ ─→ end
    ///   nc_judge_C → stamp_judge_level(J3,minor)     ─┘   (judges 数组)     (权威结论)
    /// </code>
    ///
    /// <para><b>入参</b>：一个 <c>judges_json</c>（collect_judges 的 <c>judges</c> 主输出序列化）
    /// + <c>org_code / standard_code / phase_code</c>（取规则用，复用 v1 的
    /// 「机构专属 → 平台标准级 → 逐级回落」口径）。</para>
    ///
    /// <para><b>输出</b>：<c>conclusion</c>（pass/pass_with_nc/improvement_required/fail）
    /// + <c>judges</c>（原样回传，供 nc_conclude / 报告阶段）+ <c>stats</c> + <c>ruleSnapshot</c>
    /// （当时用的哪版判定矩阵，审计可回溯）。纯函数，同输入必同输出，⛔ 无任何 AI 参与。</para>
    /// </summary>
    [Skill(
        Code = "nc_conclusion_v2",
        Name = "NC结论判定v2(多judge)",
        ReturnType = "json",
        ResultPort = "conclusion",
        Description = "★ 纯代码决策矩阵：按 cert_conclusion_rule v2 配置算多 judge 的权威总结论（pass/pass_with_nc/improvement_required/fail），AI 不参与"
    )]
    public static class NcConclusionV2Skill
    {
        private readonly static NcConclusionV2Calculator _calc = new();

        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★ judge 数组 JSON（collect_judges 的 judges 主输出；[{judge_code,level,verdict,reason},…]）",
                        BindMode = SkillParamBindMode.Link, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? judges_json,

            [SkillParam(Description = "认证机构编码（★ 取不到即找不到规则 ⇒ 中断；空串=平台标准级）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? org_code,

            [SkillParam(Description = "标准编码（空=全部标准通用规则）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? standard_code,

            [SkillParam(Description = "阶段编码（空=全阶段通用规则）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? phase_code,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            // ── ① 解析 judges（严格：非法/空直接报错，不静默）──
            List<JudgeResult> judges;
            try
            {
                judges = JudgeResultParser.Parse(judges_json);
            }
            catch (Exception ex)
            {
                return SkillResult.Fail($"judge 结果解析失败：{ex.Message}", WorkflowErrorCodes.ConfigError);
            }

            // ── ② 取判定矩阵（机构专属 → 平台标准级 回落；找不到 ⇒ 全缺省并标注）──
            var matrix = await LoadMatrixAsync(db, org_code, standard_code, phase_code, ct);

            // ── ③ ★ 计算（纯函数，同输入必同输出）──
            var result = _calc.Calculate(judges, matrix);

            // ── ④ 输出（原样 judges + 统计 + 矩阵快照，审计可读）──
            var judgesOut = judges.Select(j => new Dictionary<string, object?>
            {
                ["judge_code"]     = j.JudgeCode,
                ["level"]          = j.Level,
                ["verdict"]        = j.Verdict,
                ["reason"]         = j.Reason,
                ["level_defaulted"]= j.LevelDefaulted
            }).ToList();

            var outputs = new Dictionary<string, object>
            {
                ["conclusion"]         = result.Conclusion,
                ["conclusion_level"]   = result.ConclusionLevel,
                ["conclusion_label"]   = result.ConclusionLabel,
                ["downgrade_reason"]   = result.DowngradeReason,
                ["judges"]             = judgesOut,
                ["stats"] = new Dictionary<string, object>
                {
                    ["total"]             = result.Stats.Total,
                    ["pass"]              = result.Stats.Pass,
                    ["fail"]              = result.Stats.Fail,
                    ["unverifiable"]      = result.Stats.Unverifiable,
                    ["fail_by_level"]     = result.Stats.FailByLevel,
                    ["core_fail"]         = result.Stats.CoreFail,
                    ["levels_defaulted"]  = result.Stats.LevelsDefaulted
                },
                ["rule_snapshot"]    = result.RuleSnapshot,
                ["rule_source"]      = result.RuleSource,
                ["manual_required"]  = result.ManualRequired,
                // ★ 诊断面：矩阵来源标记（是否用了缺省矩阵）
                ["matrix_source"]    = result.RuleSource == "DEFAULT_MATRIX" ? "default" : "cert_conclusion_rule"
            };

            // ★ 配置缺口显式化：有 judge 因漏盖级别被按 major 计算 ⇒ 提示规则作者补配置
            if (result.Stats.LevelsDefaulted > 0)
            {
                outputs["warning"] =
                    $"★ {result.Stats.LevelsDefaulted} 条 judge 未盖级别，已按安全缺省 major 计入结论，请在 stamp_judge_level 节点补配 level";
            }

            return SkillResult.Ok(outputs, 1.0);   // ★ 规则确定性计算，置信度恒 1
        }

        /// <summary>
        /// 取判定矩阵：从 cert_conclusion_rule 投影 v2 列。
        /// <para><b>取数口径（归属模型 V2 §2.0，与 v1 LoadRuleAsync 一致）</b>：
        /// 机构专属（OrgCode==org）优先 → 平台标准级（OrgCode==''）回落，再按
        /// (StandardCode, PhaseCode) 精确 → 该标准全阶段 → 全通用 逐级回落。
        /// ⛔ 找不到任何行 ⇒ 返回<b>全缺省矩阵</b>并标注 RuleSource="DEFAULT_MATRIX"
        /// （⛔ 不是 Fail：配置未建是「可跑通但需补」，让链路能继续产出结论供测试，
        ///   同时用 warning 把缺口显式化；与 v1 找不到规则即 Fail 的口径不同，
        ///   因为 v2 是 2026-10-14 新链路，优先保证可测通）。</para>
        /// </summary>
        private static async Task<JudgeConclusionMatrix> LoadMatrixAsync(
            IDbOrm db, string? orgCode, string? standardCode, string? phaseCode, CancellationToken ct)
        {
            var matrix = new JudgeConclusionMatrix();

            try
            {
                var rows = await db.GetListAsync<ConclusionRule>(
                    x => (x.OrgCode == orgCode || x.OrgCode == "") && x.IsDeleted == false,
                    includeDisabled: true);

                var candidates = (rows.Data ?? new List<ConclusionRule>())
                    .Where(x => x.IsValid == 1)
                    .OrderByDescending(r => r.OrgCode == orgCode)   // ★ 机构专属优先
                    .ToList();

                var std = standardCode ?? string.Empty;
                var pha = phaseCode ?? string.Empty;

                var hit =
                        candidates.FirstOrDefault(r => r.StandardCode == std && (r.PhaseCode ?? "") == pha)
                    ?? candidates.FirstOrDefault(r => r.StandardCode == std && string.IsNullOrEmpty(r.PhaseCode))
                    ?? candidates.FirstOrDefault(r => string.IsNullOrEmpty(r.StandardCode))
                    ?? candidates.FirstOrDefault();   // ★ v2 放宽：任何启用行都比全缺省好

                if (hit != null)
                {
                    matrix.CoreLevels          = ParseCoreLevels(hit.CoreLevels);
                    matrix.MinorRejectThreshold = hit.MinorRejectThreshold > 0 ? hit.MinorRejectThreshold : matrix.MinorRejectThreshold;
                    matrix.MinorTolerated      = hit.MinorTolerated;
                    matrix.UnverifiedPolicy    = hit.UnverifiedPolicy;
                    matrix.ManualRequired      = hit.ManualRequired;
                    matrix.ConclusionLabels    = ParseLabels(hit.ConclusionLabels);
                    matrix.RuleSource          = hit.SourceRef;
                }
                else
                {
                    matrix.RuleSource = "DEFAULT_MATRIX";
                }
            }
            catch (Exception)
            {
                // 取数失败 ⇒ 全缺省矩阵（可测通），RuleSource 标注
                matrix.RuleSource = "DEFAULT_MATRIX";
            }

            return matrix;
        }

        /// <summary>CoreLevels：JSON 数组（["major","minor"]）→ List；空/非法 = 缺省 [major,minor]</summary>
        private static List<string> ParseCoreLevels(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new(NcJudgeConstants.DefaultCoreLevels);
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var list = doc.RootElement.EnumerateArray()
                        .Select(e => e.GetString()?.Trim().ToLowerInvariant())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                    return list.Count > 0 ? list : new(NcJudgeConstants.DefaultCoreLevels);
                }
            }
            catch { /* 非法 ⇒ 缺省 */ }
            return new(NcJudgeConstants.DefaultCoreLevels);
        }

        /// <summary>ConclusionLabels：JSON 对象 {pass:"…",…} → dict；空/非法 = null（计算器用内置词）</summary>
        private static Dictionary<string, string>? ParseLabels(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                var d = new Dictionary<string, string>();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var v = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(v)) d[p.Name] = v;
                }
                return d.Count > 0 ? d : null;
            }
            catch { return null; }
        }
    }
}
