using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Fill;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Excel;
using CertPlatform.Shared.Office.Word;
using NPOI.SS.UserModel;
using NPOI.WP.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
///     <b>AI 返回 → 值 → 落笔 Word/Excel</b> 的端到端契约测试。
///
///     <para><b>★ 为什么需要它（高频使用）</b>：AI 节点会长期、大量使用。这条链上有三段各自
///     被测过（<c>AiFillJsonReader</c> 取值 / <c>FillValueFactory</c> 定型 / 写入器落笔），
///     但<b>从没被串起来测过</b> ⇒ 任何一段改动都可能在接缝处静默断掉，而单段测试全绿。
///     本文件把真实路径串起来：<b>模型 JSON → 读取 → 定型 → 写进 .docx/.xlsx → 读回校验</b>。</para>
///
///     <para><b>覆盖矩阵</b>：值类型（text / number / date）× 载体（Word / Excel）×
///     写入方式（覆盖 / 填充，即「整格换」vs「一句话中间换 <c>{{}}</c>」）。</para>
/// </summary>
public class AiFillEndToEndTests
{
    // ════════════════════════════════════════════════════════════════
    //  基础设施
    // ════════════════════════════════════════════════════════════════

    /// <summary>模拟真实 AI 返回：<c>{"fields":{"KEY":{"value":…,"confidence":…}}}</c></summary>
    private static string AiJson(string anchor, string value) =>
        $"{{\"fields\":{{\"{anchor}\":{{\"value\":{JsonSerializer.Serialize(value)},\"confidence\":0.92}}}}}}";

    /// <summary>走真实链路：AI JSON → <c>AiFillJsonReader</c> → <c>FillValueFactory</c> → <see cref="FillValue"/></summary>
    private static FillValue AiValue(string anchor, string value, string valueType, string? numberFormat = null)
    {
        using var doc = JsonDocument.Parse(AiJson(anchor, value));
        var root = AiFillJsonReader.NormalizeRoot(doc)!;
        var node = AiFillJsonReader.ReadObject(root, "fields", anchor);
        var text = AiFillJsonReader.AsString(AiFillJsonReader.ReadRaw(node, "value"));

        var (ok, val, err) = FillValueFactory.TryCreate(anchor, text, valueType, numberFormat);
        Assert.True(ok, err);
        return val!;
    }

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

    private static byte[] BuildXlsx(Action<ISheet> build)
    {
        using var wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("Sheet1");
        build(sheet);
        using var ms = new MemoryStream();
        wb.Write(ms, leaveOpen: false);
        return ms.ToArray();
    }

    private static ISheet OpenSheet(byte[] bytes) => new XSSFWorkbook(new MemoryStream(bytes, writable: false)).GetSheetAt(0);

    private static byte[] BuildDocx(Action<XWPFDocument> build)
    {
        using var doc = new XWPFDocument();
        build(doc);
        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    private static string ParagraphText(XWPFDocument doc, int index)
    {
        var sb = new StringBuilder();
        foreach (var r in doc.Paragraphs[index].Runs) sb.Append(r.GetText(0));
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════
    //  Word × 字段
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Word_AI文本_端到端_读回校验()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText("企业名称：{{ENT_NAME}}（以下简称本公司）"));

        var result = new WordFillWriter().Fill(
            Request(template, ("ENT_NAME", AiValue("ENT_NAME", "河北雄安尚龙认证有限公司", "text"))));

        using var doc = new XWPFDocument(new MemoryStream(result.Output, writable: false));
        Assert.Equal("企业名称：河北雄安尚龙认证有限公司（以下简称本公司）", ParagraphText(doc, 0));
        Assert.True(result.Report.Verified);
        Assert.Equal(1, result.Report.Resolved);
    }

