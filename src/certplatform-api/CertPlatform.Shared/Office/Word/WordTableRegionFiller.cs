using System.Text;
using CertPlatform.Shared.Fill;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word 表格**区域填充** —— 把「表格标签 + 二维数据」落成真实写入。
///
/// <para><b>★ 对应 2026-10-02 用户规格</b>：「如果是 word 是<b>选择表格标签</b>，
/// 采用 <b>json 结构</b>进行填充」。</para>
///
/// <para><b>怎么定位表格</b>：模板作者在**数据区第一行**的任一单元格里写
/// <c>{{table:items}}</c>，本器扫描全文档（含嵌套表）找到该标记 ⇒
/// ① 该标记所在行 = <b>数据起始行</b>（同时是「数据多于模板」时的克隆源行）；
/// ② 标记本身**先被清空**（否则数据列范围不覆盖它时会残留在成品里）。</para>
///
/// <para><b>★ 为什么这样设计而不是「按表格序号」</b>：按序号（第 3 个表格）在模板增删内容后
/// 会**静默错位** —— 往文档前面加一段，所有序号平移，填充到错误的表里且不报错。
/// 标签是内容锚定，模板怎么改都不影响。</para>
///
/// <para><b>边界（写死，本轮定死）</b>：</para>
/// <list type="bullet">
///   <item>行不足 ⇒ <b>克隆起始行补齐</b>（深拷贝 <c>CT_Row</c>，保留合并/边框/行高）；</item>
///   <item>行多余 ⇒ <b>多余行不删</b>（删行会破坏模板结构）；</item>
///   <item>列多于模板列 ⇒ <b>丢弃多出的列</b>（⛔ 不建新列 —— 表格结构由模板控制），
///         <b>并记入 <see cref="OfficeFillRegionHit.Warnings"/></b>
///         （2026-10-05 修正：过去是<b>静默</b> <c>break</c>，起始列配错时整批值消失而报告毫无痕迹）；</item>
///   <item>单元格样式 ⇒ <b>一律沿用模板</b>（见 <see cref="WordCellWriter"/>）。</item>
/// </list>
/// </summary>
internal static class WordTableRegionFiller
{
    /// <summary>执行请求中全部 <see cref="OfficeRegionKind.WordTable"/> 区域</summary>
    public static void Fill(XWPFDocument doc, OfficeFillRequest request, OfficeFillReport report)
    {
        if (request.Regions.Count == 0) return;

        foreach (var region in request.Regions)
            if (region.Kind == OfficeRegionKind.WordTable)
                FillOne(doc, region, report);
    }

    private static void FillOne(XWPFDocument doc, OfficeFillRegion region, OfficeFillReport report)
    {
        var token = "{{table:" + (region.TableTag ?? string.Empty) + "}}";
        var hit = new OfficeFillRegionHit { Location = region.Describe() };

        if (!FindTag(doc.Tables, token, out var table, out var startRow, out var tagCell) || table is null)
        {
            hit.Matched = false;
            hit.Message = $"模板里没有 {token} 标签（表格标签必须写在数据区第一行的某个单元格里）";
            report.Regions.Add(hit);
            return;
        }

        hit.Matched = true;

        // ★ 先清标记：否则当数据列范围不覆盖标记所在列时，{{table:xxx}} 会留在成品文件里
        WordCellWriter.SetText(tagCell!, string.Empty);

        for (var i = 0; i < region.Rows.Count; i++)
        {
            var targetIndex = startRow + i;

            if (targetIndex >= table.Rows.Count)
            {
                // 数据比模板给的行多 ⇒ 克隆「数据起始行」补齐（深拷贝保留合并/边框/行高）
                WordTableRowInserter.CloneAndInsert(table, startRow, targetIndex);
                hit.ClonedRows++;
            }

            var cells = table.GetRow(targetIndex).GetTableCells();
            var data = region.Rows[i] ?? new List<FillValue?>();

            for (var j = 0; j < data.Count; j++)
            {
                var col = region.StartCol + j;
                if (col >= cells.Count)
                {
                    // ⛔ 不建新列：表格结构由模板控制。
                    // ★ 但「丢弃」必须被看见（2026-10-05 修正）—— 过去这里是一句静默的 break，
                    //   起始列/列数配错时整批值凭空消失，而报告里看不出任何异常。
                    //   这正是「试填验证 ⇒ 让用户发现参数指定错误」要消灭的缺陷。
                    hit.Warnings.Add(
                        $"表格[{region.TableTag}] 第 {targetIndex + 1} 行第 {col + 1} 列超出模板列数" +
                        $"（该行共 {cells.Count} 列）⇒ 该行剩余 {data.Count - j} 个值未写入");
                    break;
                }

                // ★ 值为 null ⇒ 写空串（用户规格：「如果没有值则自动将填写内容赋值为空」）
                WordCellWriter.Write(cells[col], data[j]);
            }

            hit.RowCount++;
        }

        report.Regions.Add(hit);
    }

    /// <summary>在全部表格（含嵌套）里找含 <paramref name="token"/> 的单元格</summary>
    private static bool FindTag(
        IList<XWPFTable> tables,
        string token,
        out XWPFTable? table,
        out int rowIndex,
        out XWPFTableCell? tagCell)
    {
        foreach (var t in tables)
        {
            for (var r = 0; r < t.Rows.Count; r++)
            {
                var cells = t.GetRow(r).GetTableCells();
                for (var c = 0; c < cells.Count; c++)
                {
                    if (!CellContains(cells[c], token)) continue;

                    table = t;
                    rowIndex = r;
                    tagCell = cells[c];
                    return true;
                }
            }

            // 递归嵌套表
            foreach (var row in t.Rows)
                foreach (var cell in row.GetTableCells())
                    if (cell.Tables.Count > 0 &&
                        FindTag(cell.Tables, token, out table, out rowIndex, out tagCell))
                        return true;
        }

        table = null;
        rowIndex = -1;
        tagCell = null;
        return false;
    }

    /// <summary>
    /// 单元格文本是否含 <paramref name="token"/>。
    /// <para>★ 必须**拼接全部 run** 再判断 —— 标记跨 run 是 Word 的常态（见 REFERENCE §二十 ㊴）。</para>
    /// </summary>
    private static bool CellContains(XWPFTableCell cell, string token)
    {
        foreach (var p in cell.Paragraphs)
        {
            var sb = new StringBuilder();
            foreach (var r in p.Runs) sb.Append(WordRunText.Get(r));

            if (sb.ToString().Contains(token, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
