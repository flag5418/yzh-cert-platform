using System;
using System.IO;
using System.IO.Compression;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Extractor;

/// <summary>
/// 文件类型识别器（不信任扩展名，优先按 magic bytes 判定）。
/// <para>识别策略：PDF(%PDF) → OOXML Zip(PK..) 查内部条目 → OLE2(D0CF11E0) 按扩展名分 doc/xls → 纯文本探测。</para>
/// <para>状态：[DONE] 基本识别逻辑；[TODO:P2] 图片 magic bytes 细化（JPEG/PNG 头已在预留枚举内）。</para>
/// </summary>
public static class FileTypeDetector
{
    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 };       // %PDF
    private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };       // PK\x03\x04
    private static readonly byte[] Ole2Magic = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // OLE2 复合文档

    /// <summary>
    /// 按文件路径识别类型。
    /// </summary>
    public static ExtractSourceType Detect(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return ExtractSourceType.Unknown;
        }

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Detect(fs, Path.GetFileName(filePath));
    }

    /// <summary>
    /// 按字节流识别类型（stream 会重置到 Position 0）。
    /// </summary>
    public static ExtractSourceType Detect(Stream stream, string fileName)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var header = new byte[8];
        var read = stream.Read(header, 0, header.Length);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        // %PDF
        if (read >= 4 && Matches(header, 0, PdfMagic))
        {
            return FinishDetect(stream, ExtractSourceType.Pdf);
        }

        // OOXML Zip：docx / xlsx / pptx 均以 PK\x03\x04 开头，须查内部条目区分
        if (read >= 4 && Matches(header, 0, ZipMagic))
        {
            return FinishDetect(stream, DetectZipInner(stream, fileName));
        }

        // OLE2 复合文档：doc / xls 同头，按扩展名区分（读取失败时由提取器回退）
        if (read >= 8 && Matches(header, 0, Ole2Magic))
        {
            return FinishDetect(stream, DetectOle2(fileName));
        }

        // 其余按文本探测（无控制字符的 UTF-8 视为纯文本）
        if (IsProbablyText(header, read))
        {
            return FinishDetect(stream, ExtractSourceType.Text);
        }

        return FinishDetect(stream, ExtractSourceType.Unknown);
    }

    /// <summary>
    /// 返回前将流复位到 Position 0，保证「Detect 不改变流位置」的调用方契约。
    /// <para>说明：DetectZipInner 内部使用 ZipArchive（leaveOpen:true），释放后流 Position 会停留在
    /// 尾部；若不复位，下游提取器（如 NPOI XWPF）将从错误位置解析，报 "EOF in header"。</para>
    /// </summary>
    private static ExtractSourceType FinishDetect(Stream stream, ExtractSourceType type)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return type;
    }

    /// <summary>
    /// 检查 Zip 包内是否有 word/document.xml（docx）或 xl/workbook.xml（xlsx）。
    /// </summary>
    private static ExtractSourceType DetectZipInner(Stream stream, string fileName)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                if (name.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractSourceType.Word;
                }

                if (name.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase))
                {
                    return ExtractSourceType.Excel;
                }
            }
        }
        catch (InvalidDataException)
        {
            // 不是合法 Zip，交给文本/Unknown 判定
        }

        // Zip 但无法识别内部结构：回退扩展名
        return DetectByExtensionFallback(fileName);
    }

    /// <summary>
    /// OLE2 复合文档：doc / xls 按扩展名区分。
    /// </summary>
    private static ExtractSourceType DetectOle2(string fileName)
    {
        var ext = GetExtension(fileName);
        return ext switch
        {
            ".doc" or ".docx" => ExtractSourceType.Word,
            ".xls" or ".xlsx" => ExtractSourceType.Excel,
            _ => ExtractSourceType.Unknown
        };
    }

    private static ExtractSourceType DetectByExtensionFallback(string fileName)
    {
        var ext = GetExtension(fileName);
        return ext switch
        {
            ".doc" or ".docx" => ExtractSourceType.Word,
            ".xls" or ".xlsx" => ExtractSourceType.Excel,
            ".pdf" => ExtractSourceType.Pdf,
            ".txt" or ".csv" or ".log" or ".md" or ".json" or ".xml" or ".yaml" or ".yml" or ".ini" or ".cfg" or ".conf" or ".sh" or ".ps1" or ".py" or ".cs" or ".js" or ".ts" or ".html" => ExtractSourceType.Text,
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".gif" or ".tif" or ".tiff" => ExtractSourceType.Image,
            _ => ExtractSourceType.Unknown
        };
    }

    /// <summary>
    /// 简单的文本探测：前 8 字节不包含常见二进制控制字符即视为文本。
    /// </summary>
    private static bool IsProbablyText(byte[] header, int read)
    {
        if (read == 0)
        {
            return false;
        }

        for (var i = 0; i < read; i++)
        {
            var b = header[i];
            if (b == 0x00 || (b < 0x09) || (b > 0x0D && b < 0x20))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Matches(byte[] source, int offset, byte[] magic)
    {
        for (var i = 0; i < magic.Length; i++)
        {
            if (offset + i >= source.Length || source[offset + i] != magic[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string GetExtension(string fileName)
    {
        return string.IsNullOrEmpty(fileName) ? string.Empty : Path.GetExtension(fileName).ToLowerInvariant();
    }
}
