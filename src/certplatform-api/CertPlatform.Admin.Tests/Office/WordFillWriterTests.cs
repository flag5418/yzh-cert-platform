using System.Text;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Word;
using NPOI.WP.UserModel;
using NPOI.XWPF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// Word 写入器的 spike 测试 —— 覆盖 26 号 §3.4.6 标注的**三个必现坑**：
/// ① <b>W9</b>：<c>{{Token}}</c> 被 Word 拆成多个 run；② 样式保留；③ 合并/格式在插行时丢失。
///
/// <para>这些用例的作用不是「覆盖率」，而是**把 NPOI 的真实行为钉死**——
/// 6 个写入 skill 都建立在这些原语之上，原语行为错 ⇒ 6 个 skill 全错且难查。</para>
/// </summary>
public class WordFillWriterTests
{
    // ─────────────────────────── 基础设施 ───────────────────────────

    /// <summary>构造请求（模板 + 值字典）</summary>
    private static OfficeFillRequest Request(byte[] template, params (string Key, string Value)[] values)
    {
        var req = new OfficeFillRequest { Template = template };
        foreach (var (k, v) in values)
            req.Values[k] = new FillValue { AnchorCode = k, Kind = FillValueKind.Text, Text = v };
        return req;
    }

    /// <summary>把段内所有 run 的首个 &lt;w:t&gt; 拼起来（绕开 NPOI 的 <c>paragraph.Text</c> 口径问题）</summary>
    private static string TextOf(XWPFParagraph p)
    {
        var sb = new StringBuilder();
        foreach (var r in p.Runs) sb.Append(r.GetText(0));
        return sb.ToString();
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

    /// <summary>
    /// 往表格单元格写文本。
    /// <para>⛔ <b>不要用 <c>cell.SetText()</c> 造测试数据</b>：它只改 <c>CT_Tc</c>、**不刷新
    /// <c>XWPFTableCell</c> 内部的段落缓存** ⇒ 同一会话内 <c>GetText()</c> 读回空串
    /// （实测确认；重新打开文档才读得到）。本仓 §二十 同类「静默不一致」。</para>
    /// </summary>
    private static void PutCell(XWPFTable table, int row, int col, string text)
    {
        var cell = table.GetRow(row).GetCell(col);
        if (cell.Paragraphs.Count == 0) cell.AddParagraph();

        var p = cell.Paragraphs[0];
        if (p.Runs.Count == 0) p.CreateRun();
        p.Runs[0].SetText(text);
    }

    // ─────────────────────────── ① 单 run ───────────────────────────

    [Fact]
    public void Fill_SingleRun_ReplacesToken()
    {
        var template = BuildDocx(doc =>
        {
            var p = doc.CreateParagraph();
            p.CreateRun().SetText("企业名称：{{company_name}}");
        });

        var result = new WordFillWriter().Fill(
            Request(template, ("company_name", "河北雄安尚龙认证有限公司")));

        using var doc = Open(result.Output);
        Assert.Equal("企业名称：河北雄安尚龙认证有限公司", TextOf(doc.Paragraphs[0]));

        Assert.Equal(1, result.Report.Resolved);
        Assert.Equal(0, result.Report.Pending);
        Assert.True(result.Report.Verified);
        Assert.False(result.Report.Hits[0].CrossRun);
    }

    // ─────────────────────── ★★ ② W9：token 跨 run ───────────────────────

    [Fact]
    public void Fill_TokenSplitAcrossRuns_StillReplaces()
    {
        // ★ 忠实复现 Word 的行为：一段文字被编辑历史拆成 5 个 run，锚点横跨中间 3 个
        var template = BuildDocx(doc =>
        {
            var p = doc.CreateParagraph();
            p.CreateRun().SetText("企业名称：");
            p.CreateRun().SetText("{{comp");
            p.CreateRun().SetText("any_");
            p.CreateRun().SetText("name}}");
            p.CreateRun().SetText("（盖章）");
        });

        var result = new WordFillWriter().Fill(
            Request(template, ("company_name", "河北雄安尚龙认证有限公司")));

        using var doc = Open(result.Output);
        Assert.Equal("企业名称：河北雄安尚龙认证有限公司（盖章）", TextOf(doc.Paragraphs[0]));

        Assert.Equal(1, result.Report.Resolved);
        Assert.True(result.Report.Verified);
        // ★ 这是 W9 的回归指标：命中必须被标记为「跨 run」
        Assert.True(result.Report.Hits[0].CrossRun);
        Assert.Equal(1, result.Report.CrossRunCount);
    }

    [Fact]
    public void Fill_MultipleTokens_OneCrossRun_OnePlain()
    {
        var template = BuildDocx(doc =>
        {
            var p = doc.CreateParagraph();
            p.CreateRun().SetText("{{a}}");
            p.CreateRun().SetText(" 与 ");
            p.CreateRun().SetText("{{b");
            p.CreateRun().SetText("}}");
        });

        var result = new WordFillWriter().Fill(Request(template, ("a", "甲"), ("b", "乙")));

        using var doc = Open(result.Output);
        Assert.Equal("甲 与 乙", TextOf(doc.Paragraphs[0]));
        Assert.Equal(2, result.Report.Resolved);
        Assert.True(result.Report.Verified);
    }

    // ─────────────────────────── ③ 样式保留 ───────────────────────────

    [Fact]
    public void Fill_PreservesFormattingOfSurroundingRunsAndAnchorRun()
    {
        var template = BuildDocx(doc =>
        {
            var p = doc.CreateParagraph();

            var bold = p.CreateRun();
            bold.IsBold = true;
            bold.SetText("【加粗前缀】");

            var italicAnchor = p.CreateRun();
            italicAnchor.IsItalic = true;
            italicAnchor.SetText("{{v}}");

            var plain = p.CreateRun();
            plain.SetText("【普通后缀】");
        });

        var result = new WordFillWriter().Fill(Request(template, ("v", "VALUE")));

        using var doc = Open(result.Output);
        var runs = doc.Paragraphs[0].Runs;

        // 加粗前缀：内容与加粗都不变
        Assert.Equal("【加粗前缀】", runs[0].GetText(0));
        Assert.True(runs[0].IsBold);

        // ★ 写入的值继承「锚点 run」的斜体（模板作者意图：锚点什么格式，值就什么格式）
        Assert.Equal("VALUE", runs[1].GetText(0));
        Assert.True(runs[1].IsItalic);

        // 普通后缀：不变
        Assert.Equal("【普通后缀】", runs[2].GetText(0));
        Assert.False(runs[2].IsBold);
    }

    [Fact]
    public void Fill_ValueWithLeadingTrailingSpace_IsPreserved()
    {
        // 锚点独占一个 run ⇒ 替换后该 run 的文本 = " 空白 "（首尾都是空格）
        var template = BuildDocx(doc =>
        {
            var p = doc.CreateParagraph();
            p.CreateRun().SetText("[");
            p.CreateRun().SetText("{{v}}");
            p.CreateRun().SetText("]");
        });

        var result = new WordFillWriter().Fill(Request(template, ("v", " 空白 ")));

        using var doc = Open(result.Output);
        var runs = doc.Paragraphs[0].Runs;

        Assert.Equal("[ 空白 ]", TextOf(doc.Paragraphs[0]));
        // ★ 承载值的 run 首尾是空格 ⇒ 必须写 xml:space="preserve"，否则 Word 会吞掉空格
        Assert.Equal("preserve", runs[1].GetCTR().GetTArray(0).space);
    }

    // ─────────────────────────── ④ 表格 ───────────────────────────

    [Fact]
    public void Fill_TableCells_AllCellsWritten()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(2, 3);
            PutCell(table, 0, 0, "项目");
            PutCell(table, 0, 1, "{{item}}");
            PutCell(table, 0, 2, "备注");
            PutCell(table, 1, 0, "{{qty}}");
            PutCell(table, 1, 1, "{{unit}}");
            PutCell(table, 1, 2, "-");
        });

        var result = new WordFillWriter().Fill(
            Request(template, ("item", "钢材"), ("qty", "100"), ("unit", "吨")));

        using var doc = Open(result.Output);
        var t = doc.Tables[0];
        Assert.Equal("项目", t.GetRow(0).GetCell(0).GetText());
        Assert.Equal("钢材", t.GetRow(0).GetCell(1).GetText());
        Assert.Equal("100", t.GetRow(1).GetCell(0).GetText());
        Assert.Equal("吨", t.GetRow(1).GetCell(1).GetText());
        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void CloneAndInsert_DeepCopiesRowFormattingAndContent()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(2, 2);
            PutCell(table, 0, 0, "样例A");
            PutCell(table, 0, 1, "样例B");
            PutCell(table, 1, 0, "尾行A");
            PutCell(table, 1, 1, "尾行B");
        });

        using var doc = Open(template);
        var table = doc.Tables[0];

        var inserted = WordTableRowInserter.CloneAndInsert(table, sourceRowIndex: 0, insertAt: 1);

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("样例A", inserted.GetCell(0).GetText());
        Assert.Equal("样例B", inserted.GetCell(1).GetText());
        // 原第 1 行被挤到第 2 行
        Assert.Equal("尾行A", table.GetRow(2).GetCell(0).GetText());
        // ★ 深拷贝：改克隆行不影响源行
        inserted.GetCell(0).Paragraphs[0].Runs[0].SetText("改过");
        Assert.Equal("样例A", table.GetRow(0).GetCell(0).GetText());
    }

    [Fact]
    public void CloneAndInsertFilled_WritesNewRowValuesNotSourceRowValues()
    {
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(1, 2);
            PutCell(table, 0, 0, "{{name}}");
            PutCell(table, 0, 1, "{{qty}}");
        });

        using var doc = Open(template);
        var table = doc.Tables[0];

        var report = new OfficeFillReport { Kind = "word" };
        var req = Request(Array.Empty<byte>(), ("name", "乙行"), ("qty", "2"));
        var row = WordTableRowInserter.CloneAndInsertFilled(table, 0, 1, req, report, "表格[0]");

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("乙行", row.GetCell(0).GetText());
        Assert.Equal("2", row.GetCell(1).GetText());
    }

    // ─────────────── ⑤ 页眉（★ 只替换，不新建；页脚不做）───────────────

    [Fact]
    public void Fill_Header_ReplacesAnchorsInExistingHeader()
    {
        var template = BuildDocx(doc =>
        {
            doc.CreateParagraph().CreateRun().SetText("正文");

            var header = doc.CreateHeader(HeaderFooterType.DEFAULT);
            header.CreateParagraph().CreateRun().SetText("编号：{{@doc_no}}");
        });

        var result = new WordFillWriter().Fill(
            Request(template, ("@doc_no", "YZH-QM-2026-001")));

        using var doc = Open(result.Output);
        Assert.Equal("编号：YZH-QM-2026-001", doc.HeaderList[0].Paragraphs[0].Runs[0].GetText(0));
        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void Fill_Header_IsNeverCreatedByWriter()
    {
        // ★ 2026-10-02 用户规格：页眉「我们需要的是进行替换」⇒ 模板没有页眉时**不新建**。
        //   页眉里的字体/边框/logo/排版全是模板资产，程序自造一个只会把它破坏掉。
        var template = BuildDocx(doc => doc.CreateParagraph().CreateRun().SetText("正文"));

        var result = new WordFillWriter().Fill(Request(template, ("@doc_no", "X")));

        using var doc = Open(result.Output);
        Assert.Empty(doc.HeaderList);
        Assert.Empty(doc.FooterList);
    }

    [Fact]
    public void Fill_Footer_IsNotProcessedButIsReported()
    {
        // ★ 页脚属 Office/Excel 模板自身的设计能力，不在本系统范围 ⇒ 其中的锚点原样保留，
        //   且**必须**被自验收扫出来（不能让「没做」变成「静默没做」）。
        var template = BuildDocx(doc =>
        {
            doc.CreateParagraph().CreateRun().SetText("正文");

            var footer = doc.CreateFooter(HeaderFooterType.DEFAULT);
            footer.CreateParagraph().CreateRun().SetText("第 {{@page}} 页");
        });

        var result = new WordFillWriter().Fill(Request(template, ("@page", "1")));

        using var doc = Open(result.Output);
        Assert.Equal("第 {{@page}} 页", doc.FooterList[0].Paragraphs[0].Runs[0].GetText(0));
        Assert.Contains("{{@page}}", result.Report.LeftoverTokens);
    }

    // ─────────────────────── ⑥ 未命中 / 自验收 ───────────────────────

    [Fact]
    public void Fill_UnresolvedToken_IsBlankedByDefault()
    {
        // ★ 2026-10-02 用户规格：「针对填写或替换，如果没有值则自动将填写内容赋值为空」
        var template = BuildDocx(doc =>
            doc.CreateParagraph().CreateRun().SetText("A={{known}} B={{unknown}}"));

        var result = new WordFillWriter().Fill(Request(template, ("known", "1")));

        using var doc = Open(result.Output);
        Assert.Equal("A=1 B=", TextOf(doc.Paragraphs[0]));

        Assert.Equal(1, result.Report.Resolved);
        Assert.Equal(1, result.Report.Pending);
        Assert.Equal("unknown", result.Report.Pendings[0].AnchorCode);
        Assert.Equal("未提供值", result.Report.Pendings[0].Reason);

        // ★ 置空后不再有残留锚点 ⇒ 自验收通过；但 Pendings 仍如实记录
        //   （「空着」与「不知道空着」是两回事，后者才是要消灭的缺陷）
        Assert.Empty(result.Report.LeftoverTokens);
        Assert.True(result.Report.Verified);
        Assert.Equal(0.5, result.Report.Completion, 3);
    }

    [Fact]
    public void Fill_UnresolvedToken_IsKeptWhenExplicitlyRequested()
    {
        var template = BuildDocx(doc =>
            doc.CreateParagraph().CreateRun().SetText("A={{known}} B={{unknown}}"));

        var req = Request(template, ("known", "1"));
        req.KeepUnresolvedAsIs = true;   // 模板调试用：留着原文，便于人工看出哪一处没填

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);
        Assert.Equal("A=1 B={{unknown}}", TextOf(doc.Paragraphs[0]));

        Assert.Contains("{{unknown}}", result.Report.LeftoverTokens);
        Assert.False(result.Report.Verified);
    }

    [Fact]
    public void Fill_NoAnchors_CompletionIsZeroNotOne()
    {
        var template = BuildDocx(doc => doc.CreateParagraph().CreateRun().SetText("没有任何锚点"));

        var result = new WordFillWriter().Fill(Request(template));

        // ⛔ 与文本引擎 / 企业端 liveCompletion 同口径：无锚点记 0（不是 1）
        Assert.Equal(0, result.Report.Total);
        Assert.Equal(0.0, result.Report.Completion);
        Assert.True(result.Report.Verified);
    }

    // ─────────────────────── ⑦ 域（doc_domain）───────────────────────

    [Fact]
    public void Fill_FieldValue_WritesRealFieldStructure()
    {
        var template = BuildDocx(doc => doc.CreateParagraph().CreateRun().SetText("{{@page_no}}"));

        var req = new OfficeFillRequest { Template = template };
        req.Values["@page_no"] = new FillValue
        {
            AnchorCode = "@page_no",
            Kind = FillValueKind.Field,
            FieldInstruction = "PAGE",
            Text = "1",
        };

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);
        var ctp = doc.Paragraphs[0].GetCTP();

        // ★ 必须是五段式域结构，而不是一段死文本
        var runs = ctp.GetRList().ToList();
        Assert.Equal(5, runs.Count);
        Assert.Equal(NPOI.OpenXmlFormats.Wordprocessing.ST_FldCharType.begin, runs[0].GetFldCharArray(0).fldCharType);
        Assert.Equal(" PAGE ", runs[1].GetInstrTextArray(0).Value);
        Assert.Equal(NPOI.OpenXmlFormats.Wordprocessing.ST_FldCharType.separate, runs[2].GetFldCharArray(0).fldCharType);
        Assert.Equal("1", runs[3].GetTArray(0).Value);
        Assert.Equal(NPOI.OpenXmlFormats.Wordprocessing.ST_FldCharType.end, runs[4].GetFldCharArray(0).fldCharType);

        Assert.Equal(1, result.Report.Resolved);
        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void Fill_FieldValue_NotAloneInParagraph_IsNotWrittenAsField()
    {
        // 域是块级元素 ⇒ 锚点不独占一段时不写域，退化为普通文本替换
        var template = BuildDocx(doc => doc.CreateParagraph().CreateRun().SetText("页码：{{@page_no}} 结束"));

        var req = new OfficeFillRequest { Template = template };
        req.Values["@page_no"] = new FillValue
        {
            AnchorCode = "@page_no", Kind = FillValueKind.Field, FieldInstruction = "PAGE", Text = "1",
        };

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);
        Assert.Equal("页码：1 结束", TextOf(doc.Paragraphs[0]));
        Assert.Equal(0, doc.Paragraphs[0].GetCTP().SizeOfFldSimpleArray());
    }

    // ─────────────── ⑦ ★ 区域填充（表格标签 + 二维数据）───────────────

    /// <summary>读单元格文本（★ 不用 <c>cell.GetText()</c> —— 有段落缓存坑，见 <see cref="PutCell"/>）</summary>
    private static string CellText(XWPFTable table, int row, int col)
    {
        var sb = new StringBuilder();
        foreach (var p in table.GetRow(row).GetCell(col).Paragraphs)
            foreach (var r in p.Runs)
                sb.Append(r.GetText(0));
        return sb.ToString();
    }

    private static OfficeFillRegion WordTableRegion(string tag, params List<FillValue?>[] rows) => new()
    {
        Kind = OfficeRegionKind.WordTable,
        TableTag = tag,
        Rows = rows.ToList(),
    };

    [Fact]
    public void RegionFill_WordTable_WritesDataFromTagRowAndClonesExtraRows()
    {
        // ★ 2026-10-02 用户规格：「如果是 word 是选择表格标签，采用 json 结构进行填充」
        var template = BuildDocx(doc =>
        {
            doc.CreateParagraph().CreateRun().SetText("明细如下：");

            var table = doc.CreateTable(2, 2);
            PutCell(table, 0, 0, "名称");
            PutCell(table, 0, 1, "数量");

            // 数据起始行（同时是「数据多于模板」时的克隆源行）：标签写在这一行的任一格
            PutCell(table, 1, 0, "{{table:items}}");
            PutCell(table, 1, 1, "占位");
        });

        var req = Request(template);
        req.Regions.Add(WordTableRegion("items",
            new List<FillValue?> { new() { Text = "甲" }, new() { Kind = FillValueKind.Number, Number = 1 } },
            new List<FillValue?> { new() { Text = "乙" }, new() { Kind = FillValueKind.Number, Number = 2 } },
            new List<FillValue?> { new() { Text = "丙" }, null }));   // null ⇒ 写空

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);
        var table = doc.Tables[0];

        // 模板只给了 1 个数据行 ⇒ 后 2 行是克隆出来的
        Assert.Equal(4, table.Rows.Count);
        Assert.Equal("名称", CellText(table, 0, 0));
        Assert.Equal("数量", CellText(table, 0, 1));

        Assert.Equal("甲", CellText(table, 1, 0));
        Assert.Equal("1", CellText(table, 1, 1));
        Assert.Equal("乙", CellText(table, 2, 0));
        Assert.Equal("2", CellText(table, 2, 1));
        Assert.Equal("丙", CellText(table, 3, 0));
        Assert.Equal(string.Empty, CellText(table, 3, 1));

        // ★ 标签本身必须被清掉，否则会残留在成品文件里
        Assert.DoesNotContain("{{table:", string.Join("|", new[]
        {
            CellText(table, 0, 0), CellText(table, 0, 1),
            CellText(table, 1, 0), CellText(table, 1, 1),
        }));

        // ★ 区域填充单独统计，⛔ 不计入 Completion
        Assert.Single(result.Report.Regions);
        Assert.True(result.Report.Regions[0].Matched);
        Assert.Equal(3, result.Report.Regions[0].RowCount);
        Assert.Equal(2, result.Report.Regions[0].ClonedRows);
        Assert.Equal(0, result.Report.Total);

        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void RegionFill_WordTable_UnmatchedTag_IsReportedNotThrown()
    {
        // 模板写错标签 ⇒ 不抛异常（167 份批量跑时不该中断），而是如实报「没找到」
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(1, 1);
            PutCell(table, 0, 0, "无标签");
        });

        var req = Request(template);
        req.Regions.Add(WordTableRegion("items", new List<FillValue?> { new() { Text = "甲" } }));

        var result = new WordFillWriter().Fill(req);

        Assert.Single(result.Report.Regions);
        Assert.False(result.Report.Regions[0].Matched);
        Assert.Contains("{{table:items}}", result.Report.Regions[0].Message);
    }

    [Fact]
    public void RegionFill_WordTable_DataFewerThanTemplate_LeavesExtraRowsAlone()
    {
        // 数据行少于模板行 ⇒ 只写覆盖到的行，⛔ 不删行（删行会破坏模板的合并/边框结构）
        var template = BuildDocx(doc =>
        {
            var table = doc.CreateTable(3, 1);
            PutCell(table, 0, 0, "{{table:items}}");
            PutCell(table, 1, 0, "模板行2");
            PutCell(table, 2, 0, "模板行3");
        });

        var req = Request(template);
        req.Regions.Add(WordTableRegion("items", new List<FillValue?> { new() { Text = "唯一一行" } }));

        var result = new WordFillWriter().Fill(req);

        using var doc = Open(result.Output);
        var table = doc.Tables[0];

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("唯一一行", CellText(table, 0, 0));
        Assert.Equal("模板行2", CellText(table, 1, 0));   // 未被数据覆盖 ⇒ 原样
        Assert.Equal("模板行3", CellText(table, 2, 0));
        Assert.Equal(0, result.Report.Regions[0].ClonedRows);
    }
}
