using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Extractor;

/// <summary>
/// 文本类提取器接口（Word / Excel / PDF / 纯文本 的具体实现契约）。
/// <para>与 <see cref="IFileExtractor"/> 同签名：路径 / 字节流两种输入，统一输出 FileExtractionResult。</para>
/// </summary>
public interface ITextExtractor
{
    /// <summary>按文件路径提取文本与表格。</summary>
    Task<FileExtractionResult> ExtractAsync(string filePath, ExtractionOptions? options = null, CancellationToken ct = default);

    /// <summary>按字节流提取文本与表格。</summary>
    Task<FileExtractionResult> ExtractAsync(Stream stream, string fileName, ExtractionOptions? options = null, CancellationToken ct = default);
}
