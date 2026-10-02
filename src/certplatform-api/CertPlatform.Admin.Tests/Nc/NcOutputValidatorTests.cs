using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using Xunit;

namespace CertPlatform.Admin.Tests.Nc;

/// <summary>
/// ★ NcOutputValidator 单元测试（18 号 §3.5 · C1-C7）
/// <para>核心目的：验证「AI 幻觉能被拦下来」+「AI 结论永远不能覆盖规则结论」。</para>
/// </summary>
public class NcOutputValidatorTests
{
    private static DimensionResult D(int no, string conf, string? desc = null, string? evidence = null) => new()
    {
        No = no, Name = $"维度{no}", Conformity = conf, Desc = desc, Evidence = evidence
    };

    // ═══════════════════════════════════════════════
    // C1：维度结果非空 → 阻断
    // ═══════════════════════════════════════════════
    [Fact]
    public void C1_EmptyDimensions_Blocks()
    {
        var v = new NcOutputValidator();
        var r = v.ValidateDimensions(Array.Empty<DimensionResult>());

        Assert.False(r.Passed);
        Assert.True(r.Blocked);
        Assert.Equal(1, r.ErrorCount);
        Assert.Equal("C1", r.Items[0].Code);
    }

    [Fact]
    public void C1_NullDimensions_Blocks()
    {
        var r = new NcOutputValidator().ValidateDimensions(null!);
        Assert.True(r.Blocked);
    }

    // ═══════════════════════════════════════════════
    // C2：维度覆盖完整性 → 漏判阻断
    // ═══════════════════════════════════════════════
    [Fact]
    public void C2_MissingDimension_Blocks()
    {
        var v = new NcOutputValidator { ExpectedDimensionNos = new() { 1, 2, 3, 4 } };
        var r = v.ValidateDimensions(new[] { D(1, "conform"), D(2, "conform") });

        Assert.True(r.Blocked);                      // ★ 漏判是阻断
        var c2 = r.Items.First(i => i.Code == "C2");
        Assert.Equal("error", c2.Level);
        Assert.Contains("漏判", c2.Message);
        Assert.Contains("3、4", c2.Message);
    }

    [Fact]
    public void C2_ExtraDimension_Warns()
    {
        var v = new NcOutputValidator { ExpectedDimensionNos = new() { 1, 2 } };
        var r = v.ValidateDimensions(new[] { D(1, "conform"), D(2, "conform"), D(99, "conform") });

        Assert.True(r.Passed);                       // ★ 多出不阻断
        Assert.Contains(r.Items, i => i.Code == "C2" && i.Message.Contains("不存在"));
    }

    [Fact]
    public void C2_NoExpectationConfigured_Skips()
    {
        // 规则没配维度清单 → 跳过 C2（不误伤）
        var r = new NcOutputValidator().ValidateDimensions(new[] { D(1, "conform") });
        Assert.True(r.Passed);
    }

    // ═══════════════════════════════════════════════
    // C3：四态枚举 → 告警 + 降级
    // ═══════════════════════════════════════════════
    [Fact]
    public void C3_IllegalEnumValue_Warns()
    {
        var r = new NcOutputValidator()
            .ValidateDimensions(new[] { D(1, "基本符合") });   // ★ 不是维度级枚举值

        Assert.True(r.Passed);                                    // ★ 不阻断
        var c3 = r.Items.First(i => i.Code == "C3");
        Assert.Equal("warn", c3.Level);
        Assert.Contains("不在四态枚举", c3.Message);
    }

    [Theory]
    [InlineData("conform")]
    [InlineData("nonconform")]
    [InlineData("na")]
    [InlineData("unverifiable")]
    public void C3_AllFourValidStates_NoWarning(string state)
    {
        var r = new NcOutputValidator().ValidateDimensions(new[] { D(1, state) });
        Assert.DoesNotContain(r.Items, i => i.Code == "C3");
    }

    // ═══════════════════════════════════════════════
    // C4：不符合必须有描述
    // ═══════════════════════════════════════════════
    [Fact]
    public void C4_NonconformWithoutDesc_Warns()
    {
        var r = new NcOutputValidator()
            .ValidateDimensions(new[] { D(1, "nonconform", desc: null) });

        Assert.Contains(r.Items, i => i.Code == "C4" && i.Message.Contains("没有描述"));
    }

    [Fact]
    public void C4_NonconformWithDesc_NoWarning()
    {
        var r = new NcOutputValidator()
            .ValidateDimensions(new[] { D(1, "nonconform", desc: "3份文件无编号") });
        Assert.DoesNotContain(r.Items, i => i.Code == "C4");
    }

    // ═══════════════════════════════════════════════
    // ★ C5：证据文件交叉验证（反幻觉 · 核心）
    // ═══════════════════════════════════════════════
    [Fact]
    public void C5_FabricatedEvidenceFile_Warns()
    {
        var v = new NcOutputValidator
        {
            KnownSourceFiles = new(StringComparer.OrdinalIgnoreCase) { "质量手册.pdf", "程序文件.pdf" }
        };
        // ★ AI 引用了不存在的文件
        var r = v.ValidateDimensions(new[]
        {
            D(1, "nonconform", desc: "缺签字", evidence: "内审报告2025.pdf 第3页")
        });

        Assert.Contains(r.Items, i => i.Code == "C5" && i.Message.Contains("编造"));
    }

    [Fact]
    public void C5_RealEvidenceFile_NoWarning()
    {
        var v = new NcOutputValidator
        {
            KnownSourceFiles = new(StringComparer.OrdinalIgnoreCase) { "质量手册.pdf" }
        };
        var r = v.ValidateDimensions(new[]
        {
            D(1, "nonconform", desc: "缺签字", evidence: "质量手册.pdf 第5行")
        });
        Assert.DoesNotContain(r.Items, i => i.Code == "C5");
    }

