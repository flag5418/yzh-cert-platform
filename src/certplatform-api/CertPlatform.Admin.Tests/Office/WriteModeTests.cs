using System.Text;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Excel;
using CertPlatform.Shared.Office.Word;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
///     <b>写入方式（覆盖 / 填充）</b>的契约测试 —— <c>FillValue.WriteMode</c> 的两条分支。
///
///     <para><b>★ 为什么这一组必须有</b>：用户 2026-10-09 在「填写规则」页选了「覆盖 / 填充」，
///     但在此之前 <b>整个 <c>Shared/Office/</c> 里没有任何一处读 <c>WriteMode</c></b> ——
///     两个选项产出<b>完全相同</b>的文件。那是「UI 上有、程序里没有」的<b>假选项</b>：
///     用户配了、保存了、看不出任何异常，只有拿到文件才发现没生效（静默失效）。</para>
///
///     <para><b>规格原话</b>（用户 2026-10-09）：
///     「我们很多时候，一个单元格并不是一个字段，一般是<b>一句话，中间有 <c>{{}}</c></b>，
///     我们如果用<b>覆盖</b>，就将 cell 全部填充成新的内容了，只能替换当前单元格的 <c>{{}}</c>」。</para>
///
///     <list type="table">
///         <listheader><term>WriteMode</term><description>行为</description></listheader>
///         <item><term><c>overwrite</c>（覆盖）</term>
///               <description>整格 / 整段换成取值，其它文字<b>一并被替换</b></description></item>
///         <item><term><c>replace</c> / <c>null</c>（填充）</term>
///               <description>只替换 <c>{{token}}</c>，其余文字<b>原样保留</b></description></item>
///     </list>
/// </summary>
public class WriteModeTests
{
    // ════════════════════════════════════════════════════════════════
    //  Excel
    // ════════════════════════════════════════════════════════════════

