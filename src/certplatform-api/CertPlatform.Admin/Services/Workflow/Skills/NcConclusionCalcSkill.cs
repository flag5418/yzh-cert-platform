using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using CertPlatform.Shared.Workflow.Models;
using YZH.Core.DataBase.Interfaces;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Shared.Domain.Scope;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ NC 认证结论判定（★ 纯代码规则引擎，★ 不涉及 AI）
    ///
    /// <para><b>在链路中的位置</b>（18 号 §三）：</para>
    /// <code>
    ///   nc_judge (AI)         → 提取事实：各维度符合/不符合/不适用/无法判断
    ///        ↓
    ///   ★ nc_conclusion_calc  → ★算结论（权威）—— 本 Skill
    ///        ↓
    ///   nc_conclude (AI)      → 写理由（不得改结论）
    ///        ↓
    ///   nc_validate           → 校验输出合法性
    /// </code>
    ///
    /// <para><b>为什么必须是 skill 而不是 ai_node</b>：结论必须<b>可复现</b>。
    /// 同一批维度结果，规则算出的结论永远一致；AI 算则可能漂移。
    /// 本 Skill 是唯一能给出「权威结论」的组件。</para>
    ///
    /// <para><b>参数说明</b>：维度结果通过 JSON 字符串传入（来自 nc_judge 节点的 output）。
    /// 之所以走字符串而非集合，是因为引擎的参数绑定按名字从 Dictionary 取单值，
    /// 不支持数组（SkillExecutor.ConvertValue 只处理标量）。</para>
    ///
    /// <para><b>⚠️ 阈值来源</b>：cert_conclusion_rule 表。当前为<b>占位值</b>，
    /// 需认证机构确认后修改（18 号 §十一）。</para>
    /// </summary>
    [Skill(
        Code = "nc_conclusion_calc",
        Name = "NC认证结论判定",
        ReturnType = "string",
        ResultPort = "conclusion",
        Description = "★ 纯规则引擎：按 cert_conclusion_rule 计算认证结论（完全符合/基本符合/不符合）。AI 不参与判定，结果可复现"
    )]
    public static class NcConclusionCalcSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            // ★ AllowEmpty = SkillParamEmptyPolicy.Deny：没有维度结果就<b>算不出结论</b> —— 必须中断而不是产出一个空结论。
            //   注意 dimensions_json 通常连 nc_judge（AI 节点）的输出；若 AI 没产出，
            //   正确做法是先用 is_empty + branch 分流，这里只是<b>安全网</b>。
            [SkillParam(Description = "维度判定结果 JSON（来自 nc_judge 节点，形如 {\"dimensions\":[...]}）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? dimensions_json,

            // ★ AllowEmpty = SkillParamEmptyPolicy.Deny：取不到机构就<b>找不到判定规则</b>（LoadRuleAsync 直接返回 null
            //   ⇒ 下方 Fail）。提前在引擎层拦下，错误信息更准（指出是哪个参数没给）。
            [SkillParam(Description = "认证机构编码（★取不到时回落到默认规则）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? org_code,

            [SkillParam(Description = "标准编码（★空 = 全部标准通用规则）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? standard_code,

            [SkillParam(Description = "阶段编码（★空 = 全阶段通用规则）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? phase_code,

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            // ── ① 解析维度结果（★ 失败即报错，不静默降级）──
            List<DimensionResult> dimensions;
            try
            {
                dimensions = DimensionResultParser.Parse(dimensions_json);
            }
            catch (FormatException ex)
            {
                return SkillResult.Fail(
                    $"维度判定结果解析失败：{ex.Message}", WorkflowErrorCodes.ConfigError);
            }

            // ── ② 取判定规则（★ 精确 → 通用 逐级回落）──
            var rule = await LoadRuleAsync(db, org_code, standard_code, phase_code);
            if (rule == null)
                return SkillResult.Fail(
                    $"未找到结论判定规则（OrgCode={org_code}, StandardCode={standard_code}, PhaseCode={phase_code}）",
                    WorkflowErrorCodes.ConfigError);

            // ── ③ ★ 计算（纯函数，同输入必同输出）──
            var result = new NcConclusionCalculator().Calculate(dimensions, rule);

            // ── ④ 输出 ──
            return SkillResult.Ok(new Dictionary<string, object>
            {
                // ★ 权威结论
                ["conclusion"]      = result.Conclusion,
                ["conclusionLevel"] = result.ConclusionLevel,
                ["conformity"]      = result.Conformity,
                ["severity"]        = result.Severity,
                ["basis"]           = result.Basis.ToArray(),
                ["unverified"]      = result.Unverified.ToArray(),
                ["downgradeReason"] = result.DowngradeReason,
                ["crossProcessFailures"] = result.CrossProcessFailures,
                ["manualRequired"]  = result.ManualRequired,
                ["mode"]            = result.Mode,
                ["ruleSource"]      = result.RuleSource,

                // 统计（供界面与 Excel 导出）
                ["stats"] = new Dictionary<string, object>
                {
                    ["total"]          = result.Stats.Total,
                    ["conform"]        = result.Stats.Conform,
                    ["nonconform"]     = result.Stats.Nonconform,
                    ["notApplicable"]  = result.Stats.NotApplicable,
                    ["unverifiable"]   = result.Stats.Unverifiable
                },

                // 维度明细（★ 供 nc_conclude 节点写理由时引用）
                ["dimensions"] = dimensions.Select(d => new Dictionary<string, object?>
                {
                    ["no"]         = d.No,
                    ["name"]       = d.Name,
                    ["conformity"] = d.Conformity,
                    ["desc"]       = d.Desc,
                    ["evidence"]   = d.Evidence,
                    ["processCode"]= d.ProcessCode
                }).ToArray(),

                // 规则快照（★ 事后可回溯当时用的哪版规则）
                ["ruleSnapshot"] = new Dictionary<string, object?>
                {
                    ["minorToBasicThreshold"] = rule.MinorToBasicThreshold,
                    ["minorRejectThreshold"]  = rule.MinorRejectThreshold,
                    ["crossProcessEnabled"]   = rule.CrossProcessEnabled,
                    ["crossProcessThreshold"] = rule.CrossProcessThreshold,
                    ["unverifiedPolicy"]      = rule.UnverifiedPolicy,
                    ["conclusionMode"]        = rule.ConclusionMode
                }
            }, confidence: 1.0);   // ★ 规则确定性计算，置信度恒为 1
        }

        /// <summary>
        /// 取判定规则：机构专属优先 → 后台标准级回落，再逐级匹配
        /// <para><b>★ 取数口径（`OrgCode` 判据铁律 §2.2）</b>：同时取「机构专属」与
        /// 「后台标准级（<see cref="''"/> = `yzh-std-env`）」两类行，
        /// 机构专属优先。⛔ 不写字符串字面量（守卫 <b>A-R9</b>），⛔ 不写
        /// `OrgCode == ""` / `IsNullOrEmpty`（守卫 <b>A-R8</b>）。</para>
        /// <para>优先级：</para>
        /// <list type="number">
        /// <item>(OrgCode, StandardCode, PhaseCode) 精确</item>
        /// <item>(OrgCode, StandardCode, NULL) 该标准全阶段</item>
        /// <item>(OrgCode, '', NULL) ★全部标准通用（种子 CR-GENERIC-0001）</item>
        /// </list>
        /// </summary>
        private static async Task<ConclusionRuleSnapshot?> LoadRuleAsync(
            IDbOrm db, string? orgCode, string? standardCode, string? phaseCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(orgCode)) return null;

            // ★ GetListAsync 无 ct 参数；includeDisabled=true 以便显式判 IsValid
            var rules = await db.GetListAsync<ConclusionRule>(
                x => (x.OrgCode == orgCode || x.OrgCode == "") && x.IsDeleted == false,
                includeDisabled: true);

            var candidates = (rules.Data ?? new List<ConclusionRule>())
                .OrderByDescending(r => r.OrgCode == orgCode)   // ★ 机构专属优先
                .ToList();
            if (candidates.Count == 0) return null;

            var std = standardCode ?? string.Empty;
            var pha = phaseCode ?? string.Empty;

            var hit =
                    candidates.FirstOrDefault(r => r.StandardCode == std && (r.PhaseCode ?? "") == pha)
                ?? candidates.FirstOrDefault(r => r.StandardCode == std && string.IsNullOrEmpty(r.PhaseCode))
                ?? candidates.FirstOrDefault(r => string.IsNullOrEmpty(r.StandardCode));

            if (hit == null) return null;

            return new ConclusionRuleSnapshot
            {
                OrgCode                = hit.OrgCode,
                StandardCode           = hit.StandardCode,
                PhaseCode              = hit.PhaseCode,
                LevelFull              = hit.LevelFull,
                LevelBasic             = hit.LevelBasic,
                LevelReject            = hit.LevelReject,
                MinorToBasicThreshold  = hit.MinorToBasicThreshold,
                MinorRejectThreshold   = hit.MinorRejectThreshold,
                CrossProcessEnabled    = hit.CrossProcessEnabled,
                CrossProcessThreshold  = hit.CrossProcessThreshold,
                UnverifiedPolicy       = hit.UnverifiedPolicy,
                ConclusionMode         = hit.ConclusionMode,
                ManualRequired         = hit.ManualRequired,
                OverrideAlertThreshold = hit.OverrideAlertThreshold,
                JudgePromptCode        = hit.JudgePromptCode,
                ConcludePromptCode     = hit.ConcludePromptCode,
                SourceRef              = hit.SourceRef
            };
        }
    }
}
