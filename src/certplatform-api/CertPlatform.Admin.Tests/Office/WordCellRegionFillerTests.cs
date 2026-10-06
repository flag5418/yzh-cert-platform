using System.Text;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Word;
using NPOI.XWPF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// Word <b>坐标定位</b>单元格填充（<see cref="OfficeRegionKind.WordCell"/>）的测试 ——
/// 覆盖 2026-10-05 新增的两条能力：
///
/// <list type="number">
/// <item><b>按「表序号 + 行 + 列」写格</b> —— 对应房产测绘参考实现
/// <c>table.SetText(0, 2, projectinfo.Id)</c> 的场景：模板里<b>什么标记都没有</b>，
/// 位置固定，靠人工指定坐标。这类位置<b>扫描器扫不出来</b>。</item>
/// <item><b>越界必须被报告</b> —— 这是「试填验证」第二条判据
/// （「我们自己填写的信息，<b>位置是否正确</b>」）的前提。
/// ⛔ 过去这类错误全是<b>静默</b>的（列超界直接丢弃、行超界克隆补行），
/// 用户看到「填成功了」，值却落在别处。</item>
/// </list>
///
/// <para><b>★ 最贵的两条断言</b>：<c>RowOutOfRange</c> 与 <c>ColOutOfRange</c> ——
/// 它们不只断言「报了 Warning」，还断言<b>表结构没被改动</b>（没克隆行、没建新列）。
/// 只报 Warning 但偷偷克隆了行，用户照样发现不了。</para>
/// </summary>
public class WordCellRegionFillerTests
{
    // ─────────────────────────── 基础设施 ───────────────────────────

    private static byte[] BuildDocx(Action<XWPFDocument> build)
    {
        using var doc = new XWPFDocument();
        build(doc);
        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    private static XWPFDocument Open(byte[] bytes) => new(new MemoryStream(bytes, writable: false));

    /// <summary>
    /// 往表格单元格写文本。
    /// <para>⛔ 不要用 <c>cell.SetText()</c> 造测试数据：它只改 <c>CT_Tc</c>、不刷新段落缓存
    /// ⇒ 同一会话内读回空串（与 <c>WordFillWriterTests.PutCell</c> 同因）。</para>
    /// </summary>
    private static void PutCell(XWPFTable table, int row, int col, string text)
    {
        var cell = table.GetRow(row).GetCell(col);
        if (cell.Paragraphs.Count == 0) cell.AddParagraph();

        var p = cell.Paragraphs[0];
        if (p.Runs.Count == 0) p.CreateRun();
        p.Runs[0].SetText(text);
    }

    private static string CellText(XWPFTable table, int row, int col)
    {
        var sb = new StringBuilder();
        foreach (var p in table.GetRow(row).GetCell(col).Paragraphs)
            foreach (var r in p.Runs)
                sb.Append(r.GetText(0));
        return sb.ToString();
    }

    /// <summary>坐标区域：第 <paramref name="tableIndex"/> 个表，从 <c>(row, col)</c> 起铺二维数据</summary>
    private static OfficeFillRegion CellRegion(
        int tableIndex, int row, int col, params List<FillValue?>[] rows) => new()
    {
        Kind = OfficeRegionKind.WordCell,
        TableIndex = tableIndex,
        StartRow = row,
        StartCol = col,
        Rows = rows.ToList(),
    };

    /// <summary>一行文本值（<c>null</c> 元素 = 该格写空）</summary>
    private static List<FillValue?> Row(params string?[] values)
        => values.Select(v => v is null
            ? (FillValue?)null
            : new FillValue { Kind = FillValueKind.Text, Text = v }).ToList();

    private static OfficeFillResult Fill(byte[] template, params OfficeFillRegion[] regions)
    {
        var req = new OfficeFillRequest { Template = template };
        foreach (var r in regions) req.Regions.Add(r);
        return new WordFillWriter().Fill(req);
    }

    // ─────────────────── ① 坐标写入（用户参考实现的场景）───────────────────

    [Fact]
    public void WordCell_WritesToSpecifiedCoordinates()
    {
        // ★ 2026-10-05 用户场景：格子里没有标记，位置靠人工指定
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(2, 3);
            PutCell(table, 0, 0, "项目代码");
            PutCell(table, 0, 2, "");          // ← 目标格（空）
            PutCell(table, 1, 0, "测量单位");
        });

