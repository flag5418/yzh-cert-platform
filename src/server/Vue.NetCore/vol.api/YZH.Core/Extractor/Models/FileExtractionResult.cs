using System;
using System.Collections.Generic;

namespace YZH.Core.Extractor.Models;

/// <summary>
/// 统一提取结果模型（所有提取器的出口契约）。
/// <para>设计对齐：字段级 → B-08 ExtractionResult；表格级 → B-09 TableExtractionResult；
/// 上层工作流 Skill（get_field / get_table）直接消费本模型，无需感知底层解析库差异。</para>
/// </summary>
public class FileExtractionResult
{
    /// <summary>源文件绝对路径（字节流提取时为 null）</summary>
    public string? FilePath { get; set; }

    /// <summary>源文件名（用于类型识别与展示）</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>识别的文件类型（magic bytes 判定）</summary>
    public ExtractSourceType SourceType { get; set; }

    /// <summary>提取状态</summary>
    public ExtractStatus Status { get; set; } = ExtractStatus.None;

    /// <summary>全文拼接文本（Word 段落 + 表格按顺序、PDF 逐页、Excel 逐行 join；选项关闭时为空）</summary>
    public string? FullText { get; set; }

    /// <summary>
    /// 结构化文本段落列表（含页码/行号/位置 JSON）。
    /// <para>比 FullText 更丰富：每段有独立位置信息，LLM 可精准定位。</para>
    /// </summary>
    public List<TextSection> Sections { get; set; } = new();

    /// <summary>字段级提取结果列表</summary>
    public List<ExtractedField> Fields { get; set; } = new();

    /// <summary>表格级提取结果列表</summary>
    public List<ExtractedTable> Tables { get; set; } = new();

    /// <summary>源探测信息（页数 / 文本层 / OCR 标记）</summary>
    public ExtractionSourceInfo SourceInfo { get; set; } = new();

    /// <summary>失败 / 需 OCR 时的说明（供上层记录与提示）</summary>
    public string? Message { get; set; }

    /// <summary>异常信息（仅 Failed 时填充）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>本次提取耗时（毫秒）</summary>
    public long DurationMs { get; set; }

    /// <summary>提取完成时间</summary>
    public DateTime ExtractedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 快速构造：创建仅含基础信息的空结果（供各提取器填充）。
    /// </summary>
    public static FileExtractionResult CreateBase(string fileName, string? filePath = null)
    {
        return new FileExtractionResult
        {
            FileName = fileName,
            FilePath = filePath,
            ExtractedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 快速构造：无文本层需 OCR 的结果（图片 / 扫描 PDF 入口使用）。
    /// </summary>
    public static FileExtractionResult CreateOcrRequired(string fileName, string? filePath, string message)
    {
        var result = CreateBase(fileName, filePath);
        result.Status = ExtractStatus.OcrRequired;
        result.SourceInfo.HasTextLayer = false;
        result.SourceInfo.OcrRequired = true;
        result.Message = message;
        return result;
    }

    /// <summary>
    /// 快速构造：不支持的文件类型。
    /// </summary>
    public static FileExtractionResult CreateUnsupported(string fileName, string? filePath, string message)
    {
        var result = CreateBase(fileName, filePath);
        result.Status = ExtractStatus.Unsupported;
        result.Message = message;
        return result;
    }
}
