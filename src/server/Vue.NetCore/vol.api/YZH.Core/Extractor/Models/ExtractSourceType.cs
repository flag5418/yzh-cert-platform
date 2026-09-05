namespace YZH.Core.Extractor.Models;

/// <summary>
/// 提取源文件类型（与扩展名无关，由 FileTypeDetector 按 magic bytes 判定）。
/// </summary>
public enum ExtractSourceType
{
    /// <summary>无法识别</summary>
    Unknown = 0,

    /// <summary>Word 文档（.doc / .docx，OLE2 或 OOXML Zip）</summary>
    Word = 1,

    /// <summary>Excel 工作簿（.xls / .xlsx）</summary>
    Excel = 2,

    /// <summary>PDF（文本层或扫描件）</summary>
    Pdf = 3,

    /// <summary>纯文本（.txt / .csv / .log / .md 等，无魔法头）</summary>
    Text = 4,

    /// <summary>图片（.jpg / .png 等，走 OCR 链路）[TODO:P2]</summary>
    Image = 5
}
