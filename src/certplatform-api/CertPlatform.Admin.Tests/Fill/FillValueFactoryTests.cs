using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// <see cref="FillValueFactory"/> 的契约测试 —— 重点是「<b>声明了类型就必须按类型落笔，⛔ 不静默降级</b>」。
///
/// <para>为什么这条最重要：<c>SetCellValue</c> 重载传错<b>不报错但结果错</b>
/// （数字写成文本 ⇒ 不能求和/排序，且打开文件看不出异常）。
/// 工厂是 8 个 Skill 唯一的构造入口 ⇒ 它静默降级，8 个 Skill 全错。</para>
/// </summary>
public class FillValueFactoryTests
{
    // ─────────────────────────── text ───────────────────────────

    [Fact]
    public void text_原样落笔()
    {
        var (ok, value, error) = FillValueFactory.TryCreate("company_name", "某某有限公司", "text");

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(FillValueKind.Text, value!.Kind);
        Assert.Equal("某某有限公司", value.Text);
        Assert.Equal("company_name", value.AnchorCode);
    }

    [Fact]
    public void enum_按文本处理()
    {
        var (ok, value, _) = FillValueFactory.TryCreate("level", "一级", "enum");

        Assert.True(ok);
        Assert.Equal(FillValueKind.Text, value!.Kind);
        Assert.Equal("一级", value.Text);
    }

    [Fact]
    public void 未知类型_按文本处理()
    {
        var (ok, value, _) = FillValueFactory.TryCreate("x", "abc", "something_else");

        Assert.True(ok);
        Assert.Equal(FillValueKind.Text, value!.Kind);
    }

    // ─────────────────────────── number ───────────────────────────

    [Theory]
    [InlineData("1234", 1234d)]
    [InlineData("1234.5", 1234.5d)]
    [InlineData("1,234.5", 1234.5d)]
    [InlineData("-0.25", -0.25d)]
    [InlineData("1e3", 1000d)]
    public void number_合法数字_转成数值(string raw, double expected)
    {
        var (ok, value, error) = FillValueFactory.TryCreate("amount", raw, "number");

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(FillValueKind.Number, value!.Kind);
        Assert.Equal(expected, value.Number!.Value, 6);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("12abc")]
    [InlineData("壹佰")]
    public void number_非法数字_失败而非降级成文本(string raw)
    {
        var (ok, value, error) = FillValueFactory.TryCreate("amount", raw, "number");

        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("不是合法数字", error);
    }

    // ─────────────────────────── date ───────────────────────────

    [Theory]
    [InlineData("2026-10-03", 2026, 10, 3)]
    [InlineData("2026/10/3", 2026, 10, 3)]
    [InlineData("2026年10月3日", 2026, 10, 3)]
    [InlineData("2026年10月03日", 2026, 10, 3)]
    [InlineData("20261003", 2026, 10, 3)]
    public void date_常见格式_解析成功(string raw, int y, int m, int d)
    {
        var (ok, value, error) = FillValueFactory.TryCreate("doc_date", raw, "date");

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(FillValueKind.Date, value!.Kind);
        Assert.Equal(new DateTime(y, m, d), value.Date!.Value.Date);
    }

    [Fact]
    public void date_非法_失败()
    {
        var (ok, value, error) = FillValueFactory.TryCreate("doc_date", "不是日期", "date");

        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("不是合法日期", error);
    }

    [Fact]
    public void date_显示文本_默认yyyy_MM_dd()
    {
        var (_, value, _) = FillValueFactory.TryCreate("doc_date", "2026-10-03", "date");

        // ⚠️ 这条断言锁死「当前 Word 侧行为」。39 号 §16.7 改动 1 会让它改为读 NumberFormat
        //    ⇒ 届时本用例需同步更新（这正是 §16.7 提醒「实施前先跑一遍」的原因）。
        Assert.Equal("2026-10-03", value!.ToDisplayText());
    }

    // ─────────────────────────── bool ───────────────────────────

    [Theory]
    [InlineData("是", true)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("Y", true)]
    [InlineData("否", false)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("N", false)]
    public void bool_中英文写法都接受(string raw, bool expected)
    {
        var (ok, value, error) = FillValueFactory.TryCreate("flag", raw, "bool");

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(FillValueKind.Bool, value!.Kind);
        Assert.Equal(expected, value.Bool);
    }

    [Fact]
    public void bool_非法_失败()
    {
        var (ok, _, error) = FillValueFactory.TryCreate("flag", "也许", "bool");

        Assert.False(ok);
        Assert.Contains("不是合法布尔", error);
    }

    // ─────────────────────────── 空值 / 格式串 / 参数 ───────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空值_不算失败_但类型按声明给(string? raw)
    {
        var (ok, value, error) = FillValueFactory.TryCreate("amount", raw, "number");

        // ★ 「取到了值但值是空的」与「没取到值」是两回事 —— 后者由调用方 Fail 表达
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(FillValueKind.Number, value!.Kind);
        Assert.Null(value.Number);
    }

    [Fact]
    public void 格式串_原样透传且去空白()
    {
        var (_, value, _) = FillValueFactory.TryCreate("amount", "1234.5", "number", "  #,##0.00  ");

        Assert.Equal("#,##0.00", value!.NumberFormat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 格式串为空_落成null(string? fmt)
    {
        var (_, value, _) = FillValueFactory.TryCreate("amount", "1", "number", fmt);

        Assert.Null(value!.NumberFormat);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void 锚点为空_失败(string? anchor)
    {
        var (ok, value, error) = FillValueFactory.TryCreate(anchor!, "x", "text");

        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("anchor_code 不能为空", error);
    }

    [Fact]
    public void 类型缺省_按text()
    {
        var (ok, value, _) = FillValueFactory.TryCreate("x", "abc", null);

        Assert.True(ok);
        Assert.Equal(FillValueKind.Text, value!.Kind);
    }

    [Fact]
    public void 默认置信度_为1()
    {
        var (_, value, _) = FillValueFactory.TryCreate("x", "abc", "text");

        Assert.Equal(1.0, value!.Confidence);
    }
}
