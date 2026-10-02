using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using Xunit;

namespace CertPlatform.Admin.Tests.Nc;

/// <summary>
/// ★ NcConclusionCalculator 单元测试（18 号 §九 第 9 项：校验用例）
/// <para>本类验证「结论由规则算、可复现」，AI 不参与。</para>
/// </summary>
public class NcConclusionCalculatorTests
{
    private static ConclusionRuleSnapshot Rule(
        int toBasic = 3, int reject = 5, int xProc = 2,
        bool xEnabled = true, string unverified = "cap_basic")
        => new()
        {
            LevelFull = "完全符合", LevelBasic = "基本符合", LevelReject = "不符合",
            MinorToBasicThreshold = toBasic,
            MinorRejectThreshold  = reject,
            CrossProcessEnabled   = xEnabled,
            CrossProcessThreshold = xProc,
            UnverifiedPolicy      = unverified,
            ConclusionMode        = NcConclusionMode.AiAssisted
        };

    private static DimensionResult D(int no, string conf,
        string? process = null, string? sev = null) => new()
    {
        No = no, Name = $"维度{no}", Conformity = conf,
        ProcessCode = process, SeverityIfFailed = sev
    };

    private readonly NcConclusionCalculator _calc = new();

    // ─────────────────────────────────────────────
    // 全部符合 → 完全符合
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_AllConform_ReturnsFull()
    {
        var dims = new[] { D(1, "conform"), D(2, "conform"), D(3, "conform") };
        var r = _calc.Calculate(dims, Rule());

        Assert.Equal("完全符合", r.Conclusion);
        Assert.Equal(0, r.ConclusionLevel);
        Assert.Equal("conform", r.Conformity);
        Assert.Null(r.Severity);
    }

    // ─────────────────────────────────────────────
    // 一般不符合累计 → 降档
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_MinorReachBasicThreshold_ReturnsBasic()
    {
        var dims = new[]
        {
            D(1, "conform"), D(2, "nonconform"), D(3, "nonconform"), D(4, "nonconform")
        };
        var r = _calc.Calculate(dims, Rule(toBasic: 3, reject: 5));

        Assert.Equal("基本符合", r.Conclusion);
        Assert.Equal(1, r.ConclusionLevel);
        Assert.Equal("minor", r.Severity);
        Assert.Contains("一般不符合 3 项", r.DowngradeReason);
    }

    [Fact]
    public void Calculate_MinorReachRejectThreshold_ReturnsReject()
    {
        var dims = Enumerable.Range(1, 5).Select(i => D(i, "nonconform")).ToArray();
        var r = _calc.Calculate(dims, Rule(toBasic: 3, reject: 5));

        Assert.Equal("不符合", r.Conclusion);
        Assert.Equal(2, r.ConclusionLevel);
    }

    // ─────────────────────────────────────────────
    // ★ 严重不符合：显式 major
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_ExplicitMajor_ReturnsRejectWithMajor()
    {
        var dims = new[] { D(1, "conform"), D(2, "nonconform", sev: "major") };
        var r = _calc.Calculate(dims, Rule());

        Assert.Equal("不符合", r.Conclusion);
        Assert.Equal("major", r.Severity);
        Assert.Contains("严重不符合", r.DowngradeReason);
    }

    // ─────────────────────────────────────────────
    // ★ 跨过程失效升级（单节点多维度范式独有能力）
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_SameProcessMultiFail_UpgradesToMajor()
    {
        // 7.5 成文信息下：审批记录 + 发放记录 同时缺失
        var dims = new[]
        {
            D(1, "conform",                        process: null),
            D(2, "nonconform", process: "P-7.5"),  // 文件审批
            D(3, "nonconform", process: "P-7.5")   // 文件发放
        };
        var r = _calc.Calculate(dims, Rule(xEnabled: true, xProc: 2));

        Assert.Equal("不符合", r.Conclusion);          // ★ 升级
        Assert.Equal("major", r.Severity);              // ★ 升为严重
        Assert.True(r.CrossProcessFailures.ContainsKey("P-7.5"));
        Assert.Equal(new[] { 2, 3 }, r.CrossProcessFailures["P-7.5"]);
    }

    [Fact]
    public void Calculate_DifferentProcessFailures_NoCrossProcessUpgrade()
    {
        // 不同过程各 1 个不符合 → 不触发升级，只按累计数判
        var dims = new[]
        {
            D(1, "nonconform", process: "P-7.5"),
            D(2, "nonconform", process: "P-8.4")
        };
        var r = _calc.Calculate(dims, Rule(toBasic: 3, xEnabled: true, xProc: 2));

        Assert.Empty(r.CrossProcessFailures);          // ★ 无跨过程
        Assert.NotEqual("major", r.Severity);          // ★ 不升严重
    }

    [Fact]
    public void Calculate_CrossProcessDisabled_SkipsUpgrade()
    {
        var dims = new[]
        {
            D(1, "nonconform", process: "P-7.5"),
            D(2, "nonconform", process: "P-7.5")
        };
        var r = _calc.Calculate(dims, Rule(xEnabled: false, toBasic: 3));

        Assert.Empty(r.CrossProcessFailures);
        Assert.NotEqual("major", r.Severity);
    }

