using System;
using System.Collections.Generic;
using System.Linq;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using Xunit;

namespace CertPlatform.Admin.Tests.Nc
{
    /// <summary>
    /// ★ NC 多 judge 结论判定 v2 单元测试（2026-10-14 用户裁决后的判定矩阵）
    /// <para>验证：① 判定矩阵 6 行 ② 级别缺省按 major（安全侧）③ 同输入三连跑 100% 一致
    /// ④ 可复现（纯函数，AI 不参与）⑤ collect 聚合形态归一。</para>
    /// </summary>
    public class NcConclusionV2Tests
    {
        private readonly NcConclusionV2Calculator _calc = new();

        private static JudgeConclusionMatrix Matrix(
            int reject = 2, int tolerated = -1, string unverified = "cap_basic",
            string[]? core = null) => new()
        {
            MinorRejectThreshold = reject,
            MinorTolerated = tolerated,
            UnverifiedPolicy = unverified,
            CoreLevels = core?.ToList() ?? new(NcJudgeConstants.DefaultCoreLevels)
        };

        private static JudgeResult J(string code, string level, string verdict,
            string? reason = null, bool defaulted = false) => new()
        {
            JudgeCode = code, Level = level, Verdict = verdict,
            Reason = reason, LevelDefaulted = defaulted
        };

        // ═══════════════════════════════════════════════════
        // 判定矩阵 6 行
        // ═══════════════════════════════════════════════════

        /// <summary>① 全 pass ⇒ pass（推荐认证）</summary>
        [Fact]
        public void Matrix_AllPass_ReturnsPass()
        {
            var judges = new[] { J("J1", "critical", "pass"), J("J2", "none", "pass"), J("J3", "minor", "pass") };
            var r = _calc.Calculate(judges, Matrix());
            Assert.Equal(NcJudgeConstants.ConclusionPass, r.Conclusion);
            Assert.Equal(0, r.ConclusionLevel);
        }

        /// <summary>② 有 major fail ⇒ fail（不通过）—— 一票否决</summary>
        [Fact]
        public void Matrix_MajorFail_ReturnsFail()
        {
            var judges = new[] { J("J1", "critical", "pass"), J("J2", "major", "fail", "缺少体系文件") };
            var r = _calc.Calculate(judges, Matrix());
            Assert.Equal(NcJudgeConstants.ConclusionFail, r.Conclusion);
            Assert.Contains("严重不符合", r.DowngradeReason);
        }

        /// <summary>③ critical 也并入 major 判定 ⇒ fail</summary>
        [Fact]
        public void Matrix_CriticalFail_ReturnsFail()
        {
            var judges = new[] { J("J1", "critical", "fail") };
            var r = _calc.Calculate(judges, Matrix());
            Assert.Equal(NcJudgeConstants.ConclusionFail, r.Conclusion);
        }

        /// <summary>④ unverifiable + reject 策略 ⇒ fail</summary>
        [Fact]
        public void Matrix_UnverifiableReject_ReturnsFail()
        {
            var judges = new[] { J("J1", "major", "pass"), J("J2", "minor", "unverifiable") };
            var r = _calc.Calculate(judges, Matrix(unverified: "reject"));
            Assert.Equal(NcJudgeConstants.ConclusionFail, r.Conclusion);
        }

        /// <summary>⑤ 核心级 fail ≥ 升级阈值 ⇒ improvement_required（用户场景：3 核心不过）</summary>
        [Fact]
        public void Matrix_CoreFailReachThreshold_ReturnsImprovement()
        {
            var judges = new[]
            {
                J("J1", "major", "fail"), J("J2", "major", "fail"), J("J3", "minor", "fail")
            };
            var r = _calc.Calculate(judges, Matrix(reject: 2));
            Assert.Equal(NcJudgeConstants.ConclusionImprovementRequired, r.Conclusion);
            Assert.Contains("复审阈值", r.DowngradeReason);
        }

        /// <summary>⑥ 核心级 fail 未达阈值 ⇒ pass_with_nc（用户场景：核心 3 过，另有 2 非核心不过仍带 NC 通过）</summary>
        [Fact]
        public void Matrix_CoreFailBelowThreshold_ReturnsPassWithNc()
        {
            // 3 核心全过，仅 2 条 observation 不过 ⇒ 核心 fail=0，observation fail=2
            var judges = new[]
            {
                J("C1", "major", "pass"), J("C2", "major", "pass"), J("C3", "minor", "pass"),
                J("O1", "observation", "fail"), J("O2", "observation", "fail")
            };
            var r = _calc.Calculate(judges, Matrix(tolerated: -1));   // 默认不限量
            Assert.Equal(NcJudgeConstants.ConclusionPass, r.Conclusion);   // ★ 2 轻微不过仍推荐认证（用户示例）
        }

