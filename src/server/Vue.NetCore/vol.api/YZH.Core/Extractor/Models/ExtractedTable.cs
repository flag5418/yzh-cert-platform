namespace YZH.Core.Extractor.Models;

/// <summary>
/// 表格级提取结果。
/// <para>对齐 B-09 TableExtractionResult 实体语义：一个表格一条记录，内容以 JSON 存储。</para>
/// </summary>
public class ExtractedTable
{
    /// <summary>表格序号（从 1 开始，对齐 B-09 TableIndex）</summary>
    public int TableIndex { get; set; } = 1;

    /// <summary>工作表名（Excel 专用；Word/PDF 为 null）</summary>
    public string? SheetName { get; set; }

    /// <summary>所在页码（PDF/Word 专用；Excel 为 0）</summary>
    public int PageNumber { get; set; }

    /// <summary>二维表格数据：rows → cells（Excel 逐行逐单元格；Word 表格逐行逐格）</summary>
    public List<List<string>> Rows { get; set; } = new();

    /// <summary>位置信息 JSON（对齐 B-09 PositionInfo）</summary>
    public string? PositionInfo { get; set; }

    /// <summary>置信度 0.00-1.00（对齐 B-09 Confidence）</summary>
    public decimal? Confidence { get; set; }

    /// <summary>序列化后的 JSON 全文（对齐 B-09 ExtractedJson，落库前用 System.Text.Json 生成）</summary>
    public string? RawJson { get; set; }
}
