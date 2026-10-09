using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.Storage;

namespace CertPlatform.Shared.DocExtraction;

/// <summary>
/// ★ 文件转换内核（36 号 T1.1，2026-10-03）—— <b>不含任何 DB 读写</b>。
///
/// <para><b>为什么抽出来</b>：本仓有<b>两条</b>文件转换链 ——
/// 标准目录 / 企业资料库（<c>OfficeConvertService</c>，Admin）与
/// 企业原始资料（<c>EnterpriseOriginalIngestExecutor</c>，Auditor）。
/// 两条链的<b>转换能力完全相同</b>（同样的 LibreOffice 转 PDF、同样的 anydoc 转 Markdown、
/// 同样的 OCR 兜底、同样的「PDF/图片原样透传」），<b>只有落库的表和列不同</b>。
/// 不抽核心就等于把状态机抄第二遍 —— 而状态机抄第二遍的代价已经付过一次：
/// <c>OfficeConvertService</c> 类注释记录的 2026-09-26 事故（PDF 链与 Markdown 链并发全列写回，
/// 互相覆盖字段，<b>零报错</b>）。</para>
///
/// <para><b>职责边界（铁律）</b>：本类<b>只做</b>「字节进 → 产物字节出 + 状态」。
/// <b>禁止</b>在这里：① 查 DB ② 写状态列 ③ 上传产物 ④ 感知 <c>EnterpriseCode</c>。
/// 状态落库由各宿主的链实现负责（列白名单不同，必须各自持有）。</para>
///
/// <para><b>产物路径</b>：由 <see cref="PathBuilder.Product"/>（唯一权威）从<b>源路径</b>派生，
/// 与源文件夹结构同步，不重新拼装编码段。</para>
///
/// <para><b>OCR</b>：anydoc 判定「需要 OCR」（退出码 3）时交给 <see cref="IOcrProvider"/>；
/// 能力未接入 ⇒ 返回 <c>unsupported</c> 而<b>不伪造内容</b>。
/// 占位文本若以 completed 流入下游，LLM 会基于它编出一份<b>格式正确的错误结果</b>且零报错。</para>
///
/// <para>★★ <b>2026-10-09 按类型分流</b>：<b>图片</b>直接送视觉模型；<b>PDF</b> 必须先经
/// <see cref="DocumentConvertClient.RenderPdfPagesAsync"/> <b>逐页渲染成图片</b>再送 ——
/// 视觉模型不接受 PDF 字节走 <c>image_url</c>（实测两种 MIME 均 400
/// <c>The image format is illegal and cannot be opened</c>）。
/// 而 anydoc 退出码 3 时<b>不产出任何文件</b>（21 页 PDF 只 1 页无文本层也一样）
/// ⇒ PDF 只能整份渲染 + 整份识别。</para>
/// </summary>
public interface IFileConvertCore
{
    /// <summary>
    /// 预览链：任意格式 → PDF（<b>不做转换</b>，不上传产物）。
    /// <para>调用方拿到 <see cref="FileConvertCoreResult.TargetPath"/> 与
    /// <see cref="FileConvertCoreResult.Passthrough"/> 后自行决定：透传则只写路径，
    /// 非透传则把 <see cref="FileConvertCoreResult.Content"/> 上传到该路径。</para>
    /// </summary>
    Task<FileConvertCoreResult> ConvertToPdfAsync(string fileName, string? sourcePath, byte[]? content);

    /// <summary>
    /// 提取链：任意格式 → Markdown（含 OCR 兜底）。同样<b>不上传产物</b>。
    /// <para>⛔ 本重载<b>没有源路径</b> ⇒ 无法派生 <c>markdown/</c> 产物路径，
    /// 恒返回 failed。<b>请用</b> <see cref="ConvertToMarkdownAtAsync"/>。</para>
    /// </summary>
    Task<FileConvertCoreResult> ConvertToMarkdownAsync(string fileName, byte[]? content);

    /// <summary>★ 提取链主入口（带源路径 ⇒ 可派生 <c>markdown/</c> 产物路径）</summary>
    Task<FileConvertCoreResult> ConvertToMarkdownAtAsync(string fileName, string? sourcePath, byte[]? content);