    [Fact]
    public void Calculate_NoProcessCode_SkipsCrossProcessCheck()
    {
        // 无 ProcessCode 的维度不参与跨过程判定
        var dims = new[] { D(1, "nonconform"), D(2, "nonconform") };
        var r = _calc.Calculate(dims, Rule(xEnabled: true, xProc: 2));

        Assert.Empty(r.CrossProcessFailures);
    }

    // ─────────────────────────────────────────────
    // ★ 未检查项（unverifiable ≠ nonconform）
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_Unverifiable_CapsAtBasic()
    {
        var dims = new[] { D(1, "conform"), D(2, "unverifiable") };
        var r = _calc.Calculate(dims, Rule(unverified: "cap_basic"));

        Assert.Equal("基本符合", r.Conclusion);          // ★ 封顶，不能给「完全符合」
        Assert.Equal(new[] { 2 }, r.Unverified);
        Assert.Contains("无法判断", r.DowngradeReason);
    }

    [Fact]
    public void Calculate_UnverifiablePolicyReject_ReturnsReject()
    {
        var dims = new[] { D(1, "conform"), D(2, "unverifiable") };
        var r = _calc.Calculate(dims, Rule(unverified: "reject"));

        Assert.Equal("不符合", r.Conclusion);
    }

    [Fact]
    public void Calculate_UnverifiablePolicyIgnore_CanReturnFull()
    {
        var dims = new[] { D(1, "conform"), D(2, "unverifiable") };
        var r = _calc.Calculate(dims, Rule(unverified: "ignore"));

        Assert.Equal("完全符合", r.Conclusion);          // ★ 不处理 → 可给完全符合
    }

    // ─────────────────────────────────────────────
    // na（不适用）不计入不符合
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_NotApplicable_NotCountedAsNonconform()
    {
        var dims = new[] { D(1, "conform"), D(2, "na"), D(3, "na") };
        var r = _calc.Calculate(dims, Rule());

        Assert.Equal("完全符合", r.Conclusion);
        Assert.Equal(2, r.Stats.NotApplicable);
        Assert.Equal(0, r.Stats.Nonconform);
    }

    [Fact]
    public void Calculate_AllNotApplicable_ConformityIsNa()
    {
        var dims = new[] { D(1, "na"), D(2, "na") };
        var r = _calc.Calculate(dims, Rule());

        Assert.Equal("na", r.Conformity);
        Assert.Equal("完全符合", r.Conclusion);
    }

    // ─────────────────────────────────────────────
    // 边界
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_EmptyDimensions_ReturnsReject()
    {
        var r = _calc.Calculate(Array.Empty<DimensionResult>(), Rule());

        Assert.Equal("不符合", r.Conclusion);
        Assert.Equal("major", r.Severity);
        Assert.Contains("无任何维度", r.DowngradeReason);
    }

    [Fact]
    public void Calculate_ThresholdBoundary_IsInclusive()
    {
        // 恰好等于阈值 → 应降档（>= 判定）
        var dims = Enumerable.Range(1, 3).Select(i => D(i, "nonconform")).ToArray();
        var r = _calc.Calculate(dims, Rule(toBasic: 3, reject: 5));

        Assert.Equal("基本符合", r.Conclusion);          // ★ 边界含等号
    }

    [Fact]
    public void Calculate_JustBelowThreshold_StaysFull()
    {
        var dims = Enumerable.Range(1, 2).Select(i => D(i, "nonconform")).ToArray();
        var r = _calc.Calculate(dims, Rule(toBasic: 3, reject: 5));

        Assert.Equal("完全符合", r.Conclusion);
    }

    // ─────────────────────────────────────────────
    // ★ 可复现性（18 号 §3.3 核心诉求：AI 不参与 → 同输入必同输出）
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_IsDeterministic()
    {
        var dims = new[]
        {
            D(1, "conform"), D(2, "nonconform", process: "P-7.5"), D(3, "nonconform", process: "P-7.5")
        };
        var rule = Rule();

        var r1 = _calc.Calculate(dims, rule);
        var r2 = _calc.Calculate(dims, rule);
        var r3 = _calc.Calculate(dims, rule);

        Assert.Equal(r1.Conclusion, r2.Conclusion);
        Assert.Equal(r2.Conclusion, r3.Conclusion);
        Assert.Equal(r1.Severity, r2.Severity);
        Assert.Equal(r1.DowngradeReason, r3.DowngradeReason);
    }

    // ─────────────────────────────────────────────
    // 规则快照透传（审计用）
    // ─────────────────────────────────────────────
    [Fact]
    public void Calculate_CarriesRuleSnapshotAndSource()
    {
        var rule = Rule();
        rule.SourceRef     = "CNCA-管理体系认证实施规则 2023 版 第 9.2 条";
        rule.ManualRequired = true;

        var r = _calc.Calculate(new[] { D(1, "conform") }, rule);

        Assert.Equal("CNCA-管理体系认证实施规则 2023 版 第 9.2 条", r.RuleSource);
        Assert.True(r.ManualRequired);
    }
}
