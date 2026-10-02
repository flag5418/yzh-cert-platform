using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace CertPlatform.Shared.Office.Excel;

/// <summary>
/// Excel 全工作簿扫描 —— 只做一件事：<b>找残留锚点</b>（自验收）。
/// <para>与 <c>WordDocumentScanner</c> 同理：**独立遍历**，不复用写入路径 ——
/// 两者结果不一致就说明写入器漏了某个 sheet / 某行。</para>
/// </summary>
internal static class ExcelSheetScanner
{
    internal static List<string> Scan(XSSFWorkbook workbook)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var si = 0; si < workbook.NumberOfSheets; si++)
        {
            var sheet = workbook.GetSheetAt(si);
            if (sheet == null) continue;

            var first = sheet.FirstRowNum;
            var last = sheet.LastRowNum;
            if (last < first) continue;

            for (var ri = first; ri <= last; ri++)
            {
                var row = sheet.GetRow(ri);
                if (row == null) continue;

                var firstCell = row.FirstCellNum;
                var lastCell = row.LastCellNum;
                if (lastCell <= firstCell) continue;

                for (var ci = firstCell; ci < lastCell; ci++)
                {
                    var cell = row.GetCell(ci);
                    if (cell == null || cell.CellType != CellType.String) continue;

                    var text = cell.StringCellValue;
                    if (string.IsNullOrEmpty(text) || text.IndexOf("{{", StringComparison.Ordinal) < 0) continue;

                    foreach (Match m in Regex.Matches(text, FillSyntax.TokenPattern, RegexOptions.CultureInvariant))
                        if (seen.Add(m.Value)) tokens.Add(m.Value);
                }
            }
        }

        return tokens;
    }
}
