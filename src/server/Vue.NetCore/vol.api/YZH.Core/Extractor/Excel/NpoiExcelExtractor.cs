using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace YZH.Core.Extractor.Excel;

/// <summary>
/// Excel 提取器（基于 NPOI，xls / xlsx 统一支持）。
/// <para>使用 WorkbookFactory.Create 自动识别 HSSF(xls) / XSSF(xlsx)，按工作表逐行逐单元格提取，
/// 每张工作表输出一个 ExtractedTable（行列结构），FullText 为每行单元格以 Tab 连接的扁平文本。</para>
/// <para>状态：[DONE] xls/xlsx 统一逐表逐行逐格提取、隐藏表过滤、行数上限；[TODO:P2] 合并单元格语义、公式缓存值策略细化。</para>
/// </summary>
public class NpoiExcelExtractor : ITextExtractor
{
    public async Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var result = await ExtractAsync(fs, Path.GetFileName(filePath), options, ct);
        result.FilePath = filePath;
        return result;
    }

    public async Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        var opts = options ?? new ExtractionOptions();
        var result = FileExtractionResult.CreateBase(fileName);
        result.SourceType = ExtractSourceType.Excel;
        var sw = Stopwatch.StartNew();

        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var workbook = WorkbookFactory.Create(stream);
            var formatter = new DataFormatter();
            var tableIndex = 1;
            var textLines = new List<string>();

            for (var s = 0; s < workbook.NumberOfSheets; s++)
            {
                ct.ThrowIfCancellationRequested();

                var sheet = workbook.GetSheetAt(s);
                if (!opts.IncludeHiddenSheets && workbook.IsSheetHidden(s))
                    continue;

                var rows = new List<List<string>>();
                var maxRow = Math.Min(sheet.LastRowNum, opts.MaxRowsPerSheet > 0 ? opts.MaxRowsPerSheet - 1 : int.MaxValue);
                if (maxRow < 0) maxRow = 0;

                var sheetSectionIndex = 0;

                for (var r = sheet.FirstRowNum; r <= maxRow; r++)
                {
                    var row = sheet.GetRow(r);
                    if (row == null) continue;

                    var cells = new List<string>();
                    var lastCell = Math.Min(row.LastCellNum, row.LastCellNum > 0 ? row.LastCellNum : 0);
                    for (var c = row.FirstCellNum; c < lastCell; c++)
                    {
                        var cell = row.GetCell(c);
                        cells.Add(cell == null ? string.Empty : formatter.FormatCellValue(cell));
                    }

                    rows.Add(cells);

                    var lineText = string.Join("\t", cells);
                    textLines.Add(lineText);

                    // 每行作为独立段落（line 类型），供 LLM 精准定位
                    sheetSectionIndex++;
                    result.Sections.Add(new TextSection
                    {
                        Content = lineText,
                        SectionIndex = sheetSectionIndex,
                        SectionType = "line",
                        SheetName = sheet.SheetName,
                        PositionInfo = System.Text.Json.JsonSerializer.Serialize(new { sheet = sheet.SheetName, row = r + 1 })
                    });
                }

                result.Tables.Add(new ExtractedTable
                {
                    TableIndex = tableIndex++,
                    SheetName = sheet.SheetName,
                    Rows = rows,
                    PositionInfo = $"{{\"sheet\":\"{sheet.SheetName}\",\"row_count\":{rows.Count}}}",
                    Confidence = 1.0m
                });

                // 整表也作为一个 section（table 类型），保留整体结构感
                result.Sections.Add(new TextSection
                {
                    Content = string.Join("\n", textLines),
                    SectionIndex = ++sheetSectionIndex,
                    SectionType = "table",
                    SheetName = sheet.SheetName,
                    PositionInfo = $"{{\"sheet\":\"{sheet.SheetName}\",\"row_count\":{rows.Count}}}"
                });
            }

            if (opts.ExtractFullText)
                result.FullText = string.Join(Environment.NewLine, textLines);

            result.SourceInfo.DetectedType = ExtractSourceType.Excel;
            result.SourceInfo.StructureCount = workbook.NumberOfSheets;
            result.Status = ExtractStatus.Success;
        }
        catch (OperationCanceledException)
        {
            result.Status = ExtractStatus.Cancelled;
            result.Message = "提取已取消";
        }
        catch (Exception ex)
        {
            result.Status = ExtractStatus.Failed;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
        }

        return result;
    }
}
