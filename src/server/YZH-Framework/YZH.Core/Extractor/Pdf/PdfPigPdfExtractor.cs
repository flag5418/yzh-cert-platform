using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;
using UglyToad.PdfPig;

namespace YZH.Core.Extractor.Pdf;

/// <summary>
/// PDF 提取器（基于 PdfPig）。
/// <para>本阶段策略：先做文本层探测——有文本层则逐页提取文字；无文本层（扫描件）返回 OcrRequired 标记与页面数量，
/// 由上层调度接入第三方 OCR 链路（见 IOcrExtractor）。</para>
/// <para>已知限制：PdfPig 对部分中文字体（缺少 ToUnicode CMap 或 CID 映射缺失）可能提取乱码，
/// 需在验证阶段用真实中文 PDF 回归（见 docs 文档 §十 已知限制）。</para>
/// <para>状态：[DONE] 文本层探测 + 逐页文字提取 + 需 OCR 标记；[TODO:P1] 中文 ToUnicode 问题回归与字库回退策略。</para>
/// </summary>
public class PdfPigPdfExtractor : ITextExtractor
{
    public async Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        var result = await ExtractCoreAsync(() => PdfDocument.Open(filePath), Path.GetFileName(filePath), options, ct);
        result.FilePath = filePath;
        return result;
    }

    public async Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return await ExtractCoreAsync(() => PdfDocument.Open(stream), fileName, options, ct);
    }

    private async Task<FileExtractionResult> ExtractCoreAsync(Func<PdfDocument> opener, string fileName, ExtractionOptions? options, CancellationToken ct)
    {
        var opts = options ?? new ExtractionOptions();
        var result = FileExtractionResult.CreateBase(fileName);
        result.SourceType = ExtractSourceType.Pdf;
        var sw = Stopwatch.StartNew();

        try
        {
            using var document = opener();
            var pageCount = 0;
            var hasTextLayer = false;
            var textBuilder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                pageCount++;

                if (opts.MaxPdfPageCount > 0 && pageCount > opts.MaxPdfPageCount)
                {
                    break;
                }

                // 文本层探测：Letters 存在即认为该页有文本层
                var letterCount = page.Letters.Count();
                if (letterCount > 0)
                {
                    hasTextLayer = true;
                    if (opts.ExtractFullText)
                    {
                        textBuilder.AppendLine($"===== 第 {page.Number} 页 =====");
                        textBuilder.AppendLine(page.Text ?? string.Empty);
                    }
                }
            }

            result.SourceInfo.StructureCount = pageCount;
            result.SourceInfo.HasTextLayer = hasTextLayer;
            result.SourceInfo.DetectedType = ExtractSourceType.Pdf;

            if (hasTextLayer)
            {
                if (opts.ExtractFullText)
                {
                    result.FullText = textBuilder.ToString();
                }

                result.Status = ExtractStatus.Success;
            }
            else
            {
                // 无文本层：返回需 OCR 标记与页面数量，为第三方 OCR 链路预留
                result.Status = ExtractStatus.OcrRequired;
                result.SourceInfo.OcrRequired = true;
                result.Message = $"PDF 无文本层（扫描件），共 {pageCount} 页，需走 OCR 链路（见 IOcrExtractor）";
            }
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
