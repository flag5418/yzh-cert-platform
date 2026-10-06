using NPOI.WP.UserModel;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word（.docx）填充写入器 —— 层 1 的对外门面（对应 26 号 §3.4.6 的 <c>doc_cell_write</c> /
/// <c>doc_replace</c> / <c>doc_table_write</c> / <c>doc_domain</c> / <c>doc_save</c>）。
///
/// <para><b>职责边界（写死）</b>：本类<b>只做确定性写入</b> ——
/// 定位锚点 → 写值 → 保留样式 → 自验收。
/// ⛔ <b>不做任何语义判断</b>：不猜值、不补值、不按上下文改写值、不决定「这个框该填什么」。
/// 那些全部属于层 2（值字典的产出方）。</para>
///
/// <para><b>★ 范围边界（2026-10-02 用户规格定死）</b>：</para>
/// <list type="bullet">
///   <item><b>样式</b>：<b>不是程序设定的，是模板控制的</b> ⇒ 本类只「沿用」不「新建」；</item>
///   <item><b>单元格合并</b>：<b>由 Office 模板考虑</b> ⇒ 本类不主动合并/拆分；</item>
///   <item><b>页眉</b>：<b>只做替换</b>（模板已定义好信息与排版），⛔ 不新建页眉；</item>
///   <item><b>页脚</b>：<b>不做</b>（属 Office/Excel 模板自身的设计能力，不在本系统范围）；</item>
///   <item><b>重点</b>：<b>单元格 + 表格填充</b>。</item>
/// </list>
///
/// <para><b>执行顺序</b>：① 表格标签区域填充 → <b>①b 坐标定位单元格填充</b> → ② 正文段落锚点 →
/// ③ 表格单元格锚点 → ④ 页眉锚点 → ⑤ 自验收 → ⑥ 落盘。
/// ⚠️ <b>两类区域填充都必须先于锚点替换</b>：① 否则 <c>{{table:xxx}}</c> 会被当成普通锚点处理掉；
/// ①b 因为坐标是<b>显式</b>指定的，优先级高于「模板里恰好写着的 <c>{{}}</c>」——
/// 目标格里若有标记，会被坐标写入覆盖，这正是用户的意图。</para>
///
/// <para><b>线程安全</b>：无状态，可注册为单例。每次调用独立打开/写入一份文档。</para>
/// </summary>
public sealed class WordFillWriter
{
    /// <summary>执行一次填充</summary>
    public OfficeFillResult Fill(OfficeFillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Template is null || request.Template.Length == 0)
            throw new ArgumentException("模板字节为空：请先确认上传的是有效的 .docx 文件", nameof(request));

        var report = new OfficeFillReport { Kind = "word" };

        using var input = new MemoryStream(request.Template, writable: false);
        using var doc = new XWPFDocument(input);

        // ── ① 区域填充（表格标签 + 二维数据）★ 必须在锚点替换之前 ──
        WordTableRegionFiller.Fill(doc, request, report);

        // ── ①b 坐标填充（表格序号 + 行列）★ 同样先于锚点替换：坐标是显式指定，优先级更高 ──
        WordCellRegionFiller.Fill(doc, request, report);

        // ── ② 正文段落 ──
        var paragraphs = doc.Paragraphs;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            WordParagraphFiller.Fill(
                paragraphs[i], request, report, FillLocationKind.BodyParagraph, $"正文·第 {i + 1} 段");
        }

        // ── ③ 表格（递归含嵌套表）──
        var tables = doc.Tables;
        for (var i = 0; i < tables.Count; i++)
            WordTableRowInserter.FillTableRecursive(tables[i], $"表格[{i}]", request, report);

        // ── ④ 页眉（★ 只替换已有，⛔ 不新建；页脚不做）──
        if (request.FillHeader)
        {
            var headers = doc.HeaderList;
            for (var i = 0; i < headers.Count; i++)
                FillHeaderFooter(headers[i], FillLocationKind.Header, $"页眉[{i}]", request, report);
        }

        // ── ⑤ 自验收（★ 独立遍历，不复用上面的路径）──
        var scan = WordDocumentScanner.Scan(doc, request.MarkStyleId);
        report.LeftoverTokens = scan.LeftoverTokens;
        report.LeftoverMarkRuns = scan.LeftoverMarkRuns;

        // ── ⑥ 落盘 ──
        using var output = new MemoryStream();
        doc.Write(output);

        return new OfficeFillResult { Output = output.ToArray(), Report = report };
    }

    private static void FillHeaderFooter(
        XWPFHeaderFooter headerFooter,
        FillLocationKind kind,
        string label,
        OfficeFillRequest request,
        OfficeFillReport report)
    {
        var paragraphs = headerFooter.Paragraphs;
        for (var i = 0; i < paragraphs.Count; i++)
        {
            WordParagraphFiller.Fill(
                paragraphs[i], request, report, kind, $"{label}·第 {i + 1} 段");
        }

        var tables = headerFooter.Tables;
        for (var i = 0; i < tables.Count; i++)
            WordTableRowInserter.FillTableRecursive(tables[i], $"{label}·表格[{i}]", request, report);
    }
}
