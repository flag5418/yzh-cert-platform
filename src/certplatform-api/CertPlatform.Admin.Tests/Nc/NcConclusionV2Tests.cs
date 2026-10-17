using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using CertPlatform.Shared.Workflow.Models;
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
    /// ★ stamp_judge_level 盖级单测（2026-10-16 起：三种接法 + 运行期判定 6 条）
    /// <para>M1 只连证据（有值⇒pass / 空⇒unverifiable）· M2 只连判定（true⇒pass / false⇒fail）·
    /// M3 都连（★证据优先：证据空⇒unverifiable，证据有值才按判定）·
    /// ⛔ 两端口都不接 ⇒ CONFIG_ERROR（判据 = 端口连接性，非值）。</para>
    /// </summary>
    public class StampJudgeLevelTests
    {
        private static Dictionary<string, object> RunRecord(
            object? result, string code, string? level = null, string? verdict = null,
            ISet<string>? connectedPorts = null)
        {
            // ★ 2026-10-16：judge 标识 = 节点名称 ⇒ 第 2 个实参 code 现作为 nodeTitle 注入
            //   （[FromContext]，画布上不可见）。断言 rec["judge_code"] 仍等于该 code。
            var r = StampJudgeLevelSkill.ExecuteAsync(
                result, level, verdict, null, code, connectedPorts, default).GetAwaiter().GetResult();
            Assert.True(r.Success, r.Error);
            return (Dictionary<string, object>)r.Outputs["result"]!;
        }

        [Fact]
        public void StampsLevelAndVerdict_Defaults()
        {
            // ★ 2026-10-15 新语义（M1 只连证据）：证据非空 ⇒ pass
            //   （旧语义「非空文本 ⇒ fail」已废止 —— 它把「材料在」猜成「不通过」）
            var rec = RunRecord("缺失审批记录", "J1", level: "major");
            Assert.Equal("major", (string)rec["level"]);
            Assert.Equal(NcJudgeConstants.VerdictPass, (string)rec["verdict"]);
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
            var r = StampJudgeLevelSkill.ExecuteAsync(
                "x", "minor", "maybe", null, "J6", null, default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Contains("verdict 非法", r.Error);
        }

        [Fact]
        public void EmptyNodeTitle_Fails()
        {
            // ★ 2026-10-16：judge 标识取自节点名称（nodeTitle，第 5 个参数）⇒ 空名报配置错。
            var r = StampJudgeLevelSkill.ExecuteAsync(
                "x", null, null, null, "", null, default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
            Assert.Contains("未命名", r.Error);
        }

        // ═══════════════════════════════════════════════════
        // 三种接法 + 运行期判定 4 条（2026-10-15 用户定稿）
        // ═══════════════════════════════════════════════════

        /// <summary>⛔ 两端口都不接 ⇒ CONFIG_ERROR（不变量：「证据空 + 判定也空」不允许存在）</summary>
        [Fact]
        public void NoInputPortsConnected_ConfigError()
        {
            var r = StampJudgeLevelSkill.ExecuteAsync(
                null, "major", null, null, "J7", new HashSet<string>(), default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
            Assert.Contains("未接任何输入", r.Error);
        }

        /// <summary>M1：证据端口已接但上游返回空 ⇒ unverifiable（「材料没拿到」≠「配置错」）</summary>
        [Fact]
        public void EvidenceConnectedButEmpty_Unverifiable()
        {
            var rec = RunRecord("  ", "J8", level: "minor",
                connectedPorts: new HashSet<string> { "result" });
            Assert.Equal(NcJudgeConstants.VerdictUnverifiable, (string)rec["verdict"]);
        }

        /// <summary>M2：判定 bool true（连线以 "True" 形态到达）⇒ pass</summary>
        [Fact]
        public void VerdictTrue_Pass()
        {
            var rec = RunRecord(null, "J9", level: "major", verdict: "True",
                connectedPorts: new HashSet<string> { "verdict" });
            Assert.Equal(NcJudgeConstants.VerdictPass, (string)rec["verdict"]);
        }

        /// <summary>M2：判定 bool false ⇒ fail</summary>
        [Fact]
        public void VerdictFalse_Fail()
        {
            var rec = RunRecord(null, "J10", level: "major", verdict: "False",
                connectedPorts: new HashSet<string> { "verdict" });
            Assert.Equal(NcJudgeConstants.VerdictFail, (string)rec["verdict"]);
        }

        /// <summary>② 判定值看不懂 ⇒ CONFIG_ERROR（⛔ 不静默猜 fail —— 旧规则已废止）</summary>
        [Fact]
        public void VerdictUnrecognized_ConfigError()
        {
            var r = StampJudgeLevelSkill.ExecuteAsync(
                "有证据", "major", "1", null, "J11",
                new HashSet<string> { "result", "verdict" }, default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
            Assert.Contains("verdict 非法", r.Error);
        }

        /// <summary>M3：证据 + 判定都连，★证据有值 ⇒ 按判定（证据非空文本不推翻 false）</summary>
        [Fact]
        public void BothConnected_EvidencePresent_VerdictWins()
        {
            var rec = RunRecord("材料齐全", "J12", level: "major", verdict: "false",
                connectedPorts: new HashSet<string> { "result", "verdict" });
            Assert.Equal(NcJudgeConstants.VerdictFail, (string)rec["verdict"]);
        }

        /// <summary>M3：证据 + 判定都连，★证据取到空 ⇒ unverifiable（★ 2026-10-16 证据优先，判定不介入）</summary>
        [Fact]
        public void BothConnected_EvidenceEmpty_Unverifiable()
        {
            var rec = RunRecord("  ", "J13", level: "major", verdict: "false",
                connectedPorts: new HashSet<string> { "result", "verdict" });
            Assert.Equal(NcJudgeConstants.VerdictUnverifiable, (string)rec["verdict"]);
        }

        /// <summary>非法 verdict 优先于「证据为空」—— 配置错必须报出来，⛔ 不被证据判空吞掉</summary>
        [Fact]
        public void BothConnected_EvidenceEmptyButVerdictIllegal_ConfigError()
        {
            var r = StampJudgeLevelSkill.ExecuteAsync(
                "  ", "major", "maybe", null, "J14",
                new HashSet<string> { "result", "verdict" }, default).GetAwaiter().GetResult();
            Assert.False(r.Success);
            Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
            Assert.Contains("verdict 非法", r.Error);
        }

        /// <summary>M2 可达性：result 允许空（否则「只连判定」会被 CollectMissingInputs 判成缺配置）</summary>
        [Fact]
        public void ResultParam_AllowsEmpty_ForM2()
        {
            var ps = typeof(StampJudgeLevelSkill)
                .GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Static)!
                .GetParameters();

            var result = ps.First(p => p.Name == "result")
                .GetCustomAttribute<SkillParamAttribute>()!;
            var verdict = ps.First(p => p.Name == "verdict")
                .GetCustomAttribute<SkillParamAttribute>()!;

            Assert.Equal(SkillParamEmptyPolicy.Allow, result.AllowEmpty);
            Assert.Equal(SkillParamEmptyPolicy.Allow, verdict.AllowEmpty);

            // ★ 删除即红（2026-10-16）：judge_code 已从画布移除 —— 标识改取节点名称。
            Assert.DoesNotContain(ps, p => p.Name == "judge_code");
            Assert.NotNull(ps.First(p => p.Name == "nodeTitle")
                .GetCustomAttribute<FromContextAttribute>());
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
            var a = StampJudgeLevelSkill.ExecuteAsync("缺审批", "major", "fail", null, "J1", default).GetAwaiter().GetResult();
            var b = StampJudgeLevelSkill.ExecuteAsync("有值", "none", "pass", null, "J2", default).GetAwaiter().GetResult();

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
            var a = StampJudgeLevelSkill.ExecuteAsync("缺审批", "major", "fail", null, "J1", default).GetAwaiter().GetResult();
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
