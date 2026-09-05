namespace YZH.Core.Extractor.Models;

/// <summary>
/// 提取源文件的探测信息（用于无文本层判定与结果定位）。
/// </summary>
public class ExtractionSourceInfo
{
    /// <summary>PDF 页数 / Word 段落数（近似）/ Excel 工作表数</summary>
    public int StructureCount { get; set; }

    /// <summary>PDF 是否有文本层（false 表示扫描件，需 OCR）</summary>
    public bool HasTextLayer { get; set; } = true;

    /// <summary>是否需要 OCR（= PDF 无文本层 或 图片类型）</summary>
    public bool OcrRequired { get; set; }

    /// <summary>识别的实际文件类型（magic bytes 判定结果）</summary>
    public ExtractSourceType DetectedType { get; set; }

    /// <summary>探测备注（如 OLE2 无法区分 doc/xls 时依赖扩展名的说明）</summary>
    public string? Remark { get; set; }
}