    [Fact]
    public void C5_EmptyKnownFiles_SkipsValidation()
    {
        // ★ 依赖 B4 修复；未修复时集合为空 → 跳过（不误伤）
        var v = new NcOutputValidator { KnownSourceFiles = new() };
        var r = v.ValidateDimensions(new[]
        {
            D(1, "nonconform", desc: "x", evidence: "任意文件.pdf 第1页")
        });
        Assert.DoesNotContain(r.Items, i => i.Code == "C5");
    }

    [Fact]
    public void C5_PartialMatch_StillWarns()
    {
        // 两份文件，一真一假 → 应告警
        var v = new NcOutputValidator { KnownSourceFiles = new() { "质量手册.pdf" } };
        var r = v.ValidateDimensions(new[]
        {
            D(1, "nonconform", desc: "x", evidence: "质量手册.pdf 第1页; 捏造文件.pdf 第2页")
        });
        Assert.Contains(r.Items, i => i.Code == "C5");
    }

    [Fact]
    public void C5_NoFilenameInEvidence_Skips()
    {
        var v = new NcOutputValidator { KnownSourceFiles = new() { "质量手册.pdf" } };
        var r = v.ValidateDimensions(new[]
        {
            D(1, "nonconform", desc: "x", evidence: "第3章第2节")
        });
        Assert.DoesNotContain(r.Items, i => i.Code == "C5");
    }

    // ═══════════════════════════════════════════════
    // C6：理由不得越界
    // ═══════════════════════════════════════════════
    [Fact]
    public void C6_ReasonCitesIllegalDimension_Warns()
    {
        var r = new NcOutputValidator()
            .ValidateConclusion("维度2和维度5均不符合", new[] { 2, 3 }, null, "基本符合");

        var c6 = r.Items.First(i => i.Code == "C6");
        Assert.Contains("5", c6.Message);
        Assert.Contains("已剥离", c6.Message);
    }

    [Fact]
    public void C6_ReasonWithinBasis_NoWarning()
    {
        var r = new NcOutputValidator()
            .ValidateConclusion("维度2与维度3不符合", new[] { 2, 3 }, null, "基本符合");
        Assert.DoesNotContain(r.Items, i => i.Code == "C6" && i.Message.Contains("越界"));
    }

    [Fact]
    public void C6_EmptyReason_Warns()
    {
        var r = new NcOutputValidator().ValidateConclusion("", new[] { 2 }, null, "基本符合");
        Assert.Contains(r.Items, i => i.Message.Contains("未产出结论理由"));
    }

    [Fact]
    public void C6_ExtractDimensionNos_ParsesBothFormats()
    {
        var a = NcOutputValidator.ExtractDimensionNos("维度2与维度 5 不符合");
        var b = NcOutputValidator.ExtractDimensionNos("第3项和第7条有问题");
        Assert.Equal(new[] { 2, 5 }, a);
        Assert.Equal(new[] { 3, 7 }, b);
    }

    // ═══════════════════════════════════════════════
    // ★ C7：AI 结论永远不能覆盖规则结论（最关键）
    // ═══════════════════════════════════════════════
    [Fact]
    public void C7_AiContradictsRule_WarnsAndKeepsRule()
    {
        var r = new NcOutputValidator()
            .ValidateConclusion("理由", new[] { 2 }, aiConclusion: "完全符合", ruleConclusion: "不符合");

        var c7 = r.Items.First(i => i.Code == "C7");
        Assert.Equal("warn", c7.Level);
        Assert.Contains("完全符合", c7.Message);
        Assert.Contains("不符合", c7.Message);
        Assert.Contains("规则结论为准", c7.Message);
    }

    [Fact]
    public void C7_AiAgreesWithRule_NoWarning()
    {
        var r = new NcOutputValidator()
            .ValidateConclusion("理由", new[] { 2 }, aiConclusion: "不符合", ruleConclusion: "不符合");
        Assert.DoesNotContain(r.Items, i => i.Code == "C7");
    }

    [Fact]
    public void C7_AiDidNotOutputConclusion_NoWarning()
    {
        // ★ 正常路径：AI 不产出结论（只写理由）→ C7 不触发
        var r = new NcOutputValidator()
            .ValidateConclusion("理由", new[] { 2 }, aiConclusion: null, ruleConclusion: "基本符合");
        Assert.DoesNotContain(r.Items, i => i.Code == "C7");
    }

    // ═══════════════════════════════════════════════
    // 汇总行为
    // ═══════════════════════════════════════════════
    [Fact]
    public void Summary_CountsAreCorrect()
    {
        var v = new NcOutputValidator
        {
            ExpectedDimensionNos = new() { 1, 2, 3 },
            KnownSourceFiles = new() { "质量手册.pdf" }
        };
        var r = v.ValidateDimensions(new[]
        {
            D(1, "nonconform", desc: null, evidence: "质量手册.pdf"),   // C4
            D(2, "非法值"),                                             // C3
            // 漏了 3                                                    // C2
        });

        Assert.True(r.Blocked);                       // 因 C2
        Assert.True(r.ErrorCount >= 1);
        Assert.True(r.WarnCount >= 2);
        Assert.Contains(r.Items, i => i.Code == "C2" && i.Level == "error");
        Assert.Contains(r.Items, i => i.Code == "C3" && i.Level == "warn");
        Assert.Contains(r.Items, i => i.Code == "C4" && i.Level == "warn");
    }
}
