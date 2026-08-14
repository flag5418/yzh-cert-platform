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

            // NPOI CT_Jc 不认识 OOXML 的 start/end 对齐值（LibreOffice 转换产物常见），
            // 解析 document.xml 时会抛 "Requested value 'start' was not found"，先做兼容替换。
            stream = SanitizeAlignmentValues(stream);

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
    /// .docx（OOXML）提取：页眉内容优先，随后 XWPF 正文按文档顺序遍历段落与表格。
    /// <para>页眉处理：认证体系文档的封面表格（如“文件编号/版本/生效日期”）常位于 Word
    /// 页眉（header*.xml）而非正文，若只读 BodyElements 会导致这些关键信息丢失，
    /// AI 分析自然无法提取。页眉按文本去重（同一页眉被多个节引用时 HeaderList 会有多份）。
    /// 页脚（页码等）不提取，避免污染内容。</para>
    /// </summary>
    private void ExtractOpenXml(Stream stream, FileExtractionResult result, ExtractionOptions opts)
    {
        using var doc = new XWPFDocument(stream);
        var tableIndex = 1;
        var sectionIndex = 0;

        // 页眉（封面表格通常在这里）优先提取，按页眉文本去重
        // （同一页眉被多个节引用时 HeaderList 会出现多份；空页眉直接跳过）
        var seenHeaderTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in doc.HeaderList ?? new List<XWPFHeader>())
        {
            var headerText = header?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(headerText) || !seenHeaderTexts.Add(headerText))
                continue;

            foreach (var element in header.BodyElements ?? new List<IBodyElement>())
                AppendBodyElement(element, result, opts, ref tableIndex, ref sectionIndex);
        }

        // 正文
        foreach (var element in doc.BodyElements)
            AppendBodyElement(element, result, opts, ref tableIndex, ref sectionIndex);

        result.SourceInfo.DetectedType = ExtractSourceType.Word;
        result.SourceInfo.StructureCount = result.Sections.Count;
    }

    /// <summary>
    /// 将单个正文元素（段落/表格）追加到提取结果：同步维护 Sections（供 AI 结构化上下文）
    /// 与 Tables（保留行列结构）、FullText（拼接纯文本）。
    /// </summary>
    private static void AppendBodyElement(
        IBodyElement element,
        FileExtractionResult result,
        ExtractionOptions opts,
        ref int tableIndex,
        ref int sectionIndex)
    {
        switch (element)
        {
            case XWPFParagraph paragraph:
                var text = paragraph.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    return;

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

    /// <summary>
    /// 将 docx 包内 XML 的 start/end 对齐值替换为 NPOI 认识的 left/right，
    /// 兼容 LibreOffice 等产出的 docx（NPOI CT_Jc 仅支持 left/right/center 等）。
    /// <para>docx 是 zip 容器，须解压逐条目改写后再重新打包；仅对含目标值的条目做替换。</para>
    /// </summary>
    private static Stream SanitizeAlignmentValues(Stream stream)
    {
        if (!stream.CanRead)
        {
            return stream;
        }

        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var input = new MemoryStream();
            stream.CopyTo(input);
            input.Position = 0;

            var output = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(input, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
            using (var writer = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in archive.Entries)
                {
                    var newEntry = writer.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.Optimal);
                    using var src = entry.Open();
                    using var dst = newEntry.Open();
                    if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                        || entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    {
                        using var entryMs = new MemoryStream();
                        src.CopyTo(entryMs);
                        var data = entryMs.ToArray();
                        data = ReplaceAscii(data, "w:val=\"start\"", "w:val=\"left\"");
                        data = ReplaceAscii(data, "w:val=\"end\"", "w:val=\"right\"");
                        dst.Write(data, 0, data.Length);
                    }
                    else
                    {
                        src.CopyTo(dst);
                    }
                }
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            return stream;
        }
    }

    /// <summary>
    /// ASCII 字节序列替换（避免对二进制流做字符串解码）。
    /// </summary>
    private static byte[] ReplaceAscii(byte[] source, string oldStr, string newStr)
    {
        var oldBytes = System.Text.Encoding.ASCII.GetBytes(oldStr);
        var newBytes = System.Text.Encoding.ASCII.GetBytes(newStr);
        if (oldBytes.Length == 0)
        {
            return source;
        }

        var list = new List<byte>(source.Length + 32);
        var i = 0;
        while (i <= source.Length - oldBytes.Length)
        {
            var match = true;
            for (var j = 0; j < oldBytes.Length; j++)
            {
                if (source[i + j] != oldBytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                list.AddRange(newBytes);
                i += oldBytes.Length;
            }
            else
            {
                list.Add(source[i]);
                i++;
            }
        }

        while (i < source.Length)
        {
            list.Add(source[i]);
            i++;
        }

        return list.ToArray();
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
