
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 文件转换服务（V3：双产物链）
///
/// <para><b>预览链</b>：Office → PDF（LibreOffice 容器）→ 产物列 <c>PreviewPdfPath</c>，
/// 状态列 <c>ConvertStatus</c>/<c>ConvertMessage</c>。PDF / 图片原样透传（产物路径 = 源路径）。</para>
///
/// <para><b>提取链</b>：任意格式 → Markdown（anydoc 容器）→ 产物列 <c>MarkdownPath</c>，
/// 状态列 <c>MarkdownStatus</c>/<c>MarkdownMessage</c>。</para>
///
/// <para><b>产物路径</b>：统一由 <see cref="CodeGeneratorService.BuildProductPath"/> 从**源路径派生**
/// （<c>.../pdf/{原文件名}.pdf</c>、<c>.../markdown/{原文件名}.md</c>），不再重新拼装编码段。</para>
///
/// <para><b>OCR</b>：anydoc 判定「需要 OCR」（退出码 3）时交给 <see cref="IOcrProvider"/>。
/// 当前默认实现不具备能力 → 状态置 <c>unsupported</c> 并写入明确原因，
/// <b>上传流程照常完成</b>，用户可手工定义字段与表格后人工填写。</para>
///
/// <para><b>中间产物</b>（<c>doc2docx</c>/<c>xls2xlsx</c> → <c>ConvertedStoragePath</c>）：
/// 保留仅为**排空存量队列任务**；新的入队点不再产生该类型（决策 D-5）。</para>
///
/// <para><b>★ 并发写入约束（务必先读再改）</b>：两条链是**两个独立队列任务、并发执行**。
/// 队列框架的资源锁**不参与调度**（<c>QueueManager.GetNextPendingTaskAsync</c> 只按
/// <c>Status='pending'</c> 取任务，不看锁），因此同一文件的 PDF 任务与 Markdown 任务
/// **必然可能同时在跑**。两者各自在开头 <c>GetOneAsync</c> 拿到一份内存快照 ——
/// 若用 <c>UpdateAsync(实体)</c> 写**所有列**，后完成的一方会把先完成方刚写的字段
/// **覆盖回自己快照里的旧值**。
/// <para>实测事故（2026-09-26）：Markdown 链先完成（<c>MarkdownPath=.../markdown/x.md</c>、
/// <c>MarkdownStatus=completed</c>），PDF 链后完成 → 全列写回 → <c>MarkdownPath</c> 变 NULL、
/// <c>MarkdownStatus</c> 退回 <c>pending</c>，**零报错、日志还打印了成功**。</para>
/// <para>⇒ 本文件**所有**写入必须走 <see cref="SavePdfChainAsync"/> /
/// <see cref="SaveMarkdownChainAsync"/> / <see cref="SaveLegacyChainAsync"/>，
/// **禁止**直接调用 <c>_db.UpdateAsync(file)</c>。</para>
/// </summary>
public class OfficeConvertService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly DocumentConvertClient _convertClient;
    private readonly IOcrProvider _ocrProvider;
    private readonly ILogger<OfficeConvertService> _logger;

    /// <summary>可预览图片扩展名（透传：预览即原文件）</summary>
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

    /// <summary>需要经 LibreOffice 转 PDF 的 Office 扩展名</summary>
    private static readonly string[] OfficeExtensions = { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf", ".odt", ".ods", ".odp" };

    public OfficeConvertService(
        IDbOrm db,
        IObjectStorage storage,
        DocumentConvertClient convertClient,
        IOcrProvider ocrProvider,
        ILogger<OfficeConvertService> logger)
    {
        _db = db;
        _storage = storage;
        _convertClient = convertClient;
        _ocrProvider = ocrProvider;
        _logger = logger;
    }

    /// <summary>
    /// 执行文件转换（队列任务入口，按 ConvertType 分流）
    /// </summary>
    public async Task<bool> ConvertAsync(FileConvertPayload payload)
    {
        _logger.LogInformation("开始转换: {FileCode} ({ConvertType})", payload.Code, payload.ConvertType);

        // 1. 查找文件记录
        //    ★ 必须用 GetOneIgnoreValidAsync，**不能用 GetOneAsync**：
        //      转换期间文件被刻意置 IsValid = 0（列表隐藏，见 RetryFailedConversionsAsync），
        //      而 GetOneAsync 会自动追加 `IsValid = 1` → **执行器找不到自己的文件**，
        //      任务恒失败（日志只有一句「文件记录不存在」），IsValid 也永远回不到 1
        //      → 文件在目录里永久消失且无法再转换。实测复现于 2026-09-26。
        //      （同 REFERENCE §二十 ⑱ 的 includeDisabled 陷阱，此处是 GetOne 版本）
        var file = (await _db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
            x => x.Code == payload.Code)).Data;
        if (file == null)
        {
            _logger.LogWarning("文件记录不存在: {FileCode}", payload.Code);
            return false;
        }

        // 2. 按转换类型分流
        return payload.ConvertType switch
        {
            "doc2pdf" or "xls2pdf" or "office2pdf" or "docx2pdf" or "xlsx2pdf" or "ppt2pdf" or "pptx2pdf"
                => await ConvertToPdfAsync(file, payload),
            "anydoc2md" or "doc2md" or "docx2md" or "xls2md" or "xlsx2md" or "pdf2md" or "office2md"
                => await ConvertToMarkdownAsync(file, payload),
            // 仅用于排空存量队列任务（新入队点不再产生）
            "doc2docx" or "xls2xlsx"
                => await ConvertToOfficeIntermediateAsync(file, payload),
            // 空/未知 → 双产物（PDF 预览 + Markdown 提取），任一失败不影响另一个
            _ => await ConvertAutoAsync(file, payload)
        };
    }

    /// <summary>
    /// 自动双产物转换：PDF（预览）+ Markdown（提取），任一失败不影响另一个
    /// <para>⚠️ 同任务内**串行**执行，因此两次写入不会互相覆盖；跨任务并发仍受
    /// <see cref="SavePdfChainAsync"/>/<see cref="SaveMarkdownChainAsync"/> 的列隔离保护。</para>
    /// </summary>
    private async Task<bool> ConvertAutoAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        var pdfOk = await ConvertToPdfAsync(file, payload);
        var mdOk = await ConvertToMarkdownAsync(file, payload);
        return pdfOk && mdOk;
    }

    // ========================================================
    // 分链写入（★ 并发安全的核心：只写本链拥有的列）
    // ========================================================

    /// <summary>预览链（PDF）拥有的列</summary>
    private static readonly string[] PdfChainColumns =
    {
        nameof(StandardDirectoryFile.PreviewPdfPath),
        nameof(StandardDirectoryFile.ConvertStatus),
        nameof(StandardDirectoryFile.ConvertMessage),
        nameof(StandardDirectoryFile.ConvertDate),
        nameof(StandardDirectoryFile.IsValid),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>提取链（Markdown）拥有的列</summary>
    private static readonly string[] MarkdownChainColumns =
    {
        nameof(StandardDirectoryFile.MarkdownPath),
        nameof(StandardDirectoryFile.MarkdownStatus),
        nameof(StandardDirectoryFile.MarkdownMessage),
        nameof(StandardDirectoryFile.MarkdownDate),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>遗留中间产物链拥有的列（仅供排空存量任务）</summary>
    private static readonly string[] LegacyChainColumns =
    {
        nameof(StandardDirectoryFile.ConvertedStoragePath),
        nameof(StandardDirectoryFile.ConvertStatus),
        nameof(StandardDirectoryFile.ConvertMessage),
        nameof(StandardDirectoryFile.ConvertDate),
        nameof(StandardDirectoryFile.IsValid),
        nameof(StandardDirectoryFile.UpdateTime)
    };

    /// <summary>
    /// 预览链写回（**只写 PDF 链的列**，绝不触碰 Markdown 链字段）。
    /// <para>详见类注释「并发写入约束」—— 用 <c>UpdateAsync(file)</c> 全列写回会造成静默数据丢失。</para>
    /// <para>⚠️ 本链新增字段时必须同步加进 <see cref="PdfChainColumns"/>，否则**写不进去且不报错**。</para>
    /// </summary>
    private Task SavePdfChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, PdfChainColumns);
    }

    /// <summary>
    /// 提取链写回（**只写 Markdown 链的列**，绝不触碰 PDF 链字段）。
    /// <para>⚠️ 本链新增字段时必须同步加进 <see cref="MarkdownChainColumns"/>。</para>
    /// </summary>
    private Task SaveMarkdownChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, MarkdownChainColumns);
    }

    /// <summary>
    /// 遗留中间产物链写回。
    /// <para>⚠️ 该链的 <c>ConvertStatus</c> 与预览链**共用同一列**，理论上仍可能与 PDF 任务竞争；
    /// 但它只为排空存量队列存在，且存量队列里同一文件不会再同时排 PDF 任务，故可接受。</para>
    /// </summary>
    private Task SaveLegacyChainAsync(StandardDirectoryFile file)
    {
        file.UpdateTime = DateTime.Now;
        return _db.UpdateAsync(file, LegacyChainColumns);
    }

    // ========================================================
    // 预览链：→ PDF
    // ========================================================

    /// <summary>
    /// 预览链：→ PDF，产物上传 MinIO，回写 <c>PreviewPdfPath</c> + <c>ConvertStatus</c>
    /// </summary>
    private async Task<bool> ConvertToPdfAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();

        // ① PDF 原样透传：预览产物 = 原文件（用户设计：PDF 的两条路径相同）
        //    ⚠️ 原实现在此分支**不写 ConvertStatus** → 前端永远看不到"已就绪"，
        //       且产物列填充率统计里 PDF 文件永远为 0（无法验证链路是否跑通）
        if (ext == ".pdf")
        {
            file.PreviewPdfPath = file.StoragePath;
            await MarkPdfCompletedAsync(file);
            _logger.LogInformation("PDF 透传（无需转换）: {FileCode}", file.Code);
            return true;
        }

        // ② 图片原样透传：预览即原文件，无需转 PDF
        if (ImageExtensions.Contains(ext))
        {
            file.PreviewPdfPath = file.StoragePath;
            await MarkPdfCompletedAsync(file);
            _logger.LogInformation("图片透传（无需转换）: {FileCode}", file.Code);
            return true;
        }

        file.ConvertStatus = "converting";
        file.ConvertMessage = null;
        await SavePdfChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = message;
                await SavePdfChainAsync(file);
                _logger.LogWarning("PDF 转换失败（源文件读取）: {FileCode}: {Msg}", file.Code, message);
                return false;
            }

            var result = await _convertClient.ConvertToPdfAsync(file.FileName, content);
            if (!result.Success || result.Content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = result.Message;
                await SavePdfChainAsync(file);
                _logger.LogWarning("PDF 转换失败: {FileCode} ({Kind}): {Msg}", file.Code, result.FailureKind, result.Message);
                return false;
            }

            var targetPath = CodeGeneratorService.BuildProductPath(
                file.StoragePath, CodeGeneratorService.ProductKindPdf, ".pdf");
            if (string.IsNullOrEmpty(targetPath))
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = "源文件缺少存储路径，无法派生产物路径";
                await SavePdfChainAsync(file);
                return false;
            }

            using (var targetStream = new MemoryStream(result.Content))
                await _storage.UploadAsync(targetPath.TrimStart('/'), targetStream, result.Content.Length, "application/pdf");

            file.PreviewPdfPath = targetPath;
            await MarkPdfCompletedAsync(file);

            _logger.LogInformation("PDF 转换完成: {FileCode} → {Path}", file.Code, file.PreviewPdfPath);
            return true;
        }
        catch (Exception ex)
        {
            file.ConvertStatus = "failed";
            file.ConvertMessage = $"转换异常：{ex.Message}";
            await SavePdfChainAsync(file);
            _logger.LogError(ex, "PDF 转换异常: {FileCode}", file.Code);
            return false;
        }
    }

    // ========================================================
    // 提取链：→ Markdown
    // ========================================================

    /// <summary>
    /// 提取链：→ Markdown，产物上传 MinIO，回写 <c>MarkdownPath</c> + <c>MarkdownStatus</c>
    /// <para>失败分类：<c>NeedsOcr</c> → 尝试 <see cref="IOcrProvider"/>；仍不可用则 <c>unsupported</c>。</para>
    /// </summary>
    private async Task<bool> ConvertToMarkdownAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.MarkdownStatus = "converting";
        file.MarkdownMessage = null;
        await SaveMarkdownChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
            {
                file.MarkdownStatus = "failed";
                file.MarkdownMessage = message;
                await SaveMarkdownChainAsync(file);
                return false;
            }

            var result = await _convertClient.ConvertToMarkdownAsync(file.FileName, content);

            // ── 分支 A：anydoc 判定需要 OCR（图片 / 扫描件，退出码 3）──
            if (!result.Success && result.NeedsOcr)
            {
                return await TryOcrAsync(file, content, result.Message);
            }

            // ── 分支 B：不支持的类型（anydoc 无法处理，如纯图片）──
            if (!result.Success && result.FailureKind == ConvertFailureKind.Unsupported)
            {
                // 图片本就走不到 anydoc（anydoc 无图片格式支持）；这里覆盖非图片的未知类型
                if (_ocrProvider.IsAvailable)
                    return await TryOcrAsync(file, content, result.Message);

                file.MarkdownStatus = "unsupported";
                file.MarkdownMessage = result.Message;
                await SaveMarkdownChainAsync(file);
                _logger.LogInformation("Markdown 不支持自动提取: {FileCode}: {Msg}", file.Code, result.Message);
                return false;
            }

            // ── 分支 C：其它失败 ──
            if (!result.Success || result.Content == null)
            {
                file.MarkdownStatus = "failed";
                file.MarkdownMessage = result.Message;
                await SaveMarkdownChainAsync(file);
                _logger.LogWarning("Markdown 转换失败: {FileCode}: {Msg}", file.Code, result.Message);
                return false;
            }

            // ── 分支 D：成功 ──
            return await SaveMarkdownAsync(file, result.Content, null);
        }
        catch (Exception ex)
        {
            file.MarkdownStatus = "failed";
            file.MarkdownMessage = $"转换异常：{ex.Message}";
            await SaveMarkdownChainAsync(file);
            _logger.LogError(ex, "Markdown 转换异常: {FileCode}", file.Code);
            return false;
        }
    }

    /// <summary>
    /// OCR 分支：调用 <see cref="IOcrProvider"/>；不可用时置 <c>unsupported</c>（**不伪造内容**）
    ///
    /// <para>★ 为什么不"默认成功"：占位文本若以 <c>completed</c> 流入提取链，会被当成真文档喂给 LLM，
    /// LLM 会编出一份**格式正确的错误结果**且零报错。宁可如实报 <c>unsupported</c>，
    /// 让用户走「手工定义字段 + 人工填写」——这正是产品当前的设计意图。</para>
    /// </summary>
    private async Task<bool> TryOcrAsync(StandardDirectoryFile file, byte[] content, string fallbackMessage)
    {
        if (!_ocrProvider.IsAvailable)
        {
            file.MarkdownStatus = "unsupported";
            file.MarkdownMessage = string.IsNullOrWhiteSpace(fallbackMessage)
                ? OcrResult.DefaultNotAvailableMessage
                : fallbackMessage;
            await SaveMarkdownChainAsync(file);
            _logger.LogInformation("Markdown 需 OCR 但能力未接入: {FileCode}", file.Code);
            return false;
        }

        try
        {
            var ocr = await _ocrProvider.ToMarkdownAsync(file.FileName, content);
            if (!ocr.Success || ocr.Content == null)
            {
                file.MarkdownStatus = "failed";
                file.MarkdownMessage = string.IsNullOrWhiteSpace(ocr.Message) ? fallbackMessage : ocr.Message;
                await SaveMarkdownChainAsync(file);
                _logger.LogWarning("OCR 转换失败: {FileCode}: {Msg}", file.Code, ocr.Message);
                return false;
            }

            return await SaveMarkdownAsync(file, ocr.Content, "ocr");
        }
        catch (Exception ex)
        {
            file.MarkdownStatus = "failed";
            file.MarkdownMessage = $"OCR 异常：{ex.Message}";
            await SaveMarkdownChainAsync(file);
            _logger.LogError(ex, "OCR 转换异常: {FileCode}", file.Code);
            return false;
        }
    }

    /// <summary>
    /// 标记 PDF 预览链完成：置状态 + **恢复可见性**
    ///
    /// <para>★ 为什么在这里置 <c>IsValid = 1</c>：<c>RetryFailedConversionsAsync</c> 入队时把文件置
    /// <c>IsValid = 0</c>（转换期间隐藏）。原实现由「中间产物链」完成时置回 1；中间产物链已停用，
    /// 若不在此处恢复，**重试过的文件会永久隐藏在目录树里**（零报错，最难查）。</para>
    ///
    /// <para>⚠️ <c>IsValid</c> 属于预览链的列：提取链**不写**它。若让提取链也写，
    /// 两条链并发时会出现「提取成功把 IsValid 置 1，而 PDF 其实还在转」的假就绪。</para>
    /// </summary>
    private async Task MarkPdfCompletedAsync(StandardDirectoryFile file)
    {
        file.ConvertStatus = "completed";
        file.ConvertMessage = null;
        file.ConvertDate = DateTime.Now;
        file.IsValid = 1;
        await SavePdfChainAsync(file);
    }

    /// <summary>落库 Markdown 产物（含状态与时间）</summary>
    private async Task<bool> SaveMarkdownAsync(StandardDirectoryFile file, byte[] content, string? viaTag)
    {
        var targetPath = CodeGeneratorService.BuildProductPath(
            file.StoragePath, CodeGeneratorService.ProductKindMarkdown, ".md");
        if (string.IsNullOrEmpty(targetPath))
        {
            file.MarkdownStatus = "failed";
            file.MarkdownMessage = "源文件缺少存储路径，无法派生产物路径";
            await SaveMarkdownChainAsync(file);
            return false;
        }

        using (var targetStream = new MemoryStream(content))
            await _storage.UploadAsync(targetPath.TrimStart('/'), targetStream, content.Length, "text/markdown");

        file.MarkdownPath = targetPath;
        file.MarkdownStatus = "completed";
        file.MarkdownMessage = viaTag == "ocr" ? "内容由 OCR 提取" : null;
        file.MarkdownDate = DateTime.Now;
        await SaveMarkdownChainAsync(file);

        _logger.LogInformation("Markdown 转换完成: {FileCode} → {Path}{Via}",
            file.Code, file.MarkdownPath, viaTag == null ? "" : $"（{viaTag}）");
        return true;
    }

    // ========================================================
    // 中间产物链（仅排空存量队列任务）
    // ========================================================

    /// <summary>
    /// 中间产物链：doc→docx / xls→xlsx，回写 <c>ConvertedStoragePath</c>。
    /// <para>⚠️ 新入队点已不再产生该类型（决策 D-5：停止写入新值、字段保留）；
    /// 本方法仅为排空历史队列任务而保留。</para>
    /// </summary>
    private async Task<bool> ConvertToOfficeIntermediateAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.ConvertStatus = "converting";
        await SaveLegacyChainAsync(file);

        try
        {
            var (ok, content, message) = await DownloadSourceAsync(file, payload);
            if (!ok || content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = message;
                await SaveLegacyChainAsync(file);
                return false;
            }

            var ext = payload.ConvertType == "doc2docx" ? ".docx" : ".xlsx";
            var contentType = payload.ConvertType == "doc2docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var result = await _convertClient.ConvertToFormatAsync(file.FileName, content, ext.TrimStart('.'));
            if (!result.Success || result.Content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = result.Message;
                await SaveLegacyChainAsync(file);
                return false;
            }

            // 遗留路径：仍写 converted 兄弟路径（存量任务语义）
            var targetPath = payload.TargetPath?.TrimStart('/')
                             ?? BuildLegacySiblingPath(file.StoragePath, ext).TrimStart('/');
            using (var targetStream = new MemoryStream(result.Content))
                await _storage.UploadAsync(targetPath, targetStream, result.Content.Length, contentType);

            file.ConvertedStoragePath = "/" + targetPath;
            file.ConvertStatus = "completed";
            file.ConvertDate = DateTime.Now;
            file.IsValid = 1;
            await SaveLegacyChainAsync(file);

            _logger.LogInformation("中间产物转换完成（遗留）: {FileCode} → {Path}", file.Code, file.ConvertedStoragePath);
            return true;
        }
        catch (Exception ex)
        {
            file.ConvertStatus = "failed";
            file.ConvertMessage = ex.Message;
            await SaveLegacyChainAsync(file);
            _logger.LogError(ex, "中间产物转换异常: {FileCode}", file.Code);
            return false;
        }
    }

    // ========================================================
    // 辅助
    // ========================================================

    /// <summary>下载源文件字节（优先 payload.SourcePath，其次 file.StoragePath）</summary>
    private async Task<(bool Ok, byte[]? Content, string Message)> DownloadSourceAsync(
        StandardDirectoryFile file, FileConvertPayload payload)
    {
        var rawPath = payload.SourcePath;
        if (string.IsNullOrWhiteSpace(rawPath)) rawPath = file.StoragePath;
        if (string.IsNullOrWhiteSpace(rawPath)) return (false, null, "源文件存储路径为空");

        try
        {
            var (stream, _) = await _storage.DownloadAsync(rawPath.TrimStart('/'));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            if (ms.Length == 0) return (false, null, "源文件内容为空");
            return (true, ms.ToArray(), "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "源文件读取失败: {FileCode} {Path}", file.Code, rawPath);
            return (false, null, $"源文件读取失败：{ex.Message}");
        }
    }

    /// <summary>遗留同目录兄弟路径（仅供排空存量 doc2docx/xls2xlsx 任务使用）</summary>
    private static string BuildLegacySiblingPath(string? storagePath, string ext)
    {
        if (string.IsNullOrEmpty(storagePath))
            return "converted/" + Guid.NewGuid().ToString("N") + ext;
        var dir = Path.GetDirectoryName(storagePath.TrimStart('/'))?.Replace('\\', '/') ?? "";
        var stem = Path.GetFileNameWithoutExtension(storagePath);
        return string.IsNullOrEmpty(dir) ? $"{stem}{ext}" : $"{dir}/{stem}{ext}";
    }
}

/// <summary>
/// 文件转换载荷（队列任务 payload）
/// </summary>
public class FileConvertPayload
{
    public string Code { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";

    /// <summary>
    /// 转换类型：
    /// <para><c>office2pdf</c>（预览链，含 doc2pdf/xls2pdf/docx2pdf/… 别名）</para>
    /// <para><c>anydoc2md</c>（提取链，含 doc2md/pdf2md/… 别名）</para>
    /// <para><c>doc2docx</c>/<c>xls2xlsx</c>（遗留中间产物，仅排空存量任务）</para>
    /// <para>空 → 自动双产物（PDF + Markdown）</para>
    /// </summary>
    public string ConvertType { get; set; } = "";

    /// <summary>企业编码（G-2d：非空且≠虚拟企业 ⇒ 转换成功后自动追加 doc_extract 提取任务）</summary>
    public string EnterpriseCode { get; set; } = "";

    /// <summary>阶段编码（透传给 doc_extract payload）</summary>
    public string StageCode { get; set; } = "";
}
