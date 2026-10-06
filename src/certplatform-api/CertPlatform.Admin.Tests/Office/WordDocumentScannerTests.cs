using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Word;
using NPOI.WP.UserModel;      // HeaderFooterType
using NPOI.XWPF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// 模板锚点扫描器的<b>位置描述</b>测试（2026-10-05 新增）。
///
/// <para><b>为什么要钉</b>：人工填坐标（<see cref="OfficeRegionKind.WordCell"/>）时，
/// 用户需要知道「这个锚点在第几个表第几行第几列」—— 光有一个 <c>table</c> 字眼没法下手，
/// 只能自己数。扫描器顺手把行列号算出来，用户就从「数」变成「确认」。</para>
///
/// <para><b>★ 最关键的一条</b>：位置串必须与<b>写入侧逐字一致</b>
/// （<c>WordFillWriter</c> 的「正文·第 N 段」、<c>WordTableRowInserter</c> 的「行 R 列 C」）——
/// 否则填充报告里的 <c>Hit.Location</c> 与模板锚点清单对不上，
/// 用户得在脑子里换算两套编号（这正是「试填验证」最不想要的东西）。</para>
/// </summary>
public class WordDocumentScannerTests
{
    // ─────────────────────────── 基础设施 ───────────────────────────

    private static XWPFDocument Doc(Action<XWPFDocument> build)
    {
        var doc = new XWPFDocument();
        build(doc);
        return doc;
    }

    private static byte[] BuildDocx(Action<XWPFDocument> build)
    {
        using var doc = new XWPFDocument();
        build(doc);
        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    private static XWPFDocument Open(byte[] bytes) => new(new MemoryStream(bytes, writable: false));

    /// <summary>⛔ 不要用 <c>cell.SetText()</c> 造数据（只改 CT、不刷缓存）</summary>
    private static void PutCell(XWPFTable table, int row, int col, string text)
    {
        var cell = table.GetRow(row).GetCell(col);
        if (cell.Paragraphs.Count == 0) cell.AddParagraph();

        var p = cell.Paragraphs[0];
        if (p.Runs.Count == 0) p.CreateRun();
        p.Runs[0].SetText(text);
    }

    // ─────────────────────── ① 正文段落序号 ───────────────────────

    [Fact]
    public void ScanAnchors_BodyParagraph_LocationMatchesWriterConvention()
    {
        using var doc = Doc(d =>
        {
            d.CreateParagraph().CreateRun().SetText("第一段没有锚点");
            d.CreateParagraph().CreateRun().SetText("企业名称：{{ENT_NAME}}");
        });

        var anchor = Assert.Single(WordDocumentScanner.ScanAnchors(doc));

        // ★ 与 WordFillWriter 的 $"正文·第 {i + 1} 段" 逐字一致
        Assert.Equal($"正文·第 {doc.Paragraphs.Count} 段", anchor.Location);
        Assert.Equal("{{ENT_NAME}}", anchor.AnchorRef);
    }

    // ─────────────────────── ② 表格行列号 ───────────────────────

    [Fact]
    public void ScanAnchors_TableAnchor_LocationHasTableRowCol()
    {
        using var doc = Doc(d =>
        {
            var table = d.CreateTable(3, 2);
            PutCell(table, 2, 1, "{{ENT_NAME}}");   // 1-based：第 3 行第 2 列
        });

        var anchor = Assert.Single(WordDocumentScanner.ScanAnchors(doc));

        // ★ 与 WordTableRowInserter.FillTableRecursive 的 $"{prefix} 行 {ri + 1} 列 {ci + 1}" 逐字一致
        Assert.Equal("表格[0] 行 3 列 2", anchor.Location);
    }

    [Fact]
    public void ScanAnchors_SecondTable_LocationHasItsOwnIndex()
    {
        using var doc = Doc(d =>
        {
            d.CreateTable(1, 1);
            var second = d.CreateTable(1, 1);
            PutCell(second, 0, 0, "{{B}}");
        });

        var anchor = Assert.Single(WordDocumentScanner.ScanAnchors(doc));

        Assert.Equal("表格[1] 行 1 列 1", anchor.Location);
    }

    // ─────────────────────── ③ 页眉段落序号 ───────────────────────

    [Fact]
    public void ScanAnchors_HeaderAnchor_LocationHasHeaderLabel()
    {
        // ⚠️ 本次新建的页眉不在 doc.HeaderList 里 ⇒ 必须显式经 extraHeaderFooters 传入
        //   （与 WordFillWriter 的说明同因）
        using var doc = Doc(_ => { });
        var header = doc.CreateHeader(HeaderFooterType.DEFAULT);
        header.CreateParagraph().CreateRun().SetText("{{ENT_NAME}}");

        var anchor = Assert.Single(WordDocumentScanner.ScanAnchors(doc, new[] { header }));

        // ★ 标「附加」而不是编号：它的序号不在 HeaderList 里，
        //   强行编号会与主列表的 [0]/[1] 撞号，报告里反而更难看懂
        Assert.Equal("页眉[附加]·第 1 段", anchor.Location);
    }

    // ─────────── ④ 与写入侧口径一致性（本轮最贵的一条）───────────

    [Fact]
    public void ScanAnchors_AndFillReport_UseTheSameLocationConvention()
    {
        var bytes = BuildDocx(doc =>
        {
            doc.CreateParagraph().CreateRun().SetText("{{A}}");

            var table = doc.CreateTable(1, 1);
            PutCell(table, 0, 0, "{{B}}");
        });

        // ① 扫描侧（模板锚点清单）
        using var scanned = Open(bytes);
        var anchors = WordDocumentScanner.ScanAnchors(scanned);
        Assert.Equal(2, anchors.Count);

        // ② 写入侧（填充报告）
        var req = new OfficeFillRequest { Template = bytes };
        req.Values["A"] = new FillValue { Kind = FillValueKind.Text, Text = "a" };
        req.Values["B"] = new FillValue { Kind = FillValueKind.Text, Text = "b" };
        var report = new WordFillWriter().Fill(req).Report;

        var hitLocations = report.Hits.Select(h => h.Location).ToList();

        // ★ 两侧位置串必须能直接对上 —— 否则「试填验证」里
        //   「哪条规则对应模板里哪个锚点」要靠人脑换算两套编号
        foreach (var a in anchors)
            Assert.Contains(a.Location, hitLocations);
    }
}
