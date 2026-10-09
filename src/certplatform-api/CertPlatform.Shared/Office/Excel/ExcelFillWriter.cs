using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace CertPlatform.Shared.Office.Excel;

/// <summary>
/// Excel（.xlsx）填充写入器 —— 层 1 的对外门面（对应 26 号 §3.4.6 的 <c>doc_cell_write</c> /
/// <c>doc_table_write</c> 的 Excel 侧）。
///
/// <para><b>★ 三条 Excel 专属陷阱（全部在本类内消化）</b>：</para>
/// <list type="number">
///   <item><b>类型必须显式</b>：<c>SetCellValue(string)</c> 把数字写成文本 ⇒ 不能求和、不能排序，
///         且打开文件看不出来。⇒ 由 <see cref="FillValue.Kind"/> 决定重载，⛔ 不做「按值猜类型」。</item>
///   <item><b>数字/日期必须显式 <c>DataFormat</c></b>：否则 <c>2026-10-02</c> 显示成 <c>46235</c>。</item>
///   <item><b><c>cell.CellStyle</c> 必须保留</b>：直接 <c>cell.CellStyle = wb.CreateCellStyle()</c>
///         会丢掉原样式（边框/底纹/对齐），症状是「填完值表格框线没了」。
///         ⇒ 本类用 <c>CloneStyleFrom</c> 复制原样式再改 <c>DataFormat</c>，并按「原样式索引+格式串」缓存，避免撞 Excel 的 64k 样式上限。</item>
/// </list>
///
/// <para>⛔ <b>不处理</b>：合并区内的非左上角单元格（Excel 规则：合并区只有左上角可写）。
/// 若锚点落在合并区非左上角，写入**会被 Excel 忽略** —— 这是模板标注问题，不是写入问题。</para>
/// </summary>
public sealed class ExcelFillWriter
{
    /// <summary>执行一次填充</summary>
    public OfficeFillResult Fill(OfficeFillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Template is null || request.Template.Length == 0)
            throw new ArgumentException("模板字节为空：请先确认上传的是有效的 .xlsx 文件", nameof(request));

        var report = new OfficeFillReport { Kind = "excel" };

        using var input = new MemoryStream(request.Template, writable: false);
        var workbook = new XSSFWorkbook(input);
        var styleCache = new StyleCache(workbook);

        // ── ① 区域填充（起始行列 + 二维数据）★ 必须先于锚点扫描：区域写的数据可能覆盖锚点格 ──
        ExcelRegionFiller.Fill(workbook, request, report, styleCache);

        // ── ② 锚点替换 ──
        for (var si = 0; si < workbook.NumberOfSheets; si++)
            FillSheet(workbook.GetSheetAt(si), request, report, styleCache);

        // ★ 自验收：独立遍历（Excel 无字符样式概念，故不做 mark 检查）
        report.LeftoverTokens = ExcelSheetScanner.Scan(workbook);
        report.LeftoverMarkRuns = 0;

        using var output = new MemoryStream();
        workbook.Write(output, leaveOpen: false);

