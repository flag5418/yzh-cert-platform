using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;
using NPOI.XWPF.UserModel;

namespace YZH.Core.Extractor.Word;

/// <summary>
/// Word 提取器（基于 NPOI）。
/// <para>实现说明：
/// - .docx（OOXML Zip）走 XWPF：正文段落 + 表格，按文档顺序拼接 FullText，表格写入 Tables（含行列结构）。[DONE]
/// - .doc（OLE2 旧格式）暂不支持 [TODO:P2]：NPOI NuGet 官方包（2.3.0~2.7.2）均未打包 HWPF 程序集，
///   无法用 NPOI 读取 .doc。检测到 .doc 时返回 Unsupported + 明确提示（建议用户转存 .docx，或后续引入
///   支持 .doc 的第三方库如 Spire.Doc 免费版 / Aspose.Words 商业授权，见 docs 文档 §十 已知限制）。</para>
/// <para>状态：[DONE] docx(XWPF) 段落+表格；[TODO:P2] doc(HWPF) 支持（受 NPOI 无 HWPF 程序集约束）。</para>
/// </summary>
public class NpoiWordExtractor : ITextExtractor
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
        result.SourceType = ExtractSourceType.Word;
        var sw = Stopwatch.StartNew();

        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var isLegacyDoc = FileTypeDetector.Detect(stream, fileName) == ExtractSourceType.Word
                              && HasOle2Header(stream);

            if (isLegacyDoc)
            {
                // [TODO:P2] NPOI 无 HWPF：.doc 暂不支持，返回明确提示
                result.Status = ExtractStatus.Unsupported;
                result.Message = "Word 旧格式 .doc 暂不支持提取（NPOI NuGet 包不含 HWPF），请转存为 .docx 后重新上传";
                result.SourceInfo.DetectedType = ExtractSourceType.Word;
                return result;
            }

            ExtractOpenXml(stream, result, opts);

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

    /// <summary>
    /// .docx（OOXML）提取：XWPF 按文档顺序遍历段落与表格。
    /// </summary>
    private void ExtractOpenXml(Stream stream, FileExtractionResult result, ExtractionOptions opts)
    {
        using var doc = new XWPFDocument(stream);
        var tableIndex = 1;
        var sectionIndex = 0;

        foreach (var element in doc.BodyElements)
        {
            switch (element)
            {
                case XWPFParagraph paragraph:
                    var text = paragraph.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    sectionIndex++;
                    var section = new TextSection
                    {
                        Content = text,
                        SectionIndex = sectionIndex,
                        SectionType = "paragraph",
                        PositionInfo = System.Text.Json.JsonSerializer.Serialize(new { line_start = sectionIndex })
                    };
                    result.Sections.Add(section);

                    if (opts.ExtractFullText)
                        result.FullText += (string.IsNullOrEmpty(result.FullText) ? "" : "\n") + text;
                    break;

                case XWPFTable table:
                    var rows = new List<List<string>>();
                    foreach (var row in table.Rows)
                    {
                        var cells = new List<string>();
                        foreach (var cell in row.GetTableCells())
                            cells.Add(cell.GetText() ?? string.Empty);
                        rows.Add(cells);
                    }

                    var t = new ExtractedTable
                    {
                        TableIndex = tableIndex++,
                        Rows = rows,
                        PositionInfo = $"{{\"table\":{tableIndex - 1}}}",
                        Confidence = 1.0m
                    };
                    result.Tables.Add(t);

                    sectionIndex++;
                    var tableSection = new TextSection
                    {
                        Content = string.Join("\n", rows.Select(r => string.Join("\t", r))),
                        SectionIndex = sectionIndex,
                        SectionType = "table",
                        PositionInfo = $"{{\"table\":{tableIndex - 1}}}"
                    };
                    result.Sections.Add(tableSection);

                    if (opts.ExtractFullText)
                        result.FullText += (string.IsNullOrEmpty(result.FullText) ? "" : "\n")
                            + "[表格] " + string.Join(" | ", rows.Select(r => string.Join("\t", r)));
                    break;
            }
        }

        result.SourceInfo.DetectedType = ExtractSourceType.Word;
        result.SourceInfo.StructureCount = result.Sections.Count;
    }

    /// <summary>
    /// 判断流是否为 OLE2（doc）而非 OOXML（docx）。
    /// </summary>
    private static bool HasOle2Header(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return false;
        }

        var original = stream.Position;
        stream.Position = 0;
        var header = new byte[8];
        var read = stream.Read(header, 0, header.Length);
        stream.Position = original;

        return read >= 8 && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0;
    }
}
