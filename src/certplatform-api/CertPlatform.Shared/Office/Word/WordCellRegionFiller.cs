using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// Word <b>坐标定位</b>单元格填充 —— 按「第几个表格 + 第几行 + 第几列」直接写格，
/// ⛔ <b>不依赖模板里的任何标记</b>（对应 <see cref="OfficeRegionKind.WordCell"/>）。
///
/// <para><b>★ 为什么需要它（2026-10-05 用户规格）</b>：真实模板里大量存在
/// 「位置固定、格子里什么标记都没有」的表格 —— 例如房产测绘参考实现
/// <c>table.SetText(0, 2, projectinfo.Id)</c>，项目代码永远在第 0 个表的第 0 行第 2 列。
/// 这类位置<b>扫描器扫不出来</b>（没有 <c>{{}}</c>），只能由人在页面上指定坐标。</para>
///
/// <para><b>★★ 与 <see cref="WordTableRegionFiller"/> 的语义差别（核心）</b>：</para>
/// <list type="table">
/// <item>
///   <term><see cref="WordTableRegionFiller"/></term>
///   <description><b>标签定位 + 数据驱动</b>：数据有几行就写几行 ⇒ 行不足<b>克隆补行</b>。
///   「模板给几行」只是排版示意。</description>
/// </item>
/// <item>
///   <term>本类</term>
///   <description><b>坐标定位 + 精确写入</b>：行列是<b>人工指定</b>的 ⇒
///   目标不存在就是<b>参数填错</b> ⇒ <b>记 <c>Warnings</c></b>，
///   ⛔ <b>不克隆补行、⛔ 不静默丢弃</b>。</description>
/// </item>
/// </list>
///
/// <para><b>为什么「不克隆」是关键</b>：若行号写大了就克隆一行出来，用户会看到
/// 「填成功了」，但值落在一个<b>模板里本来没有的行</b>上 —— 打印出来才发现表格多了一行。
/// 这类缺陷<b>在报告里完全看不出来</b>，正是 2026-10-05 用户要的
/// 「试填验证 ⇒ 让用户发现参数指定错误」要消灭的东西。</para>
///
/// <para>⚠️ <b>列号口径</b>：用 <c>GetTableCells()</c> 的<b>索引</b>（0-based），
/// <b>⛔ 不折算 <c>gridSpan</c></b>。含跨列合并的行里，「视觉列号」会大于 cell 索引 ⇒
/// 用户按肉眼看表格数出来的列号<b>可能对不上</b>。这是当前明确接受的边界
/// （2026-10-02 用户规格：「我用的 npoi 的替换来解决，当然这是针对<b>非常普通的单元格</b>」）。
/// 若日后要折算，落点在本类的 <c>cells[col]</c> 取值处，⛔ 不要动 <see cref="WordCellWriter"/>。</para>
///
/// <para><b>执行顺序</b>：必须<b>先于</b>锚点替换（同 <see cref="WordTableRegionFiller"/>）——
/// 坐标是<b>显式</b>指定的，优先级高于「模板里恰好写着的 <c>{{}}</c>」；
/// 若目标格里本来有标记，坐标写入会把它覆盖掉，这正是用户的意图。</para>
/// </summary>
internal static class WordCellRegionFiller
{
    /// <summary>执行请求中全部 <see cref="OfficeRegionKind.WordCell"/> 区域</summary>
    public static void Fill(XWPFDocument doc, OfficeFillRequest request, OfficeFillReport report)
    {
        if (request.Regions.Count == 0) return;

        foreach (var region in request.Regions)
            if (region.Kind == OfficeRegionKind.WordCell)
                FillOne(doc, region, report);
    }

    private static void FillOne(XWPFDocument doc, OfficeFillRegion region, OfficeFillReport report)
    {
        var hit = new OfficeFillRegionHit { Location = region.Describe() };

        // ── ① 表格序号：不存在 ⇒ 整块未匹配（这一块一个值都没写）──
        var tables = doc.Tables;
        if (region.TableIndex < 0 || region.TableIndex >= tables.Count)
        {
            hit.Matched = false;
            hit.Message =
                $"文档里没有第 {region.TableIndex} 个表格（共 {tables.Count} 个）" +
                "—— 表格序号按文档中出现顺序从 0 开始计";
            report.Regions.Add(hit);
            return;
        }

        hit.Matched = true;
        var table = tables[region.TableIndex];

        // ── ② 逐格写入；行/列不存在 ⇒ 记 Warning，⛔ 不克隆、⛔ 不静默 ──
        for (var i = 0; i < region.Rows.Count; i++)
        {
            var rowIndex = region.StartRow + i;
            var data = region.Rows[i] ?? new List<FillValue?>();

            if (rowIndex < 0 || rowIndex >= table.Rows.Count)
            {
                hit.Warnings.Add(
                    $"表格[{region.TableIndex}] 第 {rowIndex + 1} 行不存在" +
                    $"（该表共 {table.Rows.Count} 行）⇒ 该行的 {data.Count} 个值未写入");
                continue;
            }

            var cells = table.GetRow(rowIndex).GetTableCells();

            for (var j = 0; j < data.Count; j++)
            {
                var col = region.StartCol + j;

                if (col < 0 || col >= cells.Count)
                {
                    hit.Warnings.Add(
                        $"表格[{region.TableIndex}] 第 {rowIndex + 1} 行第 {col + 1} 列不存在" +
                        $"（该行共 {cells.Count} 列）⇒ 该格的值未写入");
                    continue;
                }

                // ★ 值为 null ⇒ 写空串（= 用户明确要清空这一格，算成功，⛔ 不算 Warning）
                WordCellWriter.Write(cells[col], data[j]);
            }

            hit.RowCount++;
        }

        report.Regions.Add(hit);
    }
}
