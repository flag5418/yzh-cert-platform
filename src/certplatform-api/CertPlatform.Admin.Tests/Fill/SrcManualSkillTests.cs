using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills.Fill;
using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// <see cref="SrcManualSkill"/> 的契约测试（对应 39 号 §5.4 的 T1~T3）。
///
/// <para>重点：<c>hit</c> <b>必须恒为 false</b> —— 它决定来源链（<c>firstHit</c>）会不会
/// 在「人工待办」处提前终止。改错的话症状是「后面的来源不再被尝试」且报告看不出异常。</para>
/// </summary>
public class SrcManualSkillTests
{
    [Fact]
    public async Task T1_正常_命中为false且回显提示()
    {
        var result = await SrcManualSkill.ExecuteAsync("company_name", "请填写企业名称");

        Assert.True(result.Success);
        Assert.False((bool)result.Outputs["hit"]);
        Assert.Equal("company_name", result.Outputs["anchor_code"]);

        var todo = Assert.IsType<FillTodo>(result.Outputs["todo"]);
        Assert.Equal("company_name", todo.AnchorCode);
        Assert.Equal("请填写企业名称", todo.Hint);
        Assert.Equal("src_manual", todo.Source);
    }

    [Fact]
    public async Task T2_锚点为空_失败()
    {
        var result = await SrcManualSkill.ExecuteAsync("");

        Assert.False(result.Success);
        Assert.Contains("anchor_code 不能为空", result.Error);
    }

    [Fact]
    public async Task T2b_锚点全空白_失败()
    {
        var result = await SrcManualSkill.ExecuteAsync("   ");

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task T3_提示为空_用默认提示(string hint)
    {
        var result = await SrcManualSkill.ExecuteAsync("company_name", hint);

        var todo = Assert.IsType<FillTodo>(result.Outputs["todo"]);
        Assert.Equal("请人工填写", todo.Hint);
    }

    [Fact]
    public async Task 不产出value键_让组合器继续往下找()
    {
        var result = await SrcManualSkill.ExecuteAsync("company_name", "hint");

        // ★ 这是「src_manual 不产值」的机器可验证表达
        Assert.False(result.Outputs.ContainsKey("value"));
    }
}