        var result = Fill(template, CellRegion(0, 0, 2, Row("XM-2026-001")));

        var hit = Assert.Single(result.Report.Regions);
        Assert.True(hit.Matched);
        Assert.Empty(hit.Warnings);

        using var doc = Open(result.Output);
        Assert.Equal("XM-2026-001", CellText(doc.Tables[0], 0, 2));
        // ⛔ 不能碰到邻格
        Assert.Equal("项目代码", CellText(doc.Tables[0], 0, 0));
    }

    [Fact]
    public void WordCell_MultipleRows_AllWritten()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(3, 2);
            PutCell(table, 0, 1, "");
            PutCell(table, 1, 1, "");
        });

        var result = Fill(template, CellRegion(0, 0, 1, Row("r1"), Row("r2")));

        using var doc = Open(result.Output);
        Assert.Equal("r1", CellText(doc.Tables[0], 0, 1));
        Assert.Equal("r2", CellText(doc.Tables[0], 1, 1));
        Assert.Equal(2, result.Report.Regions[0].RowCount);
    }

    [Fact]
    public void WordCell_SecondTable_TargetsTheRightTable()
    {
        var template = BuildDocx(doc =>
        {
            doc.CreateTable(1, 1);              // 表格[0]
            var second = doc.CreateTable(1, 1); // 表格[1]
            PutCell(second, 0, 0, "");
        });

        var result = Fill(template, CellRegion(1, 0, 0, Row("second-table")));

        using var doc = Open(result.Output);
        Assert.Equal("second-table", CellText(doc.Tables[1], 0, 0));
    }

    [Fact]
    public void WordCell_NullValue_WritesEmptyAndIsNotAWarning()
    {
        // ★ 值为 null = 用户明确要清空这一格 ⇒ 算成功，⛔ 不算 Warning
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(1, 1);
            PutCell(table, 0, 0, "旧值");
        });

        var result = Fill(template, CellRegion(0, 0, 0, Row((string?)null)));

        var hit = Assert.Single(result.Report.Regions);
        Assert.Empty(hit.Warnings);

        using var doc = Open(result.Output);
        Assert.Equal(string.Empty, CellText(doc.Tables[0], 0, 0));
    }

    // ─────────────────────────── ② 越界必须被报告 ───────────────────────────

    [Fact]
    public void WordCell_TableIndexOutOfRange_IsReportedNotThrown()
    {
        var template = BuildDocx(doc => doc.CreateTable(1, 2));

        var result = Fill(template, CellRegion(5, 0, 0, Row("x")));

        var hit = Assert.Single(result.Report.Regions);
        Assert.False(hit.Matched);
        Assert.Contains("第 5 个表格", hit.Message);
        Assert.Equal(0, hit.RowCount);
    }

    [Fact]
    public void WordCell_RowOutOfRange_IsReportedAndDoesNotCloneRow()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(2, 2);
            PutCell(table, 0, 0, "A");
        });

        // 第 10 行不存在 —— 坐标定位「⛔ 不克隆补行」，必须报
        var result = Fill(template, CellRegion(0, 9, 0, Row("x")));

        var hit = Assert.Single(result.Report.Regions);
        Assert.True(hit.Matched);
        Assert.Single(hit.Warnings);
        Assert.Contains("第 10 行不存在", hit.Warnings[0]);
        Assert.Equal(0, hit.RowCount);

        // ★ 最贵的一条：表结构没被改动（仍是 2 行）—— 只报 Warning 但偷偷克隆，用户照样发现不了
        using var doc = Open(result.Output);
        Assert.Equal(2, doc.Tables[0].Rows.Count);
    }

    [Fact]
    public void WordCell_ColOutOfRange_IsReportedAndDoesNotCreateCell()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(1, 2);
            PutCell(table, 0, 0, "A");
        });

        // 第 6 列不存在 —— ⛔ 不建新列（表格结构由模板控制）
        var result = Fill(template, CellRegion(0, 0, 5, Row("x")));

        var hit = Assert.Single(result.Report.Regions);
        Assert.Single(hit.Warnings);
        Assert.Contains("第 6 列不存在", hit.Warnings[0]);

        using var doc = Open(result.Output);
        Assert.Equal(2, doc.Tables[0].GetRow(0).GetTableCells().Count);
    }

    [Fact]
    public void WordCell_PartialRowOverflow_WritesWhatItCanAndReportsTheRest()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(1, 2);
            PutCell(table, 0, 0, "");
            PutCell(table, 0, 1, "");
        });

        // 从第 1 列起铺 3 个值 ⇒ 只有前 2 个落得下
        var result = Fill(template, CellRegion(0, 0, 0, Row("a", "b", "c")));

        var hit = Assert.Single(result.Report.Regions);
        Assert.Single(hit.Warnings);
        Assert.Contains("第 3 列不存在", hit.Warnings[0]);

        using var doc = Open(result.Output);
        Assert.Equal("a", CellText(doc.Tables[0], 0, 0));
        Assert.Equal("b", CellText(doc.Tables[0], 0, 1));
    }

    // ──────────────── ③ 坐标优先于锚点（执行顺序）────────────────

    [Fact]
    public void WordCell_RunsBeforeAnchorReplace_SoCoordinateWinsOverToken()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(1, 1);
            PutCell(table, 0, 0, "{{ENT_NAME}}");
        });

        var req = new OfficeFillRequest { Template = template };
        req.Values["ENT_NAME"] = new FillValue { Kind = FillValueKind.Text, Text = "来自锚点" };
        req.Regions.Add(CellRegion(0, 0, 0, Row("来自坐标")));

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);

        // ★ 坐标是显式指定的 ⇒ 覆盖模板里恰好写着的标记（这正是用户的意图）
        Assert.Equal("来自坐标", CellText(doc.Tables[0], 0, 0));
    }

    // ──────────────── ④ 数值格式（与 ToDisplayText 修正联动）────────────────

    [Fact]
    public void WordCell_NumberValue_HonoursNumberFormat()
    {
        var template = BuildDocx(doc => doc.CreateTable(1, 1));

        var req = new OfficeFillRequest { Template = template };
        req.Regions.Add(new OfficeFillRegion
        {
            Kind = OfficeRegionKind.WordCell,
            TableIndex = 0,
            StartRow = 0,
            StartCol = 0,
            Rows = new List<List<FillValue?>>
            {
                new()
                {
                    new FillValue
                    {
                        Kind = FillValueKind.Number,
                        Number = 1234.5,
                        NumberFormat = "#,##0.00",
                    },
                },
            },
        });

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);

        // ★ 2026-10-05 修正：过去 Number 分支忽略 NumberFormat ⇒ 这里会落成 "1234.5"
        Assert.Equal("1,234.50", CellText(doc.Tables[0], 0, 0));
    }

    // ──────────────── ⑤ 标签区域：列溢出也要报（G1）────────────────

    [Fact]
    public void WordTableRegion_ColumnOverflow_IsReportedNotSilent()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(2, 2);
            PutCell(table, 0, 0, "{{table:items}}");
        });

        var req = new OfficeFillRequest { Template = template };
        req.Regions.Add(new OfficeFillRegion
        {
            Kind = OfficeRegionKind.WordTable,
            TableTag = "items",
            StartCol = 0,
            Rows = new List<List<FillValue?>>
            {
                Row("a", "b", "c"),   // ← 3 个值，模板只有 2 列
            },
        });

        var result = new WordFillWriter().Fill(req);

        var hit = Assert.Single(result.Report.Regions);
        Assert.True(hit.Matched);

        // ★ 2026-10-05 修正：过去这里是一句静默的 break，值凭空消失而报告毫无痕迹
        Assert.Single(hit.Warnings);
        Assert.Contains("第 3 列超出模板列数", hit.Warnings[0]);
        Assert.Contains("1 个值未写入", hit.Warnings[0]);
    }

    // ──────────────── ⑥ 描述串（报告可读性）────────────────

    [Fact]
    public void WordCell_Describe_UsesTableIndexAndA1Ref()
    {
        var region = CellRegion(2, 1, 3, Row("x"));

        // ★ 与「表格[table:items]」（标签定位）刻意区分开 ——
        //   报告里一眼能看出这条是「人工指定的坐标」还是「标签/遍历出来的位置」
        Assert.Equal("表格[2]!D2", region.Describe());
    }
}
