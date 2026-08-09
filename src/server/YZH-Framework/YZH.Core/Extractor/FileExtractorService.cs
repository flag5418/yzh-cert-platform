using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Excel;
using YZH.Core.Extractor.Models;
using YZH.Core.Extractor.Pdf;
using YZH.Core.Extractor.Text;
using YZH.Core.Extractor.Word;

namespace YZH.Core.Extractor;

/// <summary>
/// 统一文件提取服务：按 FileTypeDetector 判定结果路由到具体提取器。
/// <para>职责边界：类型识别 → 路由 → 汇总为 FileExtractionResult；不感知业务规则（ExtractionRules 由上层消费）。</para>
/// <para>状态：[DONE] Word/Excel/PDF(文本层)/纯文本 路由；[TODO:P2] 图片 OCR 路由接入第三方。</para>
/// </summary>
public class FileExtractorService : IFileExtractor
{
    private readonly ITextExtractor _wordExtractor;
    private readonly ITextExtractor _excelExtractor;
    private readonly ITextExtractor _pdfExtractor;
    private readonly ITextExtractor _textExtractor;

    /// <summary>
    /// 无参构造：内部实例化具体提取器。
    /// <para>说明：具体提取器按类型 Keyed 注册于容器，本服务不依赖构造注入，
    /// 避免多 ITextExtractor 实例注册时的解析歧义；如需扩展注入可用
    /// Autofac IIndex&lt;ExtractSourceType, ITextExtractor&gt; 改造。</para>
    /// </summary>
    public FileExtractorService()
    {
        _wordExtractor = new NpoiWordExtractor();
        _excelExtractor = new NpoiExcelExtractor();
        _pdfExtractor = new PdfPigPdfExtractor();
        _textExtractor = new PlainTextExtractor();
    }

    /// <summary>
    /// 注入构造（供测试或自定义实现替换）。
    /// </summary>
    public FileExtractorService(
        ITextExtractor? wordExtractor,
        ITextExtractor? excelExtractor,
        ITextExtractor? pdfExtractor,
        ITextExtractor? textExtractor)
    {
        _wordExtractor = wordExtractor ?? new NpoiWordExtractor();
        _excelExtractor = excelExtractor ?? new NpoiExcelExtractor();
        _pdfExtractor = pdfExtractor ?? new PdfPigPdfExtractor();
        _textExtractor = textExtractor ?? new PlainTextExtractor();
    }

    public async Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        var type = FileTypeDetector.Detect(filePath);
        var fileName = Path.GetFileName(filePath);

        switch (type)
        {
            case ExtractSourceType.Word:
                return await _wordExtractor.ExtractAsync(filePath, options, ct);
            case ExtractSourceType.Excel:
                return await _excelExtractor.ExtractAsync(filePath, options, ct);
            case ExtractSourceType.Pdf:
                return await _pdfExtractor.ExtractAsync(filePath, options, ct);
            case ExtractSourceType.Text:
                return await _textExtractor.ExtractAsync(filePath, options, ct);
            case ExtractSourceType.Image:
                // [TODO:P2] 图片 OCR：接入第三方 AI OCR 接口（见 IOcrExtractor）
                return FileExtractionResult.CreateOcrRequired(fileName, filePath, "图片类型：需 OCR 链路（第三方接口未接入，见 IOcrExtractor）");
            default:
                return FileExtractionResult.CreateUnsupported(fileName, filePath, $"无法识别的文件类型：{fileName}");
        }
    }

    public async Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var type = FileTypeDetector.Detect(stream, fileName);

        switch (type)
        {
            case ExtractSourceType.Word:
                return await _wordExtractor.ExtractAsync(stream, fileName, options, ct);
            case ExtractSourceType.Excel:
                return await _excelExtractor.ExtractAsync(stream, fileName, options, ct);
            case ExtractSourceType.Pdf:
                return await _pdfExtractor.ExtractAsync(stream, fileName, options, ct);
            case ExtractSourceType.Text:
                return await _textExtractor.ExtractAsync(stream, fileName, options, ct);
            case ExtractSourceType.Image:
                // [TODO:P2] 图片 OCR：接入第三方 AI OCR 接口（见 IOcrExtractor）
                return FileExtractionResult.CreateOcrRequired(fileName, null, "图片类型：需 OCR 链路（第三方接口未接入，见 IOcrExtractor）");
            default:
                return FileExtractionResult.CreateUnsupported(fileName, null, $"无法识别的文件类型：{fileName}");
        }
    }
}
