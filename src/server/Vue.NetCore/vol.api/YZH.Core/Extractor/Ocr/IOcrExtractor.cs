using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Extractor.Ocr;

/// <summary>
/// OCR 提取器接口（第三方 AI OCR 链路的统一契约，本阶段仅预留）。
/// <para>背景：PDF 扫描件 / 图片无文本层时走 OCR。技术路线已定「后续引入第三方付费接口」，
/// 本接口为接入点，实现类可适配腾讯云 / 百度 / Azure OCR 等供应商。</para>
/// <para>状态：[PLAN] 接口已预留，暂无实现；接入计划见 docs/20-架构决策/文件数据提取能力落地-V1.md §九。</para>
/// </summary>
public interface IOcrExtractor
{
    /// <summary>
    /// 对图片 / 扫描 PDF 执行 OCR 并返回识别文本。
    /// </summary>
    /// <param name="filePath">图片或扫描 PDF 绝对路径</param>
    /// <param name="options">提取选项（页数上限等）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>统一提取结果（OCR 文本写入 FullText，Confidence 按供应商返回）</returns>
    Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// 对图片 / 扫描 PDF 字节流执行 OCR。
    /// </summary>
    /// <param name="stream">可读流（须支持 Seek 以便预处理）</param>
    /// <param name="fileName">源文件名（扩展名决定预处理策略）</param>
    /// <param name="options">提取选项</param>
    /// <param name="ct">取消令牌</param>
    Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default);
}
