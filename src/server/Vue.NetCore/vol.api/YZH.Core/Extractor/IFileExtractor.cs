using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Extractor;

/// <summary>
/// 文件提取器统一入口接口。
/// <para>职责：按文件类型路由到具体提取器（Word / Excel / PDF / 文本 / OCR），返回统一提取结果模型。
/// 上层（标准目录上传钩子、提取引擎、工作流 get_field Skill）只依赖本接口。</para>
/// <para>状态：[DONE] 基本逻辑实现（Word/Excel/PDF 文本层/纯文本）；[TODO:P2] 图片 OCR 第三方实现接入。</para>
/// </summary>
public interface IFileExtractor
{
    /// <summary>
    /// 按文件路径提取。
    /// </summary>
    /// <param name="filePath">源文件绝对路径</param>
    /// <param name="options">提取选项（null 用默认）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>统一提取结果（含字段/表格/全文/OCR 标记）</returns>
    Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// 按字节流提取（供上传管道在落盘前后直接消费）。
    /// </summary>
    /// <param name="stream">可读流（须支持 Seek 以完成 magic bytes 探测）</param>
    /// <param name="fileName">源文件名（用于类型识别兜底与展示）</param>
    /// <param name="options">提取选项（null 用默认）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>统一提取结果（不含 FilePath）</returns>
    Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default);
}
