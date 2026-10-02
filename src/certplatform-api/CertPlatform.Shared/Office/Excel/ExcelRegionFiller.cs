using CertPlatform.Shared.Fill;
using NPOI.SS.UserModel;

namespace CertPlatform.Shared.Office.Excel;

/// <summary>
/// Excel 区域填充 —— 把「<b>起始行列 + 二维数据</b>」落成真实写入。
///
/// <para><b>★ 对应 2026-10-02 用户规格</b>：「从 excel 是<b>从哪一行，那一列开始</b>，
/// 采用 <b>json 结构</b>进行填充」。</para>
///
/// <para><b>★ 为什么「起始行列」是对的抽象</b>：参考实现（房产测绘 <c>YZH.Survey.Api</c>）
/// 用 <c>sheet.SetCellValue(row, col, value)</c> <b>把坐标硬编码在业务代码里</b> ——
/// 换一张模板（表头多一行、数据区右移一列）就要改代码。本抽象把坐标<b>提升为数据</b>：
/// 坐标由层 2（规则/配置）给出，写入器只按坐标落笔 ⇒ 同一份代码服务任意模板。</para>
///
/// <para><b>边界（写死）</b>：</para>
/// <list type="bullet">
///   <item>目标格已存在 ⇒ <b>样式原样保留</b>（模板控制的边框/底纹/对齐/字体全不动）；</item>
///   <item>目标格不存在 ⇒ 新建，并<b>从上一行同列复制样式</b>（沿用模板，⛔ 不凭空造样式）；</item>
///   <item>值为 <c>null</c> ⇒ <b>写空串</b>（用户规格：「没有值则自动赋值为空」）；</item>
///   <item>⛔ <b>不合并单元格</b>（合并由模板考虑）；⛔ <b>不插行/删行</b>（结构由模板定）。</item>
/// </list>
/// </summary>
internal static class ExcelRegionFiller
{
    /// <summary>执行请求中全部 <see cref="OfficeRegionKind.ExcelRange"/> 区域</summary>
    public static void Fill(
        IWorkbook workbook, OfficeFillRequest request, OfficeFillReport report, StyleCache styleCache)
    {
        if (request.Regions.Count == 0) return;

        foreach (var region in request.Regions)
            if (region.Kind == OfficeRegionKind.ExcelRange)
                FillOne(workbook, region, report, styleCache);
    }

    private static void FillOne(
        IWorkbook workbook, OfficeFillRegion region, OfficeFillReport report, StyleCache styleCache)
    {
        var hit = new OfficeFillRegionHit { Location = region.Describe() };

        ISheet? sheet;
        try
        {
            sheet = string.IsNullOrWhiteSpace(region.SheetName)
                ? workbook.GetSheetAt(0)
                : workbook.GetSheet(region.SheetName);
        }
        catch (Exception ex)
        {
            hit.Matched = false;
            hit.Message = $"打开工作表失败：{ex.Message}";
            report.Regions.Add(hit);
            return;
        }

        if (sheet == null)
        {
            hit.Matched = false;
            hit.Message = $"找不到工作表「{region.SheetName}」";
            report.Regions.Add(hit);
            return;
        }

        hit.Matched = true;

        for (var i = 0; i < region.Rows.Count; i++)
        {
            var rowIndex = region.StartRow + i;
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);

            var data = region.Rows[i] ?? new List<FillValue?>();

            for (var j = 0; j < data.Count; j++)
            {
                var colIndex = region.StartCol + j;
                var cell = row.GetCell(colIndex);

                if (cell == null)
                {
                    cell = row.CreateCell(colIndex);

                    // ★ 新格：从上一行同列「沿用」样式 —— 这是复制模板已有样式，
                    //   不是程序自造样式（后者是本轮明令排除的）
                    var above = sheet.GetRow(rowIndex - 1)?.GetCell(colIndex);
                    if (above?.CellStyle != null) cell.CellStyle = above.CellStyle;
                }

                ExcelCellWriter.Write(cell, data[j], styleCache);
            }

            hit.RowCount++;
        }

        report.Regions.Add(hit);
    }
}
