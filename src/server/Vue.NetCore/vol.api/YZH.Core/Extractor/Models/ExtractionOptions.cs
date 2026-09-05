namespace YZH.Core.Extractor.Models;

/// <summary>
/// 提取选项。全部可空字段均有合理默认值，调用方按需覆盖。
/// </summary>
public class ExtractionOptions
{
    /// <summary>是否提取表格内容（Word 表格 / Excel 工作表）。默认 true。</summary>
    public bool ExtractTables { get; set; } = true;

    /// <summary>是否拼接全文文本。默认 true。</summary>
    public bool ExtractFullText { get; set; } = true;

    /// <summary>PDF 最多提取页数，0 表示不限。默认 0。</summary>
    public int MaxPdfPageCount { get; set; } = 0;

    /// <summary>每个 Excel 工作表最多提取行数，0 表示不限。默认 0。</summary>
    public int MaxRowsPerSheet { get; set; } = 0;

    /// <summary>是否包含 Excel 隐藏工作表。默认 false（跳过隐藏表）。</summary>
    public bool IncludeHiddenSheets { get; set; } = false;

    /// <summary>单文件超时秒数，null 表示不限。默认 null [TODO:P2 异步队列接入后启用]。</summary>
    public int? TimeoutSeconds { get; set; }
}