    /// <summary>
    /// ★ <b>视觉 OCR 直通</b>（图片 / PDF → Markdown，2026-10-09）。
    ///
    /// <para><b>存在的理由</b>：<see cref="ConvertToMarkdownAtAsync"/> 内部已含 OCR 兜底，
    /// 但企业原始资料的 ingest 另有一层<b>独立兜底</b>（<c>EnterpriseOriginalIngestExecutor</c> 的 T1）。
    /// 若那层直接调 <c>IOcrProvider.ToMarkdownAsync</c>，就会<b>绕开 PDF 逐页渲染</b>
    /// ⇒ 同一个 400 缺陷在第二处复现。故把「PDF 先渲染再识别」收口到本方法，两处共用一套。</para>
    ///
    /// <para>⚠️ 语义：<b>PDF 会先逐页渲染再识别</b>；图片直接识别。
    /// ⛔ 绝不把 PDF 字节原样送视觉模型（恒 400）。</para>
    /// </summary>
    Task<FileConvertCoreResult> OcrToMarkdownAsync(string fileName, string? sourcePath, byte[]? content);

    /// <summary>
    /// 格式归一（LibreOffice）：<c>.doc → .docx</c> / <c>.xls → .xlsx</c> / <c>.ppt → .pptx</c>。
    /// <para>两个调用方：① 标准目录的<b>遗留中间产物链</b>（<c>doc2docx</c>/<c>xls2xlsx</c>，仅排空存量队列）
    /// ② 企业原始资料 ingest 的**步骤① 格式归一**（36 号 §5.1：老企业交上来的常是 .doc/.xls）。</para>
    /// </summary>
    Task<FileConvertCoreResult> ConvertToFormatAsync(string fileName, byte[]? content, string targetFormat);

    /// <summary>该文件名是否需要走转换链（.DS_Store / .tmp 等直接忽略）</summary>
    bool NeedsConversion(string? fileName);

    /// <summary>可预览图片扩展名（预览即原文件，无需转 PDF）</summary>
    IReadOnlyList<string> ImageExtensions { get; }
}

/// <summary>转换内核返回值（<b>纯数据，无副作用</b>）</summary>
public sealed class FileConvertCoreResult
{
    /// <summary>是否成功产出可用产物（<c>unsupported</c> 恒为 false）</summary>
    public bool Success { get; set; }

    /// <summary>
    /// 状态值 —— <b>⛔ 只能是</b> <c>completed</c> / <c>failed</c> / <c>unsupported</c> 之一。
    /// <para>★ 与前端 <c>cert-share/src/utils/convertStatus.ts</c> 字典逐字对齐；
    /// 写 <c>converted</c> 会让徽标组件 <c>CLASS_MAP</c> 未命中 ⇒ 页面显示「未知状态」且无任何报错。</para>
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>失败 / 跳过原因（<c>completed</c> 时为 null 或提示语如「内容由 OCR 提取」）</summary>
    public string? Message { get; set; }

    /// <summary>
    /// 产物字节。<b>透传时为 null</b>（此时 <see cref="TargetPath"/> 即最终路径，无需上传）。
    /// </summary>
    public byte[]? Content { get; set; }

    /// <summary>
    /// 产物宿主路径（已由 <see cref="PathBuilder.Product"/> 派生，以 <c>/</c> 开头）。
    /// <para>透传场景（PDF / 图片预览）= 源路径本身。</para>
    /// </summary>
    public string? TargetPath { get; set; }

    /// <summary>true = 原样透传，<see cref="Content"/> 为 null，调用方<b>不需要</b>上传</summary>
    public bool Passthrough { get; set; }

    /// <summary>产物 MIME（上传时用；透传时为 null，由调用方按源类型推断）</summary>
    public string? ContentType { get; set; }

    public static FileConvertCoreResult Completed(string targetPath, byte[] content, string? contentType = null, string? message = null)
        => new() { Success = true, Status = "completed", Content = content, TargetPath = targetPath, ContentType = contentType, Message = message };

    public static FileConvertCoreResult PassthroughAs(string sourcePath, string? message = null)
        => new() { Success = true, Status = "completed", Content = null, TargetPath = sourcePath, Passthrough = true, Message = message };

    public static FileConvertCoreResult Failed(string message)
        => new() { Success = false, Status = "failed", Message = message };

    /// <summary>能力边界（非故障）：宁可如实报 unsupported，也不伪造内容</summary>
    public static FileConvertCoreResult Unsupported(string message)
        => new() { Success = false, Status = "unsupported", Message = message };
}

/// <summary>
/// <see cref="IFileConvertCore"/> 默认实现（唯一定义，无状态，可注册单例）。
/// </summary>
public class FileConvertCore : IFileConvertCore
{
    private readonly DocumentConvertClient _convertClient;
    private readonly IOcrProvider _ocrProvider;
    private readonly ILogger<FileConvertCore> _logger;

