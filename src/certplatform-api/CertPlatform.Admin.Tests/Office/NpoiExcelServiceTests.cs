using CertPlatform.Shared.Office;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// <see cref="NpoiExcelService"/> 单测 —— 补齐 YZH.Core 里 <c>NullExcelService</c> 三方法全 throw 的空洞。
/// <para>重点：<b>表头契约</b>（导出/模板/导入三者必须一致），以及「映射字段写错要抛而不是静默少一列」。</para>
/// </summary>
public class NpoiExcelServiceTests
{
    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;
        public int Qty { get; set; }
        public DateTime IssueDate { get; set; }
        public bool Enabled { get; set; }
    }

    private static readonly NpoiExcelService Service = new();

    [Fact]
    public void Export_Then_Import_RoundTrips()
    {
        var source = new List<Row>
        {
            new() { Name = "甲", Qty = 10, IssueDate = new DateTime(2026, 1, 2), Enabled = true },
            new() { Name = "乙", Qty = 20, IssueDate = new DateTime(2026, 3, 4), Enabled = false },
        };

        var bytes = Service.ExportToExcel(source, columnMapping: null, sheetName: "数据");
        var back = Service.ImportFromExcel<Row>(bytes);

        Assert.Equal(2, back.Count);
        Assert.Equal("甲", back[0].Name);
        Assert.Equal(10, back[0].Qty);
        Assert.Equal(new DateTime(2026, 1, 2), back[0].IssueDate);
        Assert.True(back[0].Enabled);
        Assert.Equal("乙", back[1].Name);
        Assert.False(back[1].Enabled);
    }

    [Fact]
    public void Export_WithMapping_UsesDesNameAsHeader()
    {
        var bytes = Service.ExportToExcel(
            new List<Row> { new() { Name = "甲", Qty = 1 } },
            new Dictionary<string, string> { ["Name"] = "名称", ["Qty"] = "数量" });

        var wb = new XSSFWorkbook(new MemoryStream(bytes));
        var header = wb.GetSheetAt(0).GetRow(0);
        Assert.Equal("名称", header.GetCell(0).StringCellValue);
        Assert.Equal("数量", header.GetCell(1).StringCellValue);
        // 只导出映射内的字段
        Assert.Equal(2, header.LastCellNum);
    }

    [Fact]
    public void Export_WithUnknownMappedField_Throws()
    {
        // ★ 静默跳过会产出「少一列」的文件（比报错难查得多）⇒ 必须抛
        var ex = Assert.Throws<ArgumentException>(() => Service.ExportToExcel(
            new List<Row>(), new Dictionary<string, string> { ["NotAField"] = "不存在" }));

        Assert.Contains("NotAField", ex.Message);
    }

    [Fact]
    public void GenerateImportTemplate_HeadersArePropertyNames_AndImportable()
    {
        var template = Service.GenerateImportTemplate<Row>();

        var wb = new XSSFWorkbook(new MemoryStream(template));
        var header = wb.GetSheetAt(0).GetRow(0);
        var titles = new List<string>();
        for (var c = header.FirstCellNum; c < header.LastCellNum; c++)
            titles.Add(header.GetCell(c).StringCellValue);

        Assert.Contains("Name", titles);
        Assert.Contains("Qty", titles);

        // 往模板里填一行再导回 ⇒ 模板必须「可导」
        var sheet = wb.GetSheetAt(0);
        var data = sheet.CreateRow(1);
        data.CreateCell(titles.IndexOf("Name")).SetCellValue("丙");
        data.CreateCell(titles.IndexOf("Qty")).SetCellValue(30);
        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: false);

        var back = Service.ImportFromExcel<Row>(ms.ToArray());
        Assert.Single(back);
        Assert.Equal("丙", back[0].Name);
        Assert.Equal(30, back[0].Qty);
    }

    [Fact]
    public void Import_SkipsGhostRowsAndBlankCells()
    {
        using var wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("Sheet1");
        var header = sheet.CreateRow(0);
        header.CreateCell(0).SetCellValue("Name");
        header.CreateCell(1).SetCellValue("Qty");
        sheet.CreateRow(1).CreateCell(0).SetCellValue("甲");
        sheet.CreateRow(2);                                   // 幽灵行：格式在、无内容
        sheet.CreateRow(3).CreateCell(1).SetCellValue(99);    // 缺 Name，但有 Qty ⇒ 保留
        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: false);

        var back = Service.ImportFromExcel<Row>(ms.ToArray());

        Assert.Equal(2, back.Count);
        Assert.Equal("甲", back[0].Name);
        Assert.Equal(99, back[1].Qty);
    }

    [Fact]
    public void Import_ChineseBoolean_IsAccepted()
    {
        using var wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("Sheet1");
        var header = sheet.CreateRow(0);
        header.CreateCell(0).SetCellValue("Name");
        header.CreateCell(1).SetCellValue("Enabled");
        var row = sheet.CreateRow(1);
        row.CreateCell(0).SetCellValue("甲");
        row.CreateCell(1).SetCellValue("是");
        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: false);

        var back = Service.ImportFromExcel<Row>(ms.ToArray());

        Assert.Single(back);
        Assert.True(back[0].Enabled);
    }

    [Fact]
    public void Export_DateColumn_HasDateFormat()
    {
        var bytes = Service.ExportToExcel(new List<Row> { new() { IssueDate = new DateTime(2026, 5, 6) } });
        var cell = new XSSFWorkbook(new MemoryStream(bytes)).GetSheetAt(0).GetRow(1).GetCell(2);

        Assert.Equal(CellType.Numeric, cell.CellType);
        // ★ 不套日期格式 ⇒ 显示成 46143（序列号）
        Assert.True(DateUtil.IsCellDateFormatted(cell));
    }
}
