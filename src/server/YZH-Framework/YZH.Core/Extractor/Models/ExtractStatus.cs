namespace YZH.Core.Extractor.Models;

/// <summary>
/// 提取结果状态。
/// </summary>
public enum ExtractStatus
{
    /// <summary>未开始（默认占位）</summary>
    None = 0,

    /// <summary>提取成功</summary>
    Success = 1,

    /// <summary>文件无文本层，需要走 OCR 链路（扫描件 PDF / 图片）</summary>
    OcrRequired = 2,

    /// <summary>文件类型不支持</summary>
    Unsupported = 3,

    /// <summary>提取失败（异常）</summary>
    Failed = 4,

    /// <summary>用户取消</summary>
    Cancelled = 5
}