        return new OfficeFillResult { Output = output.ToArray(), Report = report };
    }

    private static void FillSheet(
        ISheet sheet, OfficeFillRequest request, OfficeFillReport report, StyleCache styleCache)
    {
        if (sheet == null) return;

        var first = sheet.FirstRowNum;
        var last = sheet.LastRowNum;
        if (last < first) return;

        for (var ri = first; ri <= last; ri++)
        {
            var row = sheet.GetRow(ri);
            if (row == null) continue;

            var firstCell = row.FirstCellNum;
            var lastCell = row.LastCellNum;   // 排他
            if (lastCell <= firstCell) continue;

            for (var ci = firstCell; ci < lastCell; ci++)
            {
                var cell = row.GetCell(ci);
                if (cell == null) continue;

                // 只处理文本单元格；数字/公式/空白单元格里不可能有 {{ }} 锚点
                if (cell.CellType != CellType.String) continue;

                var text = cell.StringCellValue;
                if (string.IsNullOrEmpty(text) || text.IndexOf("{{", StringComparison.Ordinal) < 0) continue;

                var location = $"{sheet.SheetName}!{CellRef(ri, ci)}";
                FillCell(cell, text, location, request, report, styleCache);
            }
        }
    }

    private static void FillCell(
        ICell cell, string text, string location,
        OfficeFillRequest request, OfficeFillReport report, StyleCache styleCache)
    {
        var matches = Regex.Matches(text, FillSyntax.TokenPattern, RegexOptions.CultureInvariant);
        if (matches.Count == 0) return;

        // ── 特例：整格就是一个锚点 ⇒ 按值类型写入（保住数字/日期语义 + 锚点里的 format）──
        if (matches.Count == 1 && string.Equals(matches[0].Value, text.Trim(), StringComparison.Ordinal))
        {
            var (soloKey, soloFormat) = FillSyntax.SplitFormat(matches[0].Groups[1].Value);

            if (request.Values.TryGetValue(soloKey, out var soloValue))
            {
                // ★ 格式优先级：层 2 显式给的 > 模板锚点里写的（{{amount:#,##0.00}}）> 单元格原格式。
                //   模板作者在锚点里写 format，是「这个格子该显示成什么样」的**显式声明**，
                //   所以它比"原格恰好是什么格式"更权威 —— 程序只搬运，⛔ 不推断。
                var effective = soloFormat != null && string.IsNullOrWhiteSpace(soloValue.NumberFormat)
                    ? WithFormat(soloValue, soloFormat)
                    : soloValue;

                if (effective.Kind != FillValueKind.Text || soloFormat != null)
                {
                    ExcelCellWriter.Write(cell, effective, styleCache);
                    report.Hits.Add(new OfficeFillHit
                    {
                        Token = matches[0].Value, AnchorCode = soloKey,
                        LocationKind = FillLocationKind.ExcelCell, Location = location,
                        Value = effective.ToDisplayText(), CrossRun = false, Source = effective.Source,
                    });
                    return;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★ 覆盖（`overwrite`）—— 整格换成取值，格里的其它文字一并被替换
        //
        //  用户 2026-10-09 规格原话：
        //    「我们很多时候，一个单元格并不是一个字段，一般是**一句话，中间有 {{}}**，
        //      我们如果用**覆盖**，就将 cell 全部填充成新的内容了，只能替换当前单元格的 {{}}」
        //
        //  ⚠️ 仅在「本格恰好 1 个锚点」时成立 —— 多个锚点时「整格该换成哪一个的值」
        //     语义模糊 ⇒ **退回填充**并记一条待办（⛔ 不猜、⛔ 不静默）。
        // ════════════════════════════════════════════════════════════════
        if (matches.Count == 1)
        {
            var (owKey, _) = FillSyntax.SplitFormat(matches[0].Groups[1].Value);
            if (request.Values.TryGetValue(owKey, out var owValue) && owValue.IsOverwrite())
            {
                ExcelCellWriter.Write(cell, owValue, styleCache);
                report.Hits.Add(new OfficeFillHit
                {
                    Token = matches[0].Value, AnchorCode = owKey,
                    LocationKind = FillLocationKind.ExcelCell, Location = location,
                    Value = owValue.ToDisplayText(), CrossRun = false, Source = owValue.Source,
                });
                return;
            }
        }
        else
        {
            // 多锚点 + 有人要求覆盖 ⇒ 语义模糊，如实记一条待办（本格仍按「填充」处理）
            var owKey = matches
                .Select(m => FillSyntax.SplitFormat(m.Groups[1].Value).Key)
                .FirstOrDefault(k => request.Values.TryGetValue(k, out var v) && v.IsOverwrite());

            if (owKey != null)
            {
                report.Pendings.Add(new OfficeFillPending
                {
                    Token = matches[0].Value, AnchorCode = owKey,
                    LocationKind = FillLocationKind.ExcelCell, Location = location,
                    Reason = $"写入方式配了「覆盖」（整格替换），但本格有 {matches.Count} 个锚点 —— "
                           + "整格该换成哪一个的值无法确定，已按「填充」处理（只替换 {{}}）",
                });
            }
        }

        // ── 一般情况：格内片段替换（从后往前，避免索引位移）──
        //    ⚠️ 片段替换里锚点的 format 无意义（一格混着文本与值，格式是格级的），只取键。
        var result = text;
        for (var mi = matches.Count - 1; mi >= 0; mi--)
        {
            var match = matches[mi];
            var (key, _) = FillSyntax.SplitFormat(match.Groups[1].Value);

            if (!request.Values.TryGetValue(key, out var value))
            {
                report.Pendings.Add(new OfficeFillPending
                {
                    Token = match.Value, AnchorCode = key,
                    LocationKind = FillLocationKind.ExcelCell, Location = location,
                    Reason = "未提供值",
                });

                // ★ 未命中 ⇒ 默认置空（用户规格：「如果没有值则自动将填写内容赋值为空」）
                if (!request.KeepUnresolvedAsIs)
                    result = result[..match.Index] + result[(match.Index + match.Length)..];

                continue;
            }

            var valueText = value.ToDisplayText();
            result = result[..match.Index] + valueText + result[(match.Index + match.Length)..];

            report.Hits.Add(new OfficeFillHit
            {
                Token = match.Value, AnchorCode = key,
                LocationKind = FillLocationKind.ExcelCell, Location = location,
                Value = valueText.Length <= 120 ? valueText : valueText[..120] + "…",
                CrossRun = false, Source = value.Source,
            });
        }

        // ★ 只改值，不碰 cell.CellStyle ⇒ 单元格样式（边框/底纹/对齐/字体）全部保留
        cell.SetCellValue(result);
    }

    /// <summary>复制一个 <see cref="FillValue"/> 并覆盖其 <c>NumberFormat</c>（⛔ 不改调用方对象）</summary>
    private static FillValue WithFormat(FillValue v, string format) => new()
    {
        AnchorCode = v.AnchorCode,
        Kind = v.Kind,
        Text = v.Text,
        Number = v.Number,
        Date = v.Date,
        Bool = v.Bool,
        NumberFormat = format,
        FieldInstruction = v.FieldInstruction,
        Confidence = v.Confidence,
        Source = v.Source,
    };

    /// <summary>Excel 列号 → 字母（0 → A，26 → AA）</summary>
    internal static string CellRef(int rowIndexZeroBased, int colIndexZeroBased)
    {
        var col = colIndexZeroBased + 1;
        var name = string.Empty;
        while (col > 0)
        {
            var rem = (col - 1) % 26;
            name = (char)('A' + rem) + name;
            col = (col - 1) / 26;
        }
        return name + (rowIndexZeroBased + 1).ToString(CultureInfo.InvariantCulture);
    }
}