    /// <summary>可预览图片扩展名（透传：预览即原文件）</summary>
    private static readonly string[] Images =
    { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

    private static readonly string[] IgnoredExtensions =
    { ".ds_store", ".tmp", ".temp" };

    /// <summary>
    /// PDF 逐页 OCR 的并发上限（2026-10-09）。
    /// <para>单次视觉调用实测 4~7 秒，21 页串行要 ~2 分钟（会顶到队列任务超时）。
    /// 取 4 与 <c>EnterpriseOriginalAnalyzeExecutor.MaxConcurrency</c> 同口径 —— 视觉模型与文本模型
    /// 是同一家（百炼）的配额，并发再高会撞限流。</para>
    /// </summary>
    private const int MaxOcrConcurrency = 4;

    public IReadOnlyList<string> ImageExtensions => Images;

    public FileConvertCore(
        DocumentConvertClient convertClient,
        IOcrProvider ocrProvider,
        ILogger<FileConvertCore> logger)
    {
        _convertClient = convertClient;
        _ocrProvider = ocrProvider;
        _logger = logger;
    }

    public bool NeedsConversion(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) return false;
        return !IgnoredExtensions.Contains(ext.TrimStart('.'));
    }

    public Task<FileConvertCoreResult> ConvertToPdfAsync(string fileName, string? sourcePath, byte[]? content)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();

        // ① PDF 原样透传：用户设计「PDF 的预览路径 = 源路径」
        if (ext == ".pdf")
            return Task.FromResult(FileConvertCoreResult.PassthroughAs(sourcePath ?? "", "PDF 原文件即预览产物"));

        // ② 图片原样透传：预览即原文件
        if (Images.Contains(ext))
            return Task.FromResult(FileConvertCoreResult.PassthroughAs(sourcePath ?? "", "图片原文件即预览产物"));

        if (string.IsNullOrEmpty(sourcePath))
            return Task.FromResult(FileConvertCoreResult.Failed("源文件缺少存储路径，无法派生产物路径"));

        var targetPath = PathBuilder.Product(sourcePath, PathBuilder.PdfSegment, ".pdf");
        if (string.IsNullOrEmpty(targetPath))
            return Task.FromResult(FileConvertCoreResult.Failed("源文件缺少存储路径，无法派生产物路径"));

        if (content == null || content.Length == 0)
            return Task.FromResult(FileConvertCoreResult.Failed("源文件内容为空"));