    [Fact]
    public void Word_AI日期_按值类型写成标准日期文本()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText("审核日期：{{AUDIT_DATE}}"));

        var result = new WordFillWriter().Fill(
            Request(template, ("AUDIT_DATE", AiValue("AUDIT_DATE", "2026-03-11", "date"))));

        using var doc = new XWPFDocument(new MemoryStream(result.Output, writable: false));
        Assert.Equal("审核日期：2026-03-11", ParagraphText(doc, 0));
    }

    [Fact]
    public void Word_AI数值_带千分位格式()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText("合同金额：{{AMOUNT}} 元"));

        var result = new WordFillWriter().Fill(Request(template, ("AMOUNT",
            AiValue("AMOUNT", "1234.5", "number", "#,##0.00"))));

        using var doc = new XWPFDocument(new MemoryStream(result.Output, writable: false));
        Assert.Equal("合同金额：1,234.50 元", ParagraphText(doc, 0));
    }

    // ════════════════════════════════════════════════════════════════
    //  Excel × 字段  —— 数字必须落成数值类型（⛔ 不是文本）
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Excel_AI数值_写成数值单元格_可求和()
    {
        var template = BuildXlsx(sheet => sheet.CreateRow(0).CreateCell(0).SetCellValue("{{AMOUNT}}"));

        var result = new ExcelFillWriter().Fill(Request(template,
            ("AMOUNT", AiValue("AMOUNT", "1234.5", "number", "#,##0.00"))));

        var cell = OpenSheet(result.Output).GetRow(0).GetCell(0);
        Assert.Equal(CellType.Numeric, cell.CellType);
        Assert.Equal(1234.5, cell.NumericCellValue, 6);
        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void Excel_AI日期_写成日期单元格()
    {
        var template = BuildXlsx(sheet => sheet.CreateRow(0).CreateCell(0).SetCellValue("{{D}}"));

        var result = new ExcelFillWriter().Fill(Request(template,
            ("D", AiValue("D", "2026-03-11", "date", "yyyy-mm-dd"))));

        var cell = OpenSheet(result.Output).GetRow(0).GetCell(0);
        Assert.Equal(CellType.Numeric, cell.CellType);
        Assert.Equal(new DateTime(2026, 3, 11), cell.DateCellValue);
    }

    [Fact]
    public void Excel_AI文本_一句话中间带token_填充模式只换token()
    {
        var template = BuildXlsx(sheet => sheet.CreateRow(0).CreateCell(0).SetCellValue("本公司共 {{QTY}} 台设备"));

        var v = AiValue("QTY", "3", "number");
        v.WriteMode = "replace";   // 填充：只换 {{}}，其余文字保留

        var result = new ExcelFillWriter().Fill(Request(template, ("QTY", v)));

        var cell = OpenSheet(result.Output).GetRow(0).GetCell(0);
        Assert.Equal("本公司共 3 台设备", cell.StringCellValue);
    }

    [Fact]
    public void Excel_AI文本_整格token_覆盖模式整格换()
    {
        var template = BuildXlsx(sheet => sheet.CreateRow(0).CreateCell(0).SetCellValue("{{NAME}}"));

        var v = AiValue("NAME", "河北雄安尚龙认证有限公司", "text");
        v.WriteMode = "overwrite";   // 覆盖：整格换成取值

        var result = new ExcelFillWriter().Fill(Request(template, ("NAME", v)));

        var cell = OpenSheet(result.Output).GetRow(0).GetCell(0);
        Assert.Equal("河北雄安尚龙认证有限公司", cell.StringCellValue);
    }

    [Fact]
    public void Excel_多个AI锚点_同一次填充全部落笔()
    {
        var template = BuildXlsx(sheet =>
        {
            var row = sheet.CreateRow(0);
            row.CreateCell(0).SetCellValue("{{ENT_NAME}}");
            row.CreateCell(1).SetCellValue("{{QTY}}");
            sheet.CreateRow(1).CreateCell(0).SetCellValue("{{AUDIT_DATE}}");
        });

        var result = new ExcelFillWriter().Fill(Request(template,
            ("ENT_NAME", AiValue("ENT_NAME", "某某公司", "text")),
            ("QTY", AiValue("QTY", "12", "number")),
            ("AUDIT_DATE", AiValue("AUDIT_DATE", "2026-03-11", "date", "yyyy-mm-dd"))));

        var sheet = OpenSheet(result.Output);
        Assert.Equal("某某公司", sheet.GetRow(0).GetCell(0).StringCellValue);
        Assert.Equal(12, sheet.GetRow(0).GetCell(1).NumericCellValue, 6);
        Assert.Equal(new DateTime(2026, 3, 11), sheet.GetRow(1).GetCell(0).DateCellValue);
        Assert.Equal(3, result.Report.Resolved);
        Assert.True(result.Report.Verified);
    }

    // ════════════════════════════════════════════════════════════════
    //  防御：模型返回空值 ⇒ 置空（不残留 {{}}），且报告可核
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Word_AI返回值缺失_置空_不残留token()
    {
        var template = BuildDocx(d => d.CreateParagraph().CreateRun().SetText("编号：{{DOC_NO}}"));

        // 模型给空串 ⇒ 值字典给空文本 ⇒ 写入器把 token 换成空（⛔ 不留 {{DOC_NO}}）
        var result = new WordFillWriter().Fill(Request(template,
            ("DOC_NO", new FillValue { Kind = FillValueKind.Text, Text = "" })));

        using var doc = new XWPFDocument(new MemoryStream(result.Output, writable: false));
        Assert.Equal("编号：", ParagraphText(doc, 0));
        Assert.True(result.Report.Verified);
        Assert.Empty(result.Report.LeftoverTokens);
    }

    // ════════════════════════════════════════════════════════════════
    //  ★★ AI 表格：AI JSON → fill_table → 区域指令 → 落笔 Word/Excel
    //  ⛔ 这段此前**完全没有集成测试** —— 值链测过、区域写入器测过，
    //     但「AI 的行数组 → 列映射 → 区域定位」这道缝从没被串起来。
    // ════════════════════════════════════════════════════════════════

    /// <summary>往 Word 表格单元格写文本（⛔ 不用 <c>cell.SetText()</c>：它不刷新段落缓存，读回是空串）</summary>
    private static void PutCell(XWPFTable table, int row, int col, string text)
    {
        var cell = table.GetRow(row).GetTableCells()[col];
        if (cell.Paragraphs.Count == 0) cell.AddParagraph();

        var p = cell.Paragraphs[0];
        if (p.Runs.Count == 0) p.CreateRun();
        p.Runs[0].SetText(text);
    }

    /// <summary>拼接某格的段落文本（★ 必须拼全部 run —— 标记/内容跨 run 是 Word 常态）</summary>
    private static string CellText(XWPFTable table, int row, int col)
    {
        var sb = new StringBuilder();
        foreach (var p in table.GetRow(row).GetTableCells()[col].Paragraphs)
            foreach (var r in p.Runs) sb.Append(r.GetText(0));
        return sb.ToString();
    }

    /// <summary>
    /// 往 Excel 某格写文本。
    /// <para>⚠️ <b>必须 get-or-create</b>：NPOI 的 <c>sheet.CreateRow(r)</c> 对已存在的行是
    /// <b>替换</b>（丢掉先写的格）⇒ 一行里连续两次 <c>CreateRow(r)</c> 会让第 1 格凭空消失，
    /// 而测试会因「格不存在」在后续断言里报 NRE 或读到错值（本文件真实踩到过）。</para>
    /// </summary>
    private static void SetCell(ISheet sheet, int row, int col, string text)
    {
        var r = sheet.GetRow(row) ?? sheet.CreateRow(row);
        r.CreateCell(col).SetCellValue(text);
    }

    /// <summary>
    /// 走真实链路：AI JSON（<c>tables</c> 段）→ <c>AiFillJsonReader</c> 取值 →
    /// <c>TablePayloadFactory</c> 组装 → <c>FillTableSkill</c> 列映射 → 区域指令。
    /// <para>⛔ 不落盘（与生产一致：Skill 只累积会话，落盘由写入器一次性做）。</para>
    /// </summary>
    private static FillSession AiTableSession(
        string aiJson, string tag, string columnsJson, string fileKind, byte[] template,
        int startRow = -1, int startCol = -1, string? sheetName = null)
    {
        using var doc = JsonDocument.Parse(aiJson);
        var root = AiFillJsonReader.NormalizeRoot(doc)!;
        var node = AiFillJsonReader.ReadSectionRaw(root, "tables", tag);

        var payload = TablePayloadFactory.FromAiNode(
            tag, TablePayloadFactory.ParseColumns(columnsJson), node);

        var session = new FillSession { FileKind = fileKind, Template = template };
        var r = FillTableSkill.ExecuteAsync(session, payload, startRow, startCol, sheetName)
            .GetAwaiter().GetResult();
        Assert.True(r.Success, r.Error);

        session.Request.Template = template;   // 生产里由编排器从 session 灌进请求
        return session;
    }

    [Fact]
    public void Word_AI表格_按标签落笔_数据多于模板行则克隆补齐()
    {
        const string columns = """
            [{"field_code":"name","title":"姓名"},{"field_code":"age","title":"年龄","kind":"number"}]
            """;
        const string ai = """
            {"tables":{"items":[{"name":"张三","age":"28"},{"name":"李四","age":"35"}]}}
            """;

        var template = BuildDocx(d =>
        {
            var t = d.CreateTable(2, 2);
            PutCell(t, 0, 0, "姓名");
            PutCell(t, 0, 1, "年龄");
            // ★ 数据区第一行写 {{table:items}} ⇒ 既是数据起始行，也是「数据多于模板」时的克隆源行
            PutCell(t, 1, 0, "{{table:items}}");
            PutCell(t, 1, 1, "");
        });

        var session = AiTableSession(ai, "items", columns, "word", template);
        var result = new WordFillWriter().Fill(session.Request);

        using var doc = new XWPFDocument(new MemoryStream(result.Output, writable: false));
        var table = doc.Tables[0];

        Assert.Equal("张三", CellText(table, 1, 0));
        Assert.Equal("28", CellText(table, 1, 1));
        // ★ 第 2 行来自「克隆数据起始行」（保留合并/边框/行高），⛔ 不是新建空行
        Assert.Equal("李四", CellText(table, 2, 0));
        Assert.Equal("35", CellText(table, 2, 1));
        // ★ 标记必须被清掉 —— 否则会残留在成品文件里（用户会看到 {{table:items}}）
        Assert.DoesNotContain("{{table:items}}", CellText(table, 1, 0));
        // ★ 表头行不被动（数据区从标记行开始）
        Assert.Equal("姓名", CellText(table, 0, 0));

        Assert.Single(result.Report.Regions);
        Assert.True(result.Report.Regions[0].Matched);
        Assert.Equal(2, result.Report.Regions[0].RowCount);
        Assert.Equal(1, result.Report.Regions[0].ClonedRows);
    }

    [Fact]
    public void Excel_AI表格_按起始行列落笔_数值列落成数值格_表头不被盖()
    {
        const string columns = """
            [{"field_code":"item","title":"项目"},
             {"field_code":"amount","title":"金额","kind":"number","format":"#,##0.00"}]
            """;
        const string ai = """
            {"tables":{"costs":[{"item":"审核费","amount":"1234.5"},{"item":"差旅费","amount":"200"}]}}
            """;

        var template = BuildXlsx(sheet =>
        {
            SetCell(sheet, 0, 0, "项目");   // 表头
            SetCell(sheet, 0, 1, "金额");
            SetCell(sheet, 1, 0, "占位");
            SetCell(sheet, 2, 0, "占位");
        });

        var session = AiTableSession(ai, "costs", columns, "excel", template, startRow: 1, startCol: 0);
        var result = new ExcelFillWriter().Fill(session.Request);

        var sheet = OpenSheet(result.Output);
        Assert.Equal("审核费", sheet.GetRow(1).GetCell(0).StringCellValue);
        Assert.Equal("差旅费", sheet.GetRow(2).GetCell(0).StringCellValue);
        // ★ 声明 number ⇒ 必须是数值单元格（能求和），⛔ 不是左对齐文本
        Assert.Equal(CellType.Numeric, sheet.GetRow(1).GetCell(1).CellType);
        Assert.Equal(1234.5, sheet.GetRow(1).GetCell(1).NumericCellValue, 6);
        Assert.Equal(200, sheet.GetRow(2).GetCell(1).NumericCellValue, 6);
        Assert.Equal("#,##0.00", sheet.GetRow(1).GetCell(1).CellStyle.GetDataFormatString());
        // ★ 起始行=1 ⇒ 表头不动
        Assert.Equal("项目", sheet.GetRow(0).GetCell(0).StringCellValue);
    }

    [Fact]
    public void 表格_AI缺某列值_该格写空_其余列不被左移错位()
    {
        const string columns = """
            [{"field_code":"a","title":"A"},{"field_code":"b","title":"B"}]
            """;
        const string ai = """
            {"tables":{"t":[{"a":"甲"}]}}
            """;   // ★ 只给了 a，b 缺失

        var template = BuildXlsx(sheet =>
        {
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 2; c++)
                    SetCell(sheet, r, c, $"{r}-{c}");
        });

        var session = AiTableSession(ai, "t", columns, "excel", template, startRow: 0, startCol: 0);
        var result = new ExcelFillWriter().Fill(session.Request);

        var sheet = OpenSheet(result.Output);
        Assert.Equal("甲", sheet.GetRow(0).GetCell(0).StringCellValue);
        // ★ 缺列 ⇒ 写空。⛔ 若实现成「跳过 ⇒ 后面的值左移」，整行错位且**不报错**
        Assert.Equal(string.Empty, sheet.GetRow(0).GetCell(1).StringCellValue);
    }

    [Fact]
    public void 表格_AI返回columns_rows对象形态_列定义仍只取自输入_多余键不写入()
    {
        const string columns = """
            [{"field_code":"name","title":"姓名"}]
            """;
        // ★ AI 自带 columns（必须被丢弃）+ 行里多出的 extra 键（必须被忽略）
        const string ai = """
            {"tables":{"t":{"columns":[{"field_code":"evil"}],
                              "rows":[{"name":"张三","extra":"泄漏值"}]}}}
            """;

        var template = BuildXlsx(sheet => SetCell(sheet, 0, 0, "占位"));   // ⛔ 不预建第 2 列：本用例要验证它「没被创建」

        var session = AiTableSession(ai, "t", columns, "excel", template, startRow: 0, startCol: 0);
        var result = new ExcelFillWriter().Fill(session.Request);

        var sheet = OpenSheet(result.Output);
        Assert.Equal("张三", sheet.GetRow(0).GetCell(0).StringCellValue);
        // ★ 列由模板决定 ⇒ AI 自造的列 / 多出的键都⛔ 不写入（否则会静默多出一列）
        Assert.Null(sheet.GetRow(0).GetCell(1));   // 第 2 列根本没被创建
        Assert.Null(sheet.GetRow(0).GetCell(2));   // 第 3 列根本没被创建
    }

    [Fact]
    public void 表格_AI值与列声明类型不符_整单失败_不静默降级()
    {
        const string columns = """
            [{"field_code":"amount","title":"金额","kind":"number"}]
            """;
        const string ai = """
            {"tables":{"t":[{"amount":"叁万元"}]}}
            """;

        using var doc = JsonDocument.Parse(ai);
        var root = AiFillJsonReader.NormalizeRoot(doc)!;
        var payload = TablePayloadFactory.FromAiNode("t",
            TablePayloadFactory.ParseColumns(columns),
            AiFillJsonReader.ReadSectionRaw(root, "tables", "t"));

        var session = new FillSession { FileKind = "excel", Template = new byte[] { 1 } };
        var r = FillTableSkill.ExecuteAsync(session, payload, 0, 0, null).GetAwaiter().GetResult();

        // ★ 报错，⛔ 不降级成文本 —— 静默降级会让 Excel 数字变文本，打开看不出来
        Assert.False(r.Success);
        Assert.NotNull(r.Error);
        Assert.Contains("amount", r.Error!);
        Assert.Empty(session.Request.Regions);   // ★ 失败 ⇒ 不产出任何区域指令
    }

    [Fact]
    public void Word_AI表格_模板缺标签_报告未命中_不静默()
    {
        const string columns = """
            [{"field_code":"name","title":"姓名"}]
            """;
        const string ai = """
            {"tables":{"missing":[{"name":"张三"}]}}
            """;

        var template = BuildDocx(d =>
        {
            var t = d.CreateTable(1, 1);
            PutCell(t, 0, 0, "无标记");
        });

        var session = AiTableSession(ai, "missing", columns, "word", template);
        var result = new WordFillWriter().Fill(session.Request);

        // ★ 标签写错/漏写 ⇒ 必须报告未命中（而不是「填了但什么都没有」）
        Assert.Single(result.Report.Regions);
        Assert.False(result.Report.Regions[0].Matched);
        Assert.Contains("{{table:missing}}", result.Report.Regions[0].Message!);
    }
}