    private static byte[] BuildXlsx(Action<ISheet> build)
    {
        using var wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("Sheet1");
        build(sheet);
        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: false);
        return ms.ToArray();
    }

    private static XSSFWorkbook OpenXlsx(byte[] bytes) => new(new MemoryStream(bytes, writable: false));

    private static OfficeFillRequest XlsxRequest(byte[] template, string key, string text, string? writeMode)
    {
        var req = new OfficeFillRequest { Template = template };
        req.Values[key] = new FillValue
        {
            AnchorCode = key, Kind = FillValueKind.Text, Text = text, WriteMode = writeMode,
        };
        return req;
    }

    /// <summary>一格 = 一句话，中间夹一个 <c>{{}}</c> —— 用户规格里的那个场景</summary>
    private const string SentenceCell = "本企业 {{company_name}} 成立于 2000 年";

    [Fact]
    public void Excel_填充_只换占位符_其余文字保留()
    {
        var template = BuildXlsx(s => s.CreateRow(0).CreateCell(0).SetCellValue(SentenceCell));

        var result = new ExcelFillWriter().Fill(
            XlsxRequest(template, "company_name", "某某科技有限公司", "replace"));

        var cell = OpenXlsx(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal("本企业 某某科技有限公司 成立于 2000 年", cell.StringCellValue);
        Assert.Single(result.Report.Hits);
    }

    [Fact]
    public void Excel_覆盖_整格换成取值_句子的其余部分被替换掉()
    {
        var template = BuildXlsx(s => s.CreateRow(0).CreateCell(0).SetCellValue(SentenceCell));

        var result = new ExcelFillWriter().Fill(
            XlsxRequest(template, "company_name", "某某科技有限公司", "overwrite"));

        var cell = OpenXlsx(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        // ★ 这就是「覆盖」与「填充」的**唯一可见差别**
        Assert.Equal("某某科技有限公司", cell.StringCellValue);
        Assert.Single(result.Report.Hits);
    }

    [Fact]
    public void Excel_WriteMode为null_按填充处理_保守默认不破坏原文()
    {
        var template = BuildXlsx(s => s.CreateRow(0).CreateCell(0).SetCellValue(SentenceCell));

        var result = new ExcelFillWriter().Fill(
            XlsxRequest(template, "company_name", "某某科技有限公司", writeMode: null));

        var cell = OpenXlsx(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        // ⛔ 不认识 / 未给的值一律走「不破坏原文」那条路
        Assert.Equal("本企业 某某科技有限公司 成立于 2000 年", cell.StringCellValue);
    }

    [Fact]
    public void Excel_WriteMode大小写与空白容错()
    {
        var template = BuildXlsx(s => s.CreateRow(0).CreateCell(0).SetCellValue(SentenceCell));

        var result = new ExcelFillWriter().Fill(
            XlsxRequest(template, "company_name", "某某", "  OVERWRITE  "));

        var cell = OpenXlsx(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal("某某", cell.StringCellValue);
    }

    [Fact]
    public void Excel_覆盖_保住数字类型而不是写成文本()
    {
        // 覆盖路径必须与「独占格」路径一样走 ExcelCellWriter（按 Kind 选重载）
        var template = BuildXlsx(s => s.CreateRow(0).CreateCell(0).SetCellValue("数量：{{qty}} 件"));

        var req = new OfficeFillRequest { Template = template };
        req.Values["qty"] = new FillValue
        {
            AnchorCode = "qty", Kind = FillValueKind.Number, Number = 1234.5,
            NumberFormat = "#,##0.00", WriteMode = "overwrite",
        };

        var cell = OpenXlsx(new ExcelFillWriter().Fill(req).Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal(CellType.Numeric, cell.CellType);
        Assert.Equal(1234.5, cell.NumericCellValue, 3);
    }

    [Fact]
    public void Excel_覆盖_多锚点格_退回填充并记一条待办()
    {
        var template = BuildXlsx(s =>
            s.CreateRow(0).CreateCell(0).SetCellValue("{{a}} 与 {{b}}"));

        var req = new OfficeFillRequest { Template = template };
        req.Values["a"] = new FillValue { AnchorCode = "a", Text = "甲", WriteMode = "overwrite" };
        req.Values["b"] = new FillValue { AnchorCode = "b", Text = "乙" };

        var result = new ExcelFillWriter().Fill(req);

        // ★ 语义模糊 ⇒ 不猜：仍按「填充」写，但**必须被看见**
        var cell = OpenXlsx(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal("甲 与 乙", cell.StringCellValue);

        var pending = Assert.Single(result.Report.Pendings);
        Assert.Contains("覆盖", pending.Reason);
        Assert.Contains("2 个锚点", pending.Reason);
    }

    // ════════════════════════════════════════════════════════════════
    //  Word
    // ════════════════════════════════════════════════════════════════

    private static byte[] BuildDocx(Action<XWPFDocument> build)
    {
        using var doc = new XWPFDocument();
        build(doc);
        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    private static XWPFDocument OpenDocx(byte[] bytes) => new(new MemoryStream(bytes, writable: false));

    /// <summary>段内所有 run 的文本拼接（绕开 NPOI 的 <c>paragraph.Text</c> 口径问题）</summary>
    private static string TextOf(XWPFParagraph p)
    {
        var sb = new StringBuilder();
        foreach (var r in p.Runs) sb.Append(r.GetText(0));
        return sb.ToString();
    }

    private static OfficeFillRequest DocxRequest(byte[] template, string key, string text, string? writeMode)
    {
        var req = new OfficeFillRequest { Template = template };
        req.Values[key] = new FillValue
        {
            AnchorCode = key, Kind = FillValueKind.Text, Text = text, WriteMode = writeMode,
        };
        return req;
    }

    [Fact]
    public void Word_填充_只换占位符()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText(SentenceCell));

        var result = new WordFillWriter().Fill(
            DocxRequest(template, "company_name", "某某科技有限公司", "replace"));

        var p = OpenDocx(result.Output).Paragraphs[0];
        Assert.Equal("本企业 某某科技有限公司 成立于 2000 年", TextOf(p));
    }

    [Fact]
    public void Word_覆盖_整段换成取值()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText(SentenceCell));

        var result = new WordFillWriter().Fill(
            DocxRequest(template, "company_name", "某某科技有限公司", "overwrite"));

        var p = OpenDocx(result.Output).Paragraphs[0];
        Assert.Equal("某某科技有限公司", TextOf(p));
    }

    [Fact]
    public void Word_覆盖_跨run时也能整段替换且不留残字()
    {
        // W9 场景：{{company_name}} 被拆成多个 run
        var template = BuildDocx(d =>
        {
            var p = d.CreateParagraph();
            p.CreateRun().SetText("本企业 {{comp");
            p.CreateRun().SetText("any_na");
            p.CreateRun().SetText("me}} 成立于 2000 年");
        });

        var result = new WordFillWriter().Fill(
            DocxRequest(template, "company_name", "某某科技有限公司", "overwrite"));

        var p = OpenDocx(result.Output).Paragraphs[0];
        Assert.Equal("某某科技有限公司", TextOf(p));
        // 其余 run 必须被清空 —— 否则会留下「成立于 2000 年」的碎片
        for (var i = 1; i < p.Runs.Count; i++)
            Assert.Equal(string.Empty, p.Runs[i].GetText(0) ?? string.Empty);
    }

    [Fact]
    public void Word_覆盖_多锚点段_退回填充并记一条待办()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText("{{a}} 与 {{b}}"));

        var req = new OfficeFillRequest { Template = template };
        req.Values["a"] = new FillValue { AnchorCode = "a", Text = "甲", WriteMode = "overwrite" };
        req.Values["b"] = new FillValue { AnchorCode = "b", Text = "乙" };

        var result = new WordFillWriter().Fill(req);

        Assert.Equal("甲 与 乙", TextOf(OpenDocx(result.Output).Paragraphs[0]));
        Assert.Contains("覆盖", Assert.Single(result.Report.Pendings).Reason);
    }

    [Fact]
    public void Word_WriteMode为null_按填充处理()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText(SentenceCell));

        var result = new WordFillWriter().Fill(
            DocxRequest(template, "company_name", "某某科技有限公司", writeMode: null));

        Assert.Equal("本企业 某某科技有限公司 成立于 2000 年",
            TextOf(OpenDocx(result.Output).Paragraphs[0]));
    }

    // ════════════════════════════════════════════════════════════════
    //  ★ IsOverwrite —— 唯一判据
    // ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("overwrite", true)]
    [InlineData("OVERWRITE", true)]
    [InlineData("  overwrite ", true)]
    [InlineData("replace", false)]
    [InlineData("append", false)]
    [InlineData("remove", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOverwrite_只认overwrite_其余一律填充(string? mode, bool expected)
    {
        Assert.Equal(expected, new FillValue { WriteMode = mode }.IsOverwrite());
    }
}
