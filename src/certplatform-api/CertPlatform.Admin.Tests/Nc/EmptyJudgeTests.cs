using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Nc;
using Xunit;

namespace CertPlatform.Admin.Tests.Nc;

/// <summary>
/// ★ 空值判定逻辑单测（3 个存在性 Skill 的核心判定）
/// <para>重点：占位符识别、表格行数解析、必需 vs 可选的区分。</para>
/// </summary>
public class EmptyJudgeTests
{
    // ═══════════════════════════════════════════════
    // 字段空值：占位符识别
    // ═══════════════════════════════════════════════
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("　")]                    // 全角空格
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("—")]                    // 破折号
    [InlineData("N/A")]
    [InlineData("n/a")]
    [InlineData("NA")]
    [InlineData("无")]
    [InlineData("无内容")]
    [InlineData("暂无")]
    [InlineData("暂无数据")]
    [InlineData("待填写")]
    [InlineData("待补充")]
    [InlineData("未填写")]
    [InlineData("未上传")]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("／／")]
    public void IsBlankText_PlaceholderValues_AreBlank(string? input)
        => Assert.True(EmptyJudge.IsBlankText(input));

    [Theory]
    [InlineData("5年")]
    [InlineData("2026-03-15")]
    [InlineData("ISO9001")]
    [InlineData("无异常")]        // ★ 含"无"但不是纯占位 → 不算空
    [InlineData("未经批准")]        // ★ 含"未"但不是占位 → 不算空
    [InlineData("0")]              // ★ "0" 是有效值
    [InlineData("否")]
    public void IsBlankText_RealValues_NotBlank(string input)
        => Assert.False(EmptyJudge.IsBlankText(input));

    [Fact]
    public void IsBlankText_RealValueWithSpaces_NotBlank()
    {
        // ★ 前后有空格但内容有效
        Assert.False(EmptyJudge.IsBlankText("  合格  "));
    }

    // ═══════════════════════════════════════════════
    // 表格 JSON 行数解析
    // ═══════════════════════════════════════════════
    [Fact]
    public void ParseTable_ArrayWithRows_ReturnsCount()
    {
        var json = """
        [
          {"序号":1,"审核员":"张三","结论":"有效"},
          {"序号":2,"审核员":"李四","结论":"有改进"}
        ]
        """;
        var (rows, cols) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(2, rows);
        Assert.Equal(3, cols);
    }

    [Fact]
    public void ParseTable_WrappedInRowsKey_ReturnsCount()
    {
        // ★ 实际存储常见结构：{"rows":[...]}
        var json = """{"rows":[{"a":"1"},{"a":"2"},{"a":"3"}]}""";
        var (rows, _) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(3, rows);
    }

    [Fact]
    public void ParseTable_EmptyArray_ReturnsZero()
    {
        var (rows, cols) = IsTableEmptySkill.ParseTable("[]");
        Assert.Equal(0, rows);
        Assert.Equal(0, cols);
    }

    [Fact]
    public void ParseTable_HeaderOnlyNoDataRow_ReturnsZero()
    {
        // ★ 提取到了表头但没有数据行 —— 应判为"空"
        var json = """{"rows":[]}""";
        var (rows, _) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(0, rows);
    }

    [Fact]
    public void ParseTable_AllBlankRows_ReturnsZero()
    {
        // ★ 有行但全是空值（只有表头结构的空壳）→ 判为空
        var json = """
        [
          {"序号":"","审核员":"","结论":""},
          {"序号":null,"审核员":null,"结论":null}
        ]
        """;
        var (rows, _) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(0, rows);
    }

    [Fact]
    public void ParseTable_MixedBlankAndRealRows_CountsOnlyReal()
    {
        var json = """
        [
          {"序号":"","审核员":"","结论":""},
          {"序号":1,"审核员":"张三","结论":"有效"}
        ]
        """;
        var (rows, _) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(1, rows);
    }

    [Fact]
    public void ParseTable_InvalidJson_ReturnsZeroNotThrow()
    {
        // ★ 解析失败不能抛异常（会中断工作流），应安全判为空
        var (rows, _) = IsTableEmptySkill.ParseTable("{不是合法JSON");
        Assert.Equal(0, rows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseTable_NullOrEmptyJson_ReturnsZero(string? json)
    {
        var (rows, _) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(0, rows);
    }

    [Fact]
    public void ParseTable_DataKeyWrapped_ReturnsCount()
    {
        var json = """{"data":[{"x":1},{"x":2}]}""";
        var (rows, _) = IsTableEmptySkill.ParseTable(json);
        Assert.Equal(2, rows);
    }

    // ═══════════════════════════════════════════════
    // 三态常量一致性
    // ═══════════════════════════════════════════════
    [Fact]
    public void ThreeStates_AreDistinctConstants()
    {
        Assert.Equal("has_value",   EmptyJudge.StateHasValue);
        Assert.Equal("is_empty",    EmptyJudge.StateIsEmpty);
        Assert.Equal("not_found",   EmptyJudge.StateNotFound);
        Assert.NotEqual(EmptyJudge.StateHasValue, EmptyJudge.StateIsEmpty);
        Assert.NotEqual(EmptyJudge.StateIsEmpty,  EmptyJudge.StateNotFound);
    }

    [Fact]
    public void Result_IsEmptyCoversBothEmptyAndNotFound()
    {
        // ★ is_empty 属性 = "取不到可用值"，涵盖 is_empty 与 not_found 两态
        var hasVal = new EmptyJudge.Result { State = EmptyJudge.StateHasValue };
        var empty  = new EmptyJudge.Result { State = EmptyJudge.StateIsEmpty };
        var notFnd = new EmptyJudge.Result { State = EmptyJudge.StateNotFound };

        Assert.True(hasVal.HasValue);
        Assert.False(hasVal.IsEmpty);

        Assert.False(empty.HasValue);
        Assert.True(empty.IsEmpty);

        Assert.False(notFnd.HasValue);
        Assert.True(notFnd.IsEmpty);      // ★ not_found 也算"空"
    }

    [Fact]
    public void Result_ToOutputs_IncludesBooleanPorts()
    {
        var r = new EmptyJudge.Result
        {
            State = EmptyJudge.StateHasValue,
            RawValue = "5年", Label = "记录保存期限", RecordCount = 2
        };
        var o = r.ToOutputs();

        // ★ 三种连线性情都覆盖
        Assert.Equal(true,  o["result"]);      // 接 branch
        Assert.Equal(false, o["is_empty"]);
        Assert.Equal(true,  o["has_value"]);
        Assert.Equal("has_value", o["state"]);
        Assert.Equal("5年",   o["raw_value"]);
        Assert.Equal(2,       o["record_count"]);
    }

    [Fact]
    public void Truncate_ShortensLongText()
    {
        var long100 = new string('A', 100);
        var t = EmptyJudge.Truncate(long100, 20);
        Assert.Equal(21, t.Length);              // 20 + "…"
        Assert.EndsWith("…", t);
    }
}
