using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Extractor.Text;

/// <summary>
/// 纯文本提取器（txt / csv / log / md 等无魔法头的文本文件兜底）。
/// <para>自动识别 UTF-8 BOM / UTF-16 BOM；无 BOM 按 UTF-8 读取。逐行提取全文，StructureCount = 行数。</para>
/// <para>状态：[DONE] 基本逻辑；[TODO:P2] 按扩展名切换默认编码（GBK 等非 UTF-8 文本）。</para>
/// </summary>
public class PlainTextExtractor : ITextExtractor
{
    public async Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        var result = await ExtractCoreAsync(filePath, Path.GetFileName(filePath), options, ct);
        result.FilePath = filePath;
        return result;
    }

    public Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        return ExtractCoreAsync(stream, fileName, options, ct);
    }

    private async Task<FileExtractionResult> ExtractCoreAsync(string filePath, string fileName, ExtractionOptions? options, CancellationToken ct)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await ExtractCoreAsync(fs, fileName, options, ct);
    }

    private async Task<FileExtractionResult> ExtractCoreAsync(Stream stream, string fileName, ExtractionOptions? options, CancellationToken ct)
    {
        var opts = options ?? new ExtractionOptions();
        var result = FileExtractionResult.CreateBase(fileName);
        result.SourceType = ExtractSourceType.Text;
        var sw = Stopwatch.StartNew();

        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var encoding = DetectEncoding(stream);
            using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);

            var text = await reader.ReadToEndAsync(ct);
            var lineCount = text.Split('\n').Length;

            if (opts.ExtractFullText)
            {
                result.FullText = text;
            }

            result.SourceInfo.DetectedType = ExtractSourceType.Text;
            result.SourceInfo.StructureCount = lineCount;
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
    /// BOM 探测：UTF-8 / UTF-16LE / UTF-16BE，无 BOM 默认 UTF-8。
    /// </summary>
    private static Encoding DetectEncoding(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return Encoding.UTF8;
        }

        var original = stream.Position;
        stream.Position = 0;
        var bom = new byte[3];
        var read = stream.Read(bom, 0, bom.Length);
        stream.Position = original;

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        return Encoding.UTF8;
    }
}