        /// <summary>⑦ 非核心 fail 超过容忍数 ⇒ pass_with_nc</summary>
        [Fact]
        public void Matrix_ObsFailExceedTolerance_ReturnsPassWithNc()
        {
            var judges = new[]
            {
                J("C1", "major", "pass"),
                J("O1", "observation", "fail"), J("O2", "observation", "fail"), J("O3", "observation", "fail")
            };
            var r = _calc.Calculate(judges, Matrix(tolerated: 2));
            Assert.Equal(NcJudgeConstants.ConclusionPassWithNc, r.Conclusion);
            Assert.Contains("容忍数", r.DowngradeReason);
        }

        /// <summary>⑧ unverifiable + cap_basic ⇒ 封顶 pass_with_nc（即使全 pass 也不能给推荐认证）</summary>
        [Fact]
        public void Matrix_UnverifiableCapCapsAtPassWithNc()
        {
            var judges = new[] { J("C1", "major", "pass"), J("U1", "minor", "unverifiable") };
            var r = _calc.Calculate(judges, Matrix(unverified: "cap_basic"));
            Assert.Equal(NcJudgeConstants.ConclusionPassWithNc, r.Conclusion);
            Assert.Contains("封顶", r.DowngradeReason);
        }

        /// <summary>⑨ 空 judges ⇒ fail（不能给正向结论）</summary>
        [Fact]
        public void Matrix_EmptyJudges_ReturnsFail()
        {
            var r = _calc.Calculate(Array.Empty<JudgeResult>(), Matrix());
            Assert.Equal(NcJudgeConstants.ConclusionFail, r.Conclusion);
        }

        // ═══════════════════════════════════════════════════
        // 级别缺省（安全侧）
        // ═══════════════════════════════════════════════════

        /// <summary>★ 漏盖级别 ⇒ 按 major 计算（安全方向），并标注 level_defaulted</summary>
        [Fact]
        public void Default_MissingLevel_TreatedAsMajor()
        {
            var judges = new[] { J("J1", "", "fail") };   // level 空
            var r = _calc.Calculate(judges, Matrix());
            // major fail ⇒ 一票否决
            Assert.Equal(NcJudgeConstants.ConclusionFail, r.Conclusion);
            Assert.Equal(1, r.Stats.LevelsDefaulted);
        }

        /// <summary>缺省时统计的 fail_by_level 归到 major</summary>
        [Fact]
        public void Default_MissingLevel_BucketedAsMajor()
        {
            var judges = new[] { J("J1", "", "fail") };
            var r = _calc.Calculate(judges, Matrix());
            Assert.Equal(1, r.Stats.FailByLevel[NcJudgeConstants.LevelMajor]);
        }

        // ═══════════════════════════════════════════════════
        // 可复现（纯函数，AI 不参与 ⇒ 同输入必同输出）
        // ═══════════════════════════════════════════════════

        [Fact]
        public void Deterministic_SameInputThreeRuns()
        {
            var judges = new[]
            {
                J("J1", "major", "pass"), J("J2", "minor", "fail"),
                J("J3", "observation", "fail"), J("J4", "minor", "unverifiable")
            };
            var m = Matrix(reject: 2, tolerated: 1, unverified: "cap_basic");

            var r1 = _calc.Calculate(judges, m);
            var r2 = _calc.Calculate(judges, m);
            var r3 = _calc.Calculate(judges, m);

            Assert.Equal(r1.Conclusion, r2.Conclusion);
            Assert.Equal(r2.Conclusion, r3.Conclusion);
            Assert.Equal(r1.ConclusionLevel, r3.ConclusionLevel);
            Assert.Equal(r1.DowngradeReason, r3.DowngradeReason);
        }

        // ═══════════════════════════════════════════════════
        // 规则快照（审计可回溯）
        // ═══════════════════════════════════════════════════

        [Fact]
        public void CarriesRuleSnapshot()
        {
            var m = new JudgeConclusionMatrix
            {
                MinorRejectThreshold = 4,
                MinorTolerated = 3,
                UnverifiedPolicy = "reject",
                CoreLevels = new() { NcJudgeConstants.LevelMajor },
                RuleSource = "CNCA 2023 §9.2"
            };
            var r = _calc.Calculate(new[] { J("J1", "major", "fail") }, m);

            Assert.Equal("CNCA 2023 §9.2", r.RuleSource);
            Assert.Equal(4, (int)r.RuleSnapshot["minor_reject_threshold"]!);
            Assert.Equal(3, (int)r.RuleSnapshot["minor_tolerated"]!);
        }
    }

    /// <summary>
    /// ★ stamp_judge_level 盖级单测（重点：主输出=完整记录、缺省 major、verdict 推断）
    /// </summary>
    public class StampJudgeLevelTests
    {
        private static Dictionary<string, object> RunRecord(
            object? result, string code, string? level = null, string? verdict = null)
        {
            var r = StampJudgeLevelSkill.ExecuteAsync(
                result, code, level, verdict, null, default).GetAwaiter().GetResult();
            Assert.True(r.Success, r.Error);
            return (Dictionary<string, object>)r.Outputs["result"]!;
        }

