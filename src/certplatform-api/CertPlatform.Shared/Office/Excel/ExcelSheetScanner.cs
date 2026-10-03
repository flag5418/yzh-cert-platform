using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace CertPlatform.Shared.Office.Excel;

/// <summary>
/// Excel 全工作簿扫描 —— 两个用途：
/// <list type="number">
/// <item><b>自验收</b>：找残留锚点（<see cref="Scan"/>）。</item>
/// <item><b>模板锚点清单</b>：<see cref="ScanAnchors"/> —— 把模板里所有锚点扫出来，
/// 供「标准文档填写规则」页（37 号 §7.2）落 <c>cert_doc_template_anchor</c>。</item>
/// </list>
///
/// <para>与 <c>WordDocumentScanner</c> 同理：<b>独立遍历</b>，不复用写入路径 ——
/// 两者结果不一致就说明写入器漏了某个 sheet / 某行。
/// 两者共用一个类也是同一理由：37 号 Q-6 裁定「<b>扩展</b>已有扫描器，⛔ 不新写第二套解析」。</para>
///
/// <para><b>★ 零 LLM</b>（37 号 H-3）。</para>
/// </summary>
public static class ExcelSheetScanner
{
    /// <summary>
    /// <b>一条锚点</b>（模板锚点清单用）。
    /// </summary>
    /// <param name="AnchorRef">单元格坐标 <c>Sheet1!B3</c>（与 37 号 §7.2 的 <c>Sheet1!A11:F11</c> 同格式）</param>
    /// <param name="AnchorType">scalar / block / table / domain（由 token 内容判定）</param>
    /// <param name="AnchorKind">range（Excel 侧恒为区域锚点）</param>
    /// <param name="FieldCode">去前缀的键，与 <c>cert_doc_field_def.FieldCode</c> 对齐</param>
    /// <param name="SheetName">工作表名（落 <c>cert_doc_template_anchor.SheetName</c>，⛔ 不可为 NULL）</param>
    /// <param name="DomainKind">仅 domain 类型：text / auto</param>
    /// <param name="Context">单元格原文（人工辨认用，⛔ 不落库）</param>
    public sealed record ExcelAnchor(
        string AnchorRef,
        string AnchorType,
        string AnchorKind,
        string FieldCode,
        string SheetName,
        string? DomainKind,
        string? Context,
        int Sort);

    /// <summary>
    /// 扫描<b>全部锚点</b>（37 号 §7.2 的 Excel 侧，<b>零 LLM</b>）。
    ///
    /// <para><b>扫什么</b>：每个工作表里<b>文本单元格</b>内的 <c>{{Token}}</c>。</para>
    ///
    /// <para><b>⛔ 首版不扫「连续矩形数据区」</b>：37 号要求「<c>{{table:items}}</c> 标起始行 +
    /// 连续矩形推断行高」，但<b>当前没有任何真实空白模板可验证</b>（用户裁定「现在的文档不是我们
    /// 真正设置好的空白模板」）。在无样本时写一段靠启发式猜区域边界的代码，风险高于收益 ——
    /// 猜错会静默生成错误的表格锚点。等有真模板再补（37 号 §9 的 spike 建议）。
    /// 注意 <c>{{table:xxx}}</c> <b>本身</b>仍会被扫出并判定为 <c>table</c> 形态，只是不自动推断行范围。</para>
    ///
    /// <para><b>★ 去重口径 = <c>uk_tpl_anchor</c> 同口径</b>：<c>AnchorKind + AnchorRef</c>
    /// （<c>AnchorRef</c> 含工作表名与坐标 ⇒ 天然唯一）。</para>
    /// </summary>
    public static List<ExcelAnchor> ScanAnchors(XSSFWorkbook workbook)
    {
        var result = new List<ExcelAnchor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sort = 0;

        for (var si = 0; si < workbook.NumberOfSheets; si++)
        {
            var sheet = workbook.GetSheetAt(si);
            if (sheet == null) continue;

            var sheetName = sheet.SheetName ?? string.Empty;
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

                    var coord = new CellReference(ri, ci).FormatAsString();
                    var anchorRef = $"{sheetName}!{coord}";

                    foreach (Match m in Regex.Matches(text, FillSyntax.TokenPattern, RegexOptions.CultureInvariant))
                    {
                        var key = $"range|{anchorRef}|{m.Value}";
                        if (!seen.Add(key)) continue;

                        var inner = m.Value[2..^2].Trim();
                        var (anchorType, domainKind) = AnchorClassifier.Classify(inner);
                        result.Add(new ExcelAnchor(
                            AnchorRef: anchorRef,
                            AnchorType: anchorType,
                            AnchorKind: "range",
                            FieldCode: AnchorClassifier.FieldCodeOf(inner),
                            SheetName: sheetName,
                            DomainKind: domainKind,
                            Context: Truncate(text),
                            Sort: sort++));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 全工作簿扫描 —— 只做一件事：<b>找残留锚点</b>（自验收）。
    /// </summary>
    public static List<string> Scan(XSSFWorkbook workbook)
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

    /// <summary>单元格文本截断（人工辨认用，⛔ 不落库）</summary>
    private static string? Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length <= 60 ? t : t[..60] + "…";
    }
}
