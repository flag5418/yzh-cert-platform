namespace YZH.Core.Extractor.Models;

/// <summary>
/// 字段级提取结果。
/// <para>对齐 B-08 ExtractionResult 实体语义：一个字段一条记录，含提取值 + 置信度 + 位置信息。</para>
/// </summary>
public class ExtractedField
{
    /// <summary>字段名（对应 A-09 ExtractionField 的字段名 / 工作流输入端口）</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>字段值（对齐 B-08 ExtractedValue）</summary>
    public string? FieldValue { get; set; }

    /// <summary>字段标签（对齐 F-02 label_tag，工作流 get_field 节点的引用键）</summary>
    public string? LabelTag { get; set; }

    /// <summary>置信度 0.00-1.00（对齐 B-08 Confidence；文本层提取通常 1.0，AI/OCR 按模型返回）</summary>
    public decimal? Confidence { get; set; }

    /// <summary>来源位置 JSON（对齐 B-08 PositionInfo）：PDF/Word 用 {"page":n,"line_start":n,"line_end":n}；Excel 用 {"sheet":"名","row":n,"col":n}</summary>
    public string? PositionInfo { get; set; }

    /// <summary>原始片段（来源文本的最小上下文，供复核展示）</summary>
    public string? RawSnippet { get; set; }

    /// <summary>执行提取的组件名（如 NpoiWordExtractor / [TODO] OcrExtractor）</summary>
    public string ExtractorName { get; set; } = string.Empty;

    /// <summary>是否来自 OCR 链路（true 表示 AI/OCR 结果，需复核）</summary>
    public bool IsFromOcr { get; set; }
}