        [Fact]
        public void StampsLevelAndVerdict_Defaults()
        {
            var rec = RunRecord("缺失审批记录", "J1", level: "major");
            Assert.Equal("major", (string)rec["level"]);
            Assert.Equal("fail", (string)rec["verdict"]);   // 非空文本 ⇒ fail（确定性推断）
            Assert.Equal("J1", (string)rec["judge_code"]);
            Assert.Equal(false, (bool)rec["level_defaulted"]);
        }

        [Fact]
        public void MissingLevel_DefaultsToMajor_Safe()
        {
            var rec = RunRecord("缺材料", "J2", level: null);
            Assert.Equal(NcJudgeConstants.LevelMajor, (string)rec["level"]);
            Assert.Equal(true, (bool)rec["level_defaulted"]);
        }

        [Fact]
        public void UnknownLevel_FallsBackToMajor()
        {
            var rec = RunRecord("x", "J3", level: "super_bad");
            Assert.Equal(NcJudgeConstants.LevelMajor, (string)rec["level"]);
            Assert.Equal(true, (bool)rec["level_defaulted"]);
        }

        [Fact]
        public void NullResult_VerdictUnverifiable()
        {
            var rec = RunRecord(null, "J4", level: "minor");
            Assert.Equal(NcJudgeConstants.VerdictUnverifiable, (string)rec["verdict"]);
        }

        [Fact]
        public void ExplicitVerdict_Wins()
        {
            var rec = RunRecord("有值", "J5", level: "minor", verdict: "pass");
            Assert.Equal(NcJudgeConstants.VerdictPass, (string)rec["verdict"]);
        }

        [Fact]
        public void InvalidVerdict_Fails()
        {
            var r = StampJudgeLevelSkill.ExecuteAsync("x", "J6", "minor", "maybe", null, default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Contains("verdict 非法", r.Error);
        }

        [Fact]
        public void EmptyJudgeCode_Fails()
        {
            var r = StampJudgeLevelSkill.ExecuteAsync("x", "", null, null, null, default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Contains("judge_code", r.Error);
        }
    }

    /// <summary>
    /// ★ collect_judges 聚合单测（重点：三种形态归一、全空 Fail、输出 JSON 字符串）
    /// </summary>
    public class CollectJudgesTests
    {
        private static SkillResult Collect(params object?[] slots)
        {
            object? s(int i) => i < slots.Length ? slots[i] : null;
            return CollectJudgesSkill.ExecuteAsync(
                s(0), s(1), s(2), s(3), s(4), s(5), s(6), s(7), default).GetAwaiter().GetResult();
        }

        [Fact]
        public void AggregatesStampedRecords_ToJson()
        {
            var a = StampJudgeLevelSkill.ExecuteAsync("缺审批", "J1", "major", null, null, default).GetAwaiter().GetResult();
            var b = StampJudgeLevelSkill.ExecuteAsync("有值", "J2", "none", null, null, default).GetAwaiter().GetResult();

            var r = Collect(a.Outputs["result"], b.Outputs["result"]);
            Assert.True(r.Success, r.Error);
            Assert.Equal(2, (int)r.Outputs["judges_count"]);
            var json = (string)r.Outputs["judges"]!;
            Assert.Contains("\"judge_code\":\"J1\"", json);
            Assert.Contains("\"judge_code\":\"J2\"", json);
        }

        [Fact]
        public void StampsStampedButNullPayloads_Skipped()
        {
            var a = StampJudgeLevelSkill.ExecuteAsync("缺审批", "J1", "major", null, null, default).GetAwaiter().GetResult();
            var r = Collect(a.Outputs["result"], null, null, null, null, null, null, null);
            Assert.True(r.Success);
            Assert.Equal(1, (int)r.Outputs["judges_count"]);
        }

        [Fact]
        public void AllEmpty_Fails()
        {
            var r = Collect(null, null, null, null, null, null, null, null);
            Assert.False(r.Success);
            Assert.Contains("未收到任何 judge", r.Error);
        }

        [Fact]
        public void BareScalar_FallsBackDefaultMajor()
        {
            var r = Collect("缺材料", null, null, null, null, null, null, null);
            Assert.True(r.Success, r.Error);
            var json = (string)r.Outputs["judges"]!;
            Assert.Contains("major", json);
            Assert.Contains("judge_1", json);
            Assert.Equal(1, ((string[])r.Outputs["warnings"]!).Length);
        }

        [Fact]
        public void StampedJsonString_Input()
        {
            // 旧链路/测试：槽位直接塞 stamp 记录 JSON 字符串
            var r = Collect("{\"judge_code\":\"J1\",\"level\":\"minor\",\"verdict\":\"fail\",\"result\":\"缺\"}", null);
            Assert.True(r.Success, r.Error);
            var json = (string)r.Outputs["judges"]!;
            Assert.Contains("J1", json);
            Assert.Contains("minor", json);
        }
    }
}