        return ConvertPdfInnerAsync(fileName, content, targetPath);
    }

    private async Task<FileConvertCoreResult> ConvertPdfInnerAsync(string fileName, byte[] content, string targetPath)
    {
        try
        {
            var result = await _convertClient.ConvertToPdfAsync(fileName, content);
            if (!result.Success || result.Content == null)
                return FileConvertCoreResult.Failed(result.Message);

            return FileConvertCoreResult.Completed(targetPath, result.Content, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileConvertCore] PDF 转换异常: {FileName}", fileName);
            return FileConvertCoreResult.Failed($"转换异常：{ex.Message}");
        }
    }

    public Task<FileConvertCoreResult> ConvertToMarkdownAsync(string fileName, byte[]? content)
        => Task.FromResult(FileConvertCoreResult.Failed(
            "源文件缺少存储路径，无法派生产物路径（请改用带 sourcePath 的重载）"));

    /// <summary>★ 提取链主入口：先派生产物路径，再转换</summary>
    public async Task<FileConvertCoreResult> ConvertToMarkdownAtAsync(string fileName, string? sourcePath, byte[]? content)
    {
        var targetPath = PathBuilder.Product(sourcePath, PathBuilder.MarkdownSegment, ".md");
        if (string.IsNullOrEmpty(targetPath))
            return FileConvertCoreResult.Failed("源文件缺少存储路径，无法派生产物路径");

        if (content == null || content.Length == 0)
            return FileConvertCoreResult.Failed("源文件内容为空");

        return await ConvertMarkdownInnerAsync(fileName, content, targetPath);
    }

    /// <summary>格式归一（LibreOffice）；<paramref name="targetFormat"/> 不含点，如 <c>docx</c></summary>
    public async Task<FileConvertCoreResult> ConvertToFormatAsync(string fileName, byte[]? content, string targetFormat)
    {
        if (content == null || content.Length == 0)
            return FileConvertCoreResult.Failed("源文件内容为空");

        var ext = string.IsNullOrEmpty(targetFormat)
            ? ""
            : (targetFormat.StartsWith('.') ? targetFormat : "." + targetFormat);

        try
        {
            var result = await _convertClient.ConvertToFormatAsync(fileName, content, ext.TrimStart('.'));
            if (!result.Success || result.Content == null)
                return FileConvertCoreResult.Failed(result.Message);

            // ⚠️ 归一产物<b>没有固定宿主路径</b>（由调用方决定：legacy 走兄弟路径，
            //    ingest 走「同名新扩展名」），故 TargetPath 留空，由调用方自行拼装。
            return FileConvertCoreResult.Completed("", result.Content,
                ContentTypeFor(ext), "格式归一：" + ext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileConvertCore] 格式归一异常: {FileName} → {Ext}", fileName, ext);
            return FileConvertCoreResult.Failed($"格式归一异常：{ex.Message}");
        }
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".odt" => "application/vnd.oasis.opendocument.text",
        ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        ".odp" => "application/vnd.oasis.opendocument.presentation",
        _ => "application/octet-stream"
    };

    private async Task<FileConvertCoreResult> ConvertMarkdownInnerAsync(string fileName, byte[] content, string targetPath)
    {
        try
        {
            var result = await _convertClient.ConvertToMarkdownAsync(fileName, content);

            // ── 分支 A：anydoc 判定需要 OCR（图片 / 扫描件，退出码 3）──
            if (!result.Success && result.NeedsOcr)
                return await TryOcrAsync(fileName, content, result.Message, targetPath);

            // ── 分支 B：不支持的类型（非图片的未知类型）──
            if (!result.Success && result.FailureKind == ConvertFailureKind.Unsupported)
            {
                if (_ocrProvider.IsAvailable)
                    return await TryOcrAsync(fileName, content, result.Message, targetPath);
                return FileConvertCoreResult.Unsupported(result.Message);
            }

            // ── 分支 C：其它失败 ──
            if (!result.Success || result.Content == null)
                return FileConvertCoreResult.Failed(result.Message);

            // ── 分支 D：成功 ──
            return FileConvertCoreResult.Completed(targetPath, result.Content, "text/markdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileConvertCore] Markdown 转换异常: {FileName}", fileName);
            return FileConvertCoreResult.Failed($"转换异常：{ex.Message}");
        }
    }

    /// <summary>
    /// OCR 兜底分支。
    /// <para>★ 为什么不「默认成功」：占位文本若以 <c>completed</c> 流入下游，会被当成真文档喂给 LLM，
    /// LLM 会编出一份<b>格式正确的错误结果</b>且零报错。宁可如实报 <c>unsupported</c>，
    /// 让用户走「手工定义 + 人工填写」—— 这正是产品当前的设计意图。</para>
    ///
    /// <para>★★ <b>2026-10-09：按类型分流</b> ——
    /// <b>PDF 先逐页渲染成图片再识别</b>，图片直接识别。
    /// ⛔ 绝不把 PDF 字节原样送视觉模型：实测 <c>data:image/png</c> 与 <c>data:application/pdf</c>
    /// 均返回 400 <c>The image format is illegal and cannot be opened</c>（真机复现）。</para>
    /// </summary>
    private async Task<FileConvertCoreResult> TryOcrAsync(
        string fileName, byte[] content, string fallbackMessage, string targetPath)
    {
        if (!_ocrProvider.IsAvailable)
            return FileConvertCoreResult.Unsupported(
                string.IsNullOrWhiteSpace(fallbackMessage) ? OcrResult.DefaultNotAvailableMessage : fallbackMessage);

        // ★ PDF 与图片分流（判据 = 魔数，⛔ 不信扩展名）
        if (ImagePreprocess.IsPdf(content))
            return await TryOcrPdfAsync(fileName, content, fallbackMessage, targetPath);

        try
        {
            var ocr = await _ocrProvider.ToMarkdownAsync(fileName, content);
            if (!ocr.Success || ocr.Content == null)
                return FileConvertCoreResult.Failed(
                    string.IsNullOrWhiteSpace(ocr.Message) ? fallbackMessage : ocr.Message);

            return FileConvertCoreResult.Completed(targetPath, ocr.Content, "text/markdown", "内容由 OCR 提取");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FileConvertCore] OCR 异常: {FileName}", fileName);
            return FileConvertCoreResult.Failed($"OCR 异常：{ex.Message}");
        }
    }

    /// <summary>
    /// ★ <b>PDF 专用 OCR（2026-10-09）</b>：逐页渲染 → 逐页视觉 OCR → 按页序拼接。
    ///
    /// <para><b>为什么整份都要渲染</b>：anydoc 退出码 3 时<b>不产出任何文件</b>
    /// （实测 21 页 PDF 仅第 7 页无文本层，会话目录里连 <c>.md</c> 都没有）
    /// ⇒ 拿不到「已提取的那部分文本」，只能整份走视觉。
    /// 代价是比「仅补 OCR 缺失页」贵，但换到的是<b>能跑通</b>（原实现恒 400）。</para>
    ///
    /// <para><b>并发与保序</b>：逐页并发（上限 <see cref="MaxOcrConcurrency"/>）以避免 21 页串行
    /// 要等 2 分钟；结果<b>按下标写回数组</b>，拼接严格按页序，⛔ 不依赖完成顺序。</para>
    ///
    /// <para><b>部分成功也算成功</b>：只要有任一页产出内容就返回 completed，
    /// 但把「共 N 页 / 成功 M 页 / 截断」如实写进 Message（下游落 <c>MarkdownMessage</c>）。
    /// ⛔ 不静默 —— 残缺内容被当成完整文档是最危险的失败形态。</para>
    /// </summary>
    private async Task<FileConvertCoreResult> TryOcrPdfAsync(
        string fileName, byte[] content, string fallbackMessage, string targetPath)
    {
        var opt = _ocrProvider.PdfOptions.Normalized();

        var render = await _convertClient.RenderPdfPagesAsync(fileName, content, opt.MaxPages, opt.Dpi);
        if (!render.Success || render.Pages.Count == 0)
            return FileConvertCoreResult.Failed(
                string.IsNullOrWhiteSpace(render.Message) ? fallbackMessage : render.Message);

        var texts = new string?[render.Pages.Count];
        var failures = new List<string>();
        using var gate = new SemaphoreSlim(MaxOcrConcurrency);

        await Task.WhenAll(Enumerable.Range(0, render.Pages.Count).Select(async i =>
        {
            await gate.WaitAsync();
            try
            {
                var r = await _ocrProvider.ToMarkdownAsync(
                    $"{fileName}（第 {i + 1} 页）", render.Pages[i]);
                if (r.Success && !string.IsNullOrWhiteSpace(r.Markdown))
                {
                    texts[i] = r.Markdown!.Trim();
                }
                else
                {
                    var why = string.IsNullOrWhiteSpace(r.Message) ? "未产出内容" : r.Message;
                    lock (failures) failures.Add($"第 {i + 1} 页：{why}");
                }
            }
            catch (Exception ex)
            {
                lock (failures) failures.Add($"第 {i + 1} 页异常：{ex.Message}");
            }
            finally { gate.Release(); }
        }));

        var okCount = texts.Count(t => !string.IsNullOrWhiteSpace(t));
        if (okCount == 0)
            return FileConvertCoreResult.Failed(
                $"PDF 共 {render.Pages.Count} 页，视觉模型均未产出内容：{string.Join("；", failures.Take(3))}");

        // 按页序拼接。★ 加 HTML 注释标注页码：人工核对时能立刻定位「哪一页没识别出来」
        var sb = new StringBuilder();
        for (var i = 0; i < texts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i])) continue;
            sb.Append("<!-- 第 ").Append(i + 1).Append(" 页 -->\n\n")
              .Append(texts[i]!.Trim()).Append("\n\n");
        }

        var msg = $"内容由视觉模型逐页 OCR 提取（成功 {okCount}/{render.Pages.Count} 页）";
        if (render.Truncated) msg += $"；{render.Message}";
        if (failures.Count > 0)
            msg += $"；{failures.Count} 页未识别（{string.Join("；", failures.Take(2))}）";

        _logger.LogInformation("[FileConvertCore] PDF OCR 完成: {FileName} {Msg}", fileName, msg);
        return FileConvertCoreResult.Completed(targetPath, Encoding.UTF8.GetBytes(sb.ToString()), "text/markdown", msg);
    }

    /// <summary>视觉 OCR 直通（图片直接识别；PDF 先逐页渲染）—— 供企业侧兜底分支复用</summary>
    public async Task<FileConvertCoreResult> OcrToMarkdownAsync(string fileName, string? sourcePath, byte[]? content)
    {
        if (content == null || content.Length == 0)
            return FileConvertCoreResult.Failed("源文件内容为空");

        var targetPath = PathBuilder.Product(sourcePath, PathBuilder.MarkdownSegment, ".md");
        if (string.IsNullOrEmpty(targetPath))
            return FileConvertCoreResult.Failed("源文件缺少存储路径，无法派生产物路径");

        return await TryOcrAsync(fileName, content, "", targetPath);
    }
}