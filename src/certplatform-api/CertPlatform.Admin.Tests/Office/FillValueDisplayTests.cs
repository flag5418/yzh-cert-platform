using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// <see cref="FillValue.ToDisplayText"/> 的测试（2026-10-05 新增）。
///
/// <para><b>为什么要钉</b>：这个方法是「值 → 落到文件里的文本」的<b>唯一出口</b>，
/// 三处都吃它 —— Word 单元格（<c>WordCellWriter.Write</c>）、Word 表格区域、
/// Excel 的「格内片段替换」。它错一处，<b>三处同时错</b>。</para>
///
/// <para><b>★ 本轮修的是什么</b>：原实现的 <c>Number</c> 分支是
/// <c>Number?.ToString(InvariantCulture)</c> —— 完全忽略 <c>NumberFormat</c>。
/// 后果：<b>Word 表格里的金额永远拿不到千分位</b>（<c>1,234.50</c> 落成 <c>1234.5</c>）。
/// Excel 的正规路径不受影响（它走 <c>SetCellValue(double)</c> + <c>DataFormat</c>），
/// 所以这个缺陷<b>只在 Word 里现形</b>，而 Word 恰恰没有 cell type 可依赖。</para>
/// </summary>
public class FillValueDisplayTests
{
    [Fact]
    public void ToDisplayText_Number_UsesNumberFormat()
    {
        var v = new FillValue
        {
            Kind = FillValueKind.Number,
            Number = 1234.5,
            NumberFormat = "#,##0.00",
        };

        Assert.Equal("1,234.50", v.ToDisplayText());
    }

    [Fact]
    public void ToDisplayText_Number_WithoutFormat_IsInvariantPlain()
    {
        var v = new FillValue { Kind = FillValueKind.Number, Number = 1234.5 };

        // ⛔ 不给格式 ⇒ 不做任何推断（「显示成 1,234 还是 1234.00」是层 2 的判断）
        Assert.Equal("1234.5", v.ToDisplayText());
    }

    [Fact]
    public void ToDisplayText_Number_WeirdFormat_NeverThrows()
    {
        // ★ 一个格式串不该让整份文档的填充炸掉。
        //   NumberFormat 在 Excel 侧被当 Excel 格式串用，可能含 [$-409] 这类 .NET 不认的写法。
        var v = new FillValue
        {
            Kind = FillValueKind.Number,
            Number = 1234.5,
            NumberFormat = "[$-409]0.00",
        };

        var ex = Record.Exception(() => v.ToDisplayText());

        Assert.Null(ex);
    }

    [Fact]
    public void ToDisplayText_Number_NullValue_IsEmpty()
    {
        var v = new FillValue { Kind = FillValueKind.Number, NumberFormat = "#,##0.00" };

        Assert.Equal(string.Empty, v.ToDisplayText());
    }

    [Fact]
    public void ToDisplayText_Date_IsIsoFormatted()
    {
        var v = new FillValue { Kind = FillValueKind.Date, Date = new DateTime(2026, 10, 5) };

        Assert.Equal("2026-10-05", v.ToDisplayText());
    }

    [Fact]
    public void ToDisplayText_Bool_IsChinese()
    {
        Assert.Equal("是", new FillValue { Kind = FillValueKind.Bool, Bool = true }.ToDisplayText());
        Assert.Equal("否", new FillValue { Kind = FillValueKind.Bool, Bool = false }.ToDisplayText());
    }

    [Fact]
    public void ToDisplayText_Text_ReturnsText()
    {
        var v = new FillValue { Kind = FillValueKind.Text, Text = "映智汇认证" };

        Assert.Equal("映智汇认证", v.ToDisplayText());
    }

    [Fact]
    public void ToDisplayText_Field_ReturnsDisplayTextNotInstruction()
    {
        // ★ Field 的 Text 是「域的显示文本」，⛔ 不是 FieldInstruction（那是给 Word 的指令）
        var v = new FillValue
        {
            Kind = FillValueKind.Field,
            Text = "3",
            FieldInstruction = "PAGE",
        };

        Assert.Equal("3", v.ToDisplayText());
    }
}
