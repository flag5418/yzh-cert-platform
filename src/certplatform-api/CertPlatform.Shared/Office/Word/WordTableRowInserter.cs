using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word 表格行克隆插入 —— 对应 <c>doc_table_write</c> 的插行原语（26 号 §3.4.6）。
///
/// <para><b>★ 为什么必须克隆 <c>CT_Row</c> 而不是 <c>CreateRow()</c></b>：
/// <c>CreateRow()</c> 建出的是**空白行** —— 丢掉列宽、单元格合并（<c>gridSpan</c> / <c>vMerge</c>）、
/// 底纹、边框、行高、单元格对齐。症状是「插进去的行比模板行矮、合并单元格散开」，
/// 且 <b>Word 打开时不会报错</b>，只有肉眼比对才发现。</para>
///
/// <para><b>★ 顺序陷阱</b>：先插行再填值 —— 因为插入的行是**源行的深拷贝**，
/// 里面还带着源行的锚点原文，插入后必须**立刻**用新记录的值覆盖，
/// 否则重复区会出现「所有行都是第一行的值」。</para>
/// </summary>
public static class WordTableRowInserter
{
    /// <summary>
    /// 深拷贝 <paramref name="sourceRowIndex"/> 行并插入到 <paramref name="insertAt"/> 位置。
    /// <para><paramref name="insertAt"/> &lt; 0 或 &gt; 行数 ⇒ 追加到末尾。</para>
    /// </summary>
    /// <returns>新插入的行（<b>深拷贝</b>，含源行的全部格式与内容）</returns>
    public static XWPFTableRow CloneAndInsert(XWPFTable table, int sourceRowIndex, int insertAt = -1)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (sourceRowIndex < 0 || sourceRowIndex >= table.Rows.Count)
            throw new ArgumentOutOfRangeException(
                nameof(sourceRowIndex),
                $"源行索引 {sourceRowIndex} 越界（当前 {table.Rows.Count} 行）");

        var source = table.GetRow(sourceRowIndex);
        if (source == null)
            throw new InvalidOperationException($"表格第 {sourceRowIndex} 行不存在");

        var rowCount = table.Rows.Count;
        var pos = insertAt < 0 || insertAt > rowCount ? rowCount : insertAt;

        // ★ 深拷贝 CT_Row（保留 gridSpan / vMerge / 行高 / 底纹 / 边框）
        var clone = (CT_Row)source.GetCTRow().Copy();

        // ★★ 必须走 table.AddRow(...)：它同时更新 CT_Tbl 与 XWPFTable 内部的行缓存。
        //    ⛔ 直接操作 CT_Tbl（InsertNewTr + SetTrArray）**只改 XML、不改缓存** ⇒
        //       症状是「重新打开文件行数对了，但当前会话里 table.Rows.Count 还是旧值」
        //       （实测：trList=3 而 Rows.Count=2）—— 本仓 §二十 的典型「静默不一致」。
        //    ⛔ 也不能用无参 CloneRow()：它只会把行**追加到表尾**（2.7.2 没有 CloneRow(pos) 重载）。
        var newRow = new XWPFTableRow(clone, table);
        if (!table.AddRow(newRow, pos))
            throw new InvalidOperationException(
                $"行插入失败：位置 {pos} 非法（表格当前 {rowCount} 行）");

        return newRow;
    }

    /// <summary>
    /// 克隆插入 + 立刻填充新行（避免「所有重复行都是第一行的值」）。
    /// <para>新行的每个单元格按段落走一次锚点替换（含嵌套表格）。</para>
    /// </summary>
    /// <returns>新插入的行</returns>
    public static XWPFTableRow CloneAndInsertFilled(
        XWPFTable table,
        int sourceRowIndex,
        int insertAt,
        OfficeFillRequest request,
        OfficeFillReport report,
        string locationPrefix)
    {
        var newRow = CloneAndInsert(table, sourceRowIndex, insertAt);

        var cells = newRow.GetTableCells();
        for (var ci = 0; ci < cells.Count; ci++)
        {
            var cell = cells[ci];
            var cellLocation = $"{locationPrefix} 行 {insertAt} 列 {ci + 1}";

            foreach (var p in cell.Paragraphs)
                WordParagraphFiller.Fill(p, request, report, FillLocationKind.TableCell, cellLocation);

            foreach (var nested in cell.Tables)
                FillTableRecursive(nested, cellLocation, request, report);
        }

        return newRow;
    }

    /// <summary>递归填充一张表（含嵌套表）—— 供 <see cref="WordFillWriter"/> 与本类共用。</summary>
    internal static void FillTableRecursive(
        XWPFTable table, string locationPrefix, OfficeFillRequest request, OfficeFillReport report)
    {
        var rows = table.Rows;
        for (var ri = 0; ri < rows.Count; ri++)
        {
            var cells = rows[ri].GetTableCells();
            for (var ci = 0; ci < cells.Count; ci++)
            {
                var cellLocation = $"{locationPrefix} 行 {ri + 1} 列 {ci + 1}";

                foreach (var p in cells[ci].Paragraphs)
                    WordParagraphFiller.Fill(p, request, report, FillLocationKind.TableCell, cellLocation);

                foreach (var nested in cells[ci].Tables)
                    FillTableRecursive(nested, cellLocation, request, report);
            }
        }
    }
}
