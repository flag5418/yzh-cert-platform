
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// Office 文件转换服务（V2：决策 D-6 落地）
/// <para>预览链：doc/docx/xls/xlsx/ppt/pptx → PDF（LibreOffice 容器），产物列 preview_pdf_path</para>
/// <para>提取链：任意格式 → Markdown（anydoc 容器，旧格式直转），产物列 markdown_path</para>
/// <para>converted_storage_path 语义保留：doc→docx / xls→xlsx 中间产物（兼容存量队列任务）</para>
/// </summary>
public class OfficeConvertService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly CodeGeneratorService _codeGenerator;
    private readonly CertPlatform.Shared.DocExtraction.DocumentConvertClient _convertClient;
    private readonly ILogger<OfficeConvertService> _logger;

    public OfficeConvertService(
        IDbOrm db,
        IObjectStorage storage,
        CodeGeneratorService codeGenerator,
        CertPlatform.Shared.DocExtraction.DocumentConvertClient convertClient,
        ILogger<OfficeConvertService> logger)
    {
        _db = db;
        _storage = storage;
        _codeGenerator = codeGenerator;
        _convertClient = convertClient;
        _logger = logger;
    }

    /// <summary>
    /// 执行文件转换（队列任务入口，按 ConvertType 分流）
    /// </summary>
    public async Task<bool> ConvertAsync(FileConvertPayload payload)
    {
        _logger.LogInformation("开始转换: {FileCode} ({ConvertType})", payload.FileCode, payload.ConvertType);

        // 1. 查找文件记录
        var file = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.FileCode == payload.FileCode)).Data;
        if (file == null)
        {
            _logger.LogWarning("文件记录不存在: {FileCode}", payload.FileCode);
            return false;
        }

        // 2. 按转换类型分流
        return payload.ConvertType switch
        {
            "doc2pdf" or "xls2pdf" or "office2pdf" or "docx2pdf" or "xlsx2pdf" or "ppt2pdf" or "pptx2pdf"
                => await ConvertToPdfAsync(file, payload),
            "anydoc2md" or "doc2md" or "docx2md" or "xls2md" or "xlsx2md" or "pdf2md" or "office2md"
                => await ConvertToMarkdownAsync(file, payload),
            "doc2docx" or "xls2xlsx"
                => await ConvertToOfficeIntermediateAsync(file, payload),
            _ => await ConvertAutoAsync(file, payload) // 默认：双产物并行（PDF 预览 + Markdown 提取）
        };
    }

    /// <summary>
    /// 自动双产物转换：PDF（预览）+ Markdown（提取），任一失败不影响另一个
    /// </summary>
    private async Task<bool> ConvertAutoAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        var pdfOk = await ConvertToPdfAsync(file, payload);
        var mdOk = await ConvertToMarkdownAsync(file, payload);
        return pdfOk && mdOk;
    }

    /// <summary>
    /// 预览链：→ PDF，产物上传 MinIO，回写 preview_pdf_path
    /// </summary>
    private async Task<bool> ConvertToPdfAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        var ext = Path.GetExtension(file.FileName ?? "")?.ToLowerInvariant() ?? "";
        if (ext == ".pdf")
        {
            // PDF 原样透传：预览产物 = 原文件
            file.PreviewPdfPath = file.StoragePath;
            await _db.UpdateAsync(file);
            return true;
        }

        try
        {
            var sourcePath = (payload.SourcePath ?? file.StoragePath ?? "").TrimStart('/');
            var (stream, _) = await _storage.DownloadAsync(sourcePath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var result = await _convertClient.ConvertToPdfAsync(file.FileName, ms.ToArray());
            if (!result.Success || result.Content == null)
            {
                _logger.LogWarning("PDF 转换失败: {FileCode}: {Msg}", file.FileCode, result.Message);
                // 预览失败不写 convert_status（那是中间产物状态），仅记录
                return false;
            }

            // 产物路径：与源文件同目录，后缀 .pdf
            var targetPath = BuildSiblingPath(file.StoragePath, ".pdf");
            using var targetStream = new MemoryStream(result.Content);
            await _storage.UploadAsync(targetPath.TrimStart('/'), targetStream, result.Content.Length, "application/pdf");

            file.PreviewPdfPath = targetPath.StartsWith("/") ? targetPath : "/" + targetPath;
            await _db.UpdateAsync(file);

            _logger.LogInformation("PDF 转换完成: {FileCode} → {Path}", file.FileCode, file.PreviewPdfPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF 转换异常: {FileCode}", file.FileCode);
            return false;
        }
    }

    /// <summary>
    /// 提取链：→ Markdown，产物上传 MinIO，回写 markdown_path / markdown_status
    /// </summary>
    private async Task<bool> ConvertToMarkdownAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.MarkdownStatus = "converting";
        await _db.UpdateAsync(file);

        try
        {
            var sourcePath = (payload.SourcePath ?? file.StoragePath ?? "").TrimStart('/');
            var (stream, _) = await _storage.DownloadAsync(sourcePath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var result = await _convertClient.ConvertToMarkdownAsync(file.FileName, ms.ToArray());
            if (!result.Success || result.Content == null)
            {
                file.MarkdownStatus = "failed";
                file.MarkdownMessage = result.Message;
                await _db.UpdateAsync(file);
                _logger.LogWarning("Markdown 转换失败: {FileCode}: {Msg}", file.FileCode, result.Message);
                return false;
            }

            var targetPath = BuildSiblingPath(file.StoragePath, ".md");
            using var targetStream = new MemoryStream(result.Content);
            await _storage.UploadAsync(targetPath.TrimStart('/'), targetStream, result.Content.Length, "text/markdown");

            file.MarkdownPath = targetPath.StartsWith("/") ? targetPath : "/" + targetPath;
            file.MarkdownStatus = "completed";
            file.MarkdownMessage = null;
            file.MarkdownDate = DateTime.Now;
            await _db.UpdateAsync(file);

            _logger.LogInformation("Markdown 转换完成: {FileCode} → {Path}", file.FileCode, file.MarkdownPath);
            return true;
        }
        catch (Exception ex)
        {
            file.MarkdownStatus = "failed";
            file.MarkdownMessage = ex.Message;
            await _db.UpdateAsync(file);
            _logger.LogError(ex, "Markdown 转换异常: {FileCode}", file.FileCode);
            return false;
        }
    }

    /// <summary>
    /// 中间产物链（兼容存量 doc2docx / xls2xlsx 队列任务语义）：doc→docx / xls→xlsx，回写 converted_*
    /// </summary>
    private async Task<bool> ConvertToOfficeIntermediateAsync(StandardDirectoryFile file, FileConvertPayload payload)
    {
        file.ConvertStatus = "converting";
        await _db.UpdateAsync(file);

        try
        {
            var sourcePath = (payload.SourcePath ?? file.StoragePath ?? "").TrimStart('/');
            var (stream, _) = await _storage.DownloadAsync(sourcePath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var ext = payload.ConvertType == "doc2docx" ? ".docx" : ".xlsx";
            var contentType = payload.ConvertType == "doc2docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            // LibreOffice 统一处理两种中间产物
            var result = await _convertClient.ConvertToFormatAsync(file.FileName, ms.ToArray(), ext.TrimStart('.'));
            if (!result.Success || result.Content == null)
            {
                file.ConvertStatus = "failed";
                file.ConvertMessage = result.Message;
                await _db.UpdateAsync(file);
                return false;
            }

            var targetPath = payload.TargetPath?.TrimStart('/') ?? BuildSiblingPath(file.StoragePath, ext).TrimStart('/');
            using var targetStream = new MemoryStream(result.Content);
            await _storage.UploadAsync(targetPath, targetStream, result.Content.Length, contentType);

            file.ConvertedStoragePath = "/" + targetPath;
            file.ConvertStatus = "completed";
            file.ConvertDate = DateTime.Now;
            file.IsValid = 1;
            await _db.UpdateAsync(file);

            _logger.LogInformation("中间产物转换完成: {FileCode} → {Path}", file.FileCode, file.ConvertedStoragePath);
            return true;
        }
        catch (Exception ex)
        {
            file.ConvertStatus = "failed";
            file.ConvertMessage = ex.Message;
            await _db.UpdateAsync(file);
            _logger.LogError(ex, "中间产物转换异常: {FileCode}", file.FileCode);
            return false;
        }
    }

    /// <summary>同目录兄弟路径：/a/b/文件名.doc → /a/b/文件名.{ext}</summary>
    private static string BuildSiblingPath(string? storagePath, string ext)
    {
        if (string.IsNullOrEmpty(storagePath))
            return "converted/" + Guid.NewGuid().ToString("N") + ext;
        var dir = Path.GetDirectoryName(storagePath.TrimStart('/'))?.Replace('\\', '/') ?? "";
        var stem = Path.GetFileNameWithoutExtension(storagePath);
        return string.IsNullOrEmpty(dir) ? $"{stem}{ext}" : $"{dir}/{stem}{ext}";
    }
}

/// <summary>
/// 文件转换载荷（队列任务 payload）—— 扩展 ConvertType 语义
/// </summary>
public class FileConvertPayload
{
    public string FileCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    /// <summary>
    /// 转换类型：
    /// doc2pdf/xls2pdf/office2pdf（预览链）| anydoc2md/doc2md/xlsx2md（提取链）
    /// doc2docx/xls2xlsx（中间产物，兼容存量）| 空 → 自动双产物
    /// </summary>
    public string ConvertType { get; set; } = "";
}
