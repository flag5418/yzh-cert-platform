using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Workflow.Models;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// ★ judge 结果盖章级别（NC 结论判定 v2 · 纯代码 · 不涉及 AI）
    ///
    /// <para><b>作用</b>：给一个 nc_judge（确定性节点）的输出<b>盖一个级别</b>，
    /// 标记「这条 judge 若 fail，按什么级别影响最终结论」。
    /// 级别由专家在画布上配置（critical/major/minor/observation/none），
    /// 这是 2026-10-14 用户裁决的「每个 nc_judge 是否影响最终结论」的配置载体 ——
    /// 配置写在本节点（生产者），下游 <c>nc_conclusion_v2</c> 只认输出里的 level，⛔ 不反查。</para>
    ///
    /// <para><b>数据契约（★ 关键）</b>：本 skill 的<b>主输出端口 <c>result</c></b> =
    /// 一条<b>完整 judge 记录</b>（字典，含 judge_code/level/verdict/reason/result/payload/level_defaulted）。
    /// 下游 <c>collect_judges</c> 经连线拿到的正是这条记录（引擎 <c>PickFromOutput</c> 取 <c>result</c> 主端口），
    /// 聚合为数组后交 <c>nc_conclusion_v2</c> 计算。verdict 若未显式给，则据上游结果值<b>确定性</b>推断
    /// （<see cref="NcConclusionV2Calculator.InferVerdict"/>），⛔ 不引入 AI。</para>
    ///
    /// <para><b>★ 级别缺省方向（安全侧）</b>：level 传空/未知 ⇒ 按 <c>major</c> 计算并打
    /// <c>level_defaulted=true</c> —— 漏盖一条 = 按最严处理，⛔ 不能默认「不影响」。</para>
    /// </summary>
    [Skill(
        Code = "stamp_judge_level",
        Name = "judge盖章级别",
        ReturnType = "json",
        ResultPort = "result",
        Description = "★ 给 nc_judge 结果盖级别（critical/major/minor/observation/none），标记它对最终结论的影响；级别缺省按 major（安全侧）；主输出=完整judge记录"
    )]
    public static class StampJudgeLevelSkill
    {
        /// <summary>本 Skill 编码</summary>
        public const string SkillCode = "stamp_judge_level";

        /// <summary>合法级别集合（小写）。critical 与 major 同档（严重），单独计数展示。</summary>
        private static readonly string[] Levels =
        {
            NcJudgeConstants.LevelCritical, NcJudgeConstants.LevelMajor,
            NcJudgeConstants.LevelMinor, NcJudgeConstants.LevelObservation, NcJudgeConstants.LevelNone
        };

        public static Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "★ 上游 nc_judge 的输出（连线；对象/数组/标量皆可，保留为记录里的 result/payload）",
                        BindMode = SkillParamBindMode.LinkOrConstant, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            object? result,

            [SkillParam(Description = "★ judge 标识（常量；如 J1 / rule_nc_75，用于报告阶段区分来源）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Deny)]
            string? judge_code,

            [SkillParam(Description = "★ 级别（常量；critical/major/minor/observation/none，空=按 major 安全缺省）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? level = null,

            [SkillParam(Description = "判定（常量；pass/fail/unverifiable；空=据上游结果值确定性推断）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? verdict = null,

            [SkillParam(Description = "失败原因/证据（常量；报告阶段叙述素材，可空）",
                        BindMode = SkillParamBindMode.ConstantOnly, AllowEmpty = SkillParamEmptyPolicy.Allow)]
            string? reason = null,

            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(judge_code))
                return Task.FromResult(SkillResult.Fail("judge_code 不能为空", WorkflowErrorCodes.PreconditionEmpty));

            // ── 级别规约（大小写/空 → 合法集合 or 安全缺省 major）──
            var normalizedLevel = (level ?? string.Empty).Trim().ToLowerInvariant();
            bool levelDefaulted;
            string effectiveLevel;
            if (normalizedLevel.Length == 0)
            {
                effectiveLevel = NcJudgeConstants.DefaultLevel;
                levelDefaulted = true;
            }
            else if (Array.IndexOf(Levels, normalizedLevel) >= 0)
            {
                effectiveLevel = normalizedLevel;
                levelDefaulted = false;
            }
            else
            {
                // ★ 未知级别不按「不影响」漏过，按安全缺省 major + 标注
                effectiveLevel = NcJudgeConstants.DefaultLevel;
                levelDefaulted = true;
            }

            // verdict 规约（可空：未给则确定性推断，非枚举值报错）
            var normalizedVerdict = (verdict ?? string.Empty).Trim().ToLowerInvariant();
            string effectiveVerdict;
            if (normalizedVerdict.Length == 0)
            {
                effectiveVerdict = NcConclusionV2Calculator.InferVerdict(result);
            }
            else if (normalizedVerdict == NcJudgeConstants.VerdictPass ||
                     normalizedVerdict == NcJudgeConstants.VerdictFail ||
                     normalizedVerdict == NcJudgeConstants.VerdictUnverifiable)
            {
                effectiveVerdict = normalizedVerdict;
            }
            else
            {
                return Task.FromResult(SkillResult.Fail(
                    $"verdict 非法：{verdict}（允许 pass/fail/unverifiable，或留空自动推断）",
                    WorkflowErrorCodes.ConfigError));
            }

            // ── 组装「完整 judge 记录」作为主输出 result ──
            // ★ 注意：业务键 "result" 这里存的是**整条记录**（下游 collect 直接用它），
            //   原始上游值另存 "payload"，报告阶段可回看。
            var record = new Dictionary<string, object>
            {
                ["judge_code"]      = judge_code,
                ["level"]           = effectiveLevel,
                ["verdict"]         = effectiveVerdict,
                ["reason"]          = reason ?? (object)string.Empty,
                ["result"]          = result ?? new object(),   // 原样保留上游值（供再推断/展示）
                ["payload"]         = result,
                ["level_defaulted"] = levelDefaulted
            };

            var outputs = new Dictionary<string, object>
            {
                ["result"] = record
            };

            _ = ct;
            return Task.FromResult(SkillResult.Ok(outputs, levelDefaulted ? 0.8 : 1.0));
        }
    }
}
