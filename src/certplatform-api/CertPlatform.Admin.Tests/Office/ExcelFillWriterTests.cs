using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Excel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// Excel 写入器的 spike 测试 —— 覆盖 26 号 §3.4.6 标注的 Excel 侧三个坑：
/// ① <c>SetCellValue(string)</c> 把数字写成文本；② 数字/日期缺 <c>DataFormat</c>；
/// ③ <c>ShiftRows</c> 把区外内容一起下移。
/// </summary>
public class ExcelFillWriterTests
{
    private static OfficeFillRequest Request(byte[] template, params (string Key, FillValue Value)[] values)
    {
        var req = new OfficeFillRequest { Template = template };
        foreach (var (k, v) in values)
        {
            v.AnchorCode = k;
            req.Values[k] = v;
        }
        return req;
    }

    private static byte[] BuildXlsx(Action<XSSFWorkbook, ISheet> build)
    {
        using var wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("Sheet1");
        build(wb, sheet);
        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: false);
        return ms.ToArray();
    }

    private static XSSFWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes, writable: false));

    // ─────────────────────────── 文本替换 ───────────────────────────

    [Fact]
    public void Fill_TextAnchor_ReplacesInPlace()
    {
        var template = BuildXlsx((_, sheet) =>
        {
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("企业名称");
            row.CreateCell(1).SetCellValue("{{company_name}}");
            sheet.CreateRow(1).CreateCell(1).SetCellValue("前缀-{{company_name}}-后缀");
        });

        var result = new ExcelFillWriter().Fill(Request(template,
            ("company_name", new FillValue { Kind = FillValueKind.Text, Text = "河北雄安尚龙认证有限公司" })));

        var wb = Open(result.Output);
        var s = wb.GetSheetAt(0);
        Assert.Equal("河北雄安尚龙认证有限公司", s.GetRow(0).GetCell(1).StringCellValue);
        Assert.Equal("前缀-河北雄安尚龙认证有限公司-后缀", s.GetRow(1).GetCell(1).StringCellValue);

        Assert.Equal(2, result.Report.Resolved);
        Assert.True(result.Report.Verified);
    }

    // ─────────────────────── ★ 数字：必须是数值类型 ───────────────────────

    [Fact]
    public void Fill_NumberAnchor_WritesNumericCellNotText()
    {
        var template = BuildXlsx((_, sheet) => sheet.CreateRow(0).CreateCell(0).SetCellValue("{{qty}}"));

        var result = new ExcelFillWriter().Fill(Request(template, ("qty", new FillValue
        {
            Kind = FillValueKind.Number, Number = 1234.5, NumberFormat = "#,##0.00",
        })));

        var cell = Open(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);

        // ★ 核心断言：是数字单元格（能求和/排序），不是左对齐的文本
        Assert.Equal(CellType.Numeric, cell.CellType);
        Assert.Equal(1234.5, cell.NumericCellValue, 6);
        Assert.Equal("#,##0.00", cell.CellStyle.GetDataFormatString());
    }

    [Fact]
    public void Fill_DateAnchor_WithoutFormat_FallsBackToDatePattern()
    {
        var template = BuildXlsx((_, sheet) => sheet.CreateRow(0).CreateCell(0).SetCellValue("{{issue_date}}"));

        var result = new ExcelFillWriter().Fill(Request(template, ("issue_date", new FillValue
        {
            Kind = FillValueKind.Date, Date = new DateTime(2026, 10, 2),
        })));

        var cell = Open(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);

        Assert.Equal(CellType.Numeric, cell.CellType);
        // ★ 类型驱动兜底：不给格式也必须是日期格式，否则显示成 46235（语义丢失）
        Assert.True(DateUtil.IsCellDateFormatted(cell));
        Assert.Equal(new DateTime(2026, 10, 2), cell.DateCellValue);
    }

    [Fact]
    public void Fill_DateAnchor_KeepsExistingDateFormat()
    {
        var template = BuildXlsx((wb, sheet) =>
        {
            var cell = sheet.CreateRow(0).CreateCell(0);
            cell.SetCellValue("{{d}}");
            var style = wb.CreateCellStyle();
            style.DataFormat = wb.CreateDataFormat().GetFormat("yyyy年m月d日");
            cell.CellStyle = style;
        });

        var result = new ExcelFillWriter().Fill(Request(template, ("d", new FillValue
        {
            Kind = FillValueKind.Date, Date = new DateTime(2026, 1, 2),
        })));

        var cell = Open(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal("yyyy年m月d日", cell.CellStyle.GetDataFormatString());
    }

    // ─────────────────────── ★ 样式保留 ───────────────────────

    [Fact]
    public void Fill_TextAnchor_PreservesCellStyle()
    {
        var template = BuildXlsx((wb, sheet) =>
        {
            var cell = sheet.CreateRow(0).CreateCell(0);
            cell.SetCellValue("{{v}}");

            var style = wb.CreateCellStyle();
            style.BorderTop = BorderStyle.Thick;
            style.BorderBottom = BorderStyle.Thin;
            style.FillForegroundColor = IndexedColors.Yellow.Index;
            style.FillPattern = FillPattern.SolidForeground;
            cell.CellStyle = style;
        });

        var result = new ExcelFillWriter().Fill(
            Request(template, ("v", new FillValue { Text = "新值" })));

        var cell = Open(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal("新值", cell.StringCellValue);
        // ★ 只改值不碰 CellStyle ⇒ 边框/底纹全在（否则「填完值表格框线没了」）
        Assert.Equal(BorderStyle.Thick, cell.CellStyle.BorderTop);
        Assert.Equal(BorderStyle.Thin, cell.CellStyle.BorderBottom);
        Assert.Equal(FillPattern.SolidForeground, cell.CellStyle.FillPattern);
    }

    [Fact]
    public void Fill_NumberFormat_ReusesStyleAcrossCells()
    {
        var template = BuildXlsx((_, sheet) =>
        {
            var row = sheet.CreateRow(0);
            for (var c = 0; c < 5; c++) row.CreateCell(c).SetCellValue("{{n}}");
        });

        var result = new ExcelFillWriter().Fill(Request(template, ("n", new FillValue
        {
            Kind = FillValueKind.Number, Number = 1, NumberFormat = "0.00",
        })));

        var wb = Open(result.Output);
        var sheet = wb.GetSheetAt(0);
        // ★ 样式缓存：同格式多格共用一个样式（避免撞 Excel 64k 样式上限）
        var idx = sheet.GetRow(0).GetCell(0).CellStyle.Index;
        for (var c = 1; c < 5; c++)
            Assert.Equal(idx, sheet.GetRow(0).GetCell(c).CellStyle.Index);
    }

    // ─────────────────────── 未命中 / 自验收 ───────────────────────

    [Fact]
    public void Fill_UnresolvedToken_IsBlankedByDefault()
    {
        // ★ 2026-10-02 用户规格：「针对填写或替换，如果没有值则自动将填写内容赋值为空」
        var template = BuildXlsx((_, sheet) =>
        {
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("{{known}}");
            row.CreateCell(1).SetCellValue("{{unknown}}");
        });

        var result = new ExcelFillWriter().Fill(
            Request(template, ("known", new FillValue { Text = "1" })));

        var sheet = Open(result.Output).GetSheetAt(0);
        Assert.Equal("1", sheet.GetRow(0).GetCell(0).StringCellValue);
        Assert.Equal(string.Empty, sheet.GetRow(0).GetCell(1).StringCellValue);

        Assert.Equal(1, result.Report.Resolved);
        Assert.Equal(1, result.Report.Pending);

        // ★ 置空后无残留锚点 ⇒ 自验收通过；但 Pendings 仍如实记录
        Assert.Empty(result.Report.LeftoverTokens);
        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void Fill_UnresolvedToken_KeptWhenExplicitlyRequested()
    {
        var template = BuildXlsx((_, sheet) =>
        {
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("{{known}}");
            row.CreateCell(1).SetCellValue("{{unknown}}");
        });

        var req = Request(template, ("known", new FillValue { Text = "1" }));
        req.KeepUnresolvedAsIs = true;   // 模板调试用

        var result = new ExcelFillWriter().Fill(req);

        var sheet = Open(result.Output).GetSheetAt(0);
        Assert.Equal("{{unknown}}", sheet.GetRow(0).GetCell(1).StringCellValue);

        Assert.Contains("{{unknown}}", result.Report.LeftoverTokens);
        Assert.False(result.Report.Verified);
    }

    // ─────────────────────── ★ 锚点内 format（{{key:format}}）───────────────────────

    [Fact]
    public void Fill_AnchorFormat_IsAppliedToNumberCell()
    {
        // ★ 2026-10-02 用户规格：「excel 是遍历 {{}} 特定的数组，加上 {{}} 中的 format
        //   和对应的值进行填写或替换」⇒ 格式由**模板作者写在锚点里**，程序只搬运。
        var template = BuildXlsx((_, sheet) =>
        {
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("{{amount:#,##0.00}}");
        });

        var result = new ExcelFillWriter().Fill(
            Request(template, ("amount", new FillValue { Kind = FillValueKind.Number, Number = 1234567.5 })));

        var cell = Open(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal(CellType.Numeric, cell.CellType);
        Assert.Equal(1234567.5, cell.NumericCellValue, 3);
        Assert.Equal("#,##0.00", cell.CellStyle.GetDataFormatString());

        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void Fill_AnchorFormat_DoesNotOverrideValueDictionaryFormat()
    {
        // 层 2 显式给了 NumberFormat ⇒ 以层 2 为准（它更了解业务口径）
        var template = BuildXlsx((_, sheet) =>
        {
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("{{amount:0.0}}");
        });

        var result = new ExcelFillWriter().Fill(
            Request(template, ("amount", new FillValue
            {
                Kind = FillValueKind.Number, Number = 12.34, NumberFormat = "0.000",
            })));

        var cell = Open(result.Output).GetSheetAt(0).GetRow(0).GetCell(0);
        Assert.Equal("0.000", cell.CellStyle.GetDataFormatString());
    }

    // ─────────────────────── ★ 区域填充（起始行列 + 二维数据）───────────────────────

    [Fact]
    public void RegionFill_WritesTwoDimensionalDataFromStartCell()
    {
        // ★ 2026-10-02 用户规格：「从 excel 是从哪一行，那一列开始，采用 json 结构进行填充」
        var template = BuildXlsx((_, sheet) =>
        {
            for (var r = 0; r < 5; r++)
                sheet.CreateRow(r).CreateCell(0).SetCellValue($"占位{r}");
        });

        var req = Request(template);
        req.Regions.Add(new OfficeFillRegion
        {
            Kind = OfficeRegionKind.ExcelRange,
            StartRow = 1,      // 0-based：第 2 行
            StartCol = 1,      // 0-based：B 列
            Rows = new List<List<FillValue?>>
            {
                new() { new FillValue { Text = "甲" }, new FillValue { Kind = FillValueKind.Number, Number = 1 } },
                new() { new FillValue { Text = "乙" }, null },   // null ⇒ 写空
            },
        });

        var result = new ExcelFillWriter().Fill(req);

        var sheet = Open(result.Output).GetSheetAt(0);
        Assert.Equal("甲", sheet.GetRow(1).GetCell(1).StringCellValue);
        Assert.Equal(1, sheet.GetRow(1).GetCell(2).NumericCellValue, 3);
        Assert.Equal("乙", sheet.GetRow(2).GetCell(1).StringCellValue);
        Assert.Equal(string.Empty, sheet.GetRow(2).GetCell(2).StringCellValue);

        // ★ 区域填充单独统计，⛔ 不计入 Completion（数据有几行就是几行，完成度无意义）
        Assert.Single(result.Report.Regions);
        Assert.True(result.Report.Regions[0].Matched);
        Assert.Equal(2, result.Report.Regions[0].RowCount);
        Assert.Equal(0, result.Report.Total);
    }

    [Fact]
    public void RegionFill_ReportsMissingSheet()
    {
        var template = BuildXlsx((_, sheet) => sheet.CreateRow(0).CreateCell(0).SetCellValue("x"));

        var req = Request(template);
        req.Regions.Add(new OfficeFillRegion
        {
            Kind = OfficeRegionKind.ExcelRange,
            SheetName = "不存在的表",
            Rows = new List<List<FillValue?>> { new() { new FillValue { Text = "1" } } },
        });

        var result = new ExcelFillWriter().Fill(req);

        Assert.Single(result.Report.Regions);
        Assert.False(result.Report.Regions[0].Matched);
        Assert.Contains("找不到工作表", result.Report.Regions[0].Message);
    }

    // ─────────────────────── 行插入（ShiftRows 副作用）───────────────────────

    [Fact]
    public void ShiftDown_MovesEverythingBelowIncludingOutOfRegionContent()
    {
        var template = BuildXlsx((_, sheet) =>
        {
            for (var r = 0; r < 4; r++)
                sheet.CreateRow(r).CreateCell(0).SetCellValue($"R{r}");
        });

        var wb = Open(template);
        var s = wb.GetSheetAt(0);

        // 在 R1 处下移 1 行腾位
        ExcelRowInserter.ShiftDown(s, startRow: 1, count: 1);

        Assert.Null(s.GetRow(1));
        Assert.Equal("R1", s.GetRow(2).GetCell(0).StringCellValue);
        // ★ 这就是「区外内容会下移」—— 模板设计时必须预留，⛔ 不能靠删内容规避
        Assert.Equal("R2", s.GetRow(3).GetCell(0).StringCellValue);
        Assert.Equal("R3", s.GetRow(4).GetCell(0).StringCellValue);
    }
}
