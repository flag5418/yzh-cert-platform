using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Controllers;

/// <summary>
/// 通用文件存储控制器（YZH 核心能力）
/// <para>提供通用的上传/下载/删除接口，存储路径由调用方通过参数控制</para>
/// <para>任何业务模块（报告模板、标准目录、审核记录等）均可复用此接口</para>
/// </summary>
[Route("api/file-storage")]
public class FileStorageController : WebControllerBase
{
    private readonly IObjectStorage _storage;
    private readonly ILogger<FileStorageController> _logger;

    public FileStorageController(IObjectStorage storage, ILogger<FileStorageController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// 上传文件
    /// <para>存储路径规则：{business}/{orgCode}/{standardCode}/{phaseCode}/{subPath}/{fileName}</para>
    /// <para>调用方只需传入 objectName，无需关心底层存储实现</para>
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)] // 500MB
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string? objectName = null,
        [FromQuery] string? subPath = null)
    {
        if (file == null || file.Length == 0)
            return Ok(new { code = 400, message = "请选择文件" });

        // 未指定 objectName 时，使用时间戳路径
        var finalObjectName = objectName;
        if (string.IsNullOrEmpty(finalObjectName))
        {
            var ext = Path.GetExtension(file.FileName);
            var safeName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            finalObjectName = string.IsNullOrEmpty(subPath)
                ? safeName
                : $"{subPath.Trim('/')}/{safeName}";
        }

        try
        {
            using var stream = file.OpenReadStream();
            await _storage.UploadAsync(finalObjectName, stream, file.Length, file.ContentType);

            return Ok(new
            {
                code = 200,
                data = new
                {
                    path = finalObjectName,
                    fileName = file.FileName,
                    size = file.Length,
                    contentType = file.ContentType,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件失败: {ObjectName}", finalObjectName);
            return Ok(new { code = 500, message = $"上传失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 批量上传文件
    /// </summary>
    [HttpPost("upload-batch")]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> UploadBatch(
        List<IFormFile> files,
        [FromQuery] string? subPath = null)
    {
        if (files == null || files.Count == 0)
            return Ok(new { code = 400, message = "请选择文件" });

        var results = new List<object>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            var ext = Path.GetExtension(file.FileName);
            var safeName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var objectName = string.IsNullOrEmpty(subPath)
                ? safeName
                : $"{subPath.Trim('/')}/{safeName}";

            try
            {
                using var stream = file.OpenReadStream();
                await _storage.UploadAsync(objectName, stream, file.Length, file.ContentType);
                results.Add(new
                {
                    path = objectName,
                    fileName = file.FileName,
                    size = file.Length,
                    success = true,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传文件失败: {FileName}", file.FileName);
                results.Add(new
                {
                    path = objectName,
                    fileName = file.FileName,
                    success = false,
                    error = ex.Message,
                });
            }
        }

        return Ok(new { code = 200, data = results });
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
            return Ok(new { code = 400, message = "缺少文件路径" });

        try
        {
            var (stream, contentType) = await _storage.DownloadAsync(path);
            var fileName = Path.GetFileName(path);
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文件失败: {Path}", path);
            return Ok(new { code = 404, message = $"文件不存在或下载失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
            return Ok(new { code = 400, message = "缺少文件路径" });

        try
        {
            await _storage.DeleteAsync(path);
            return Ok(new { code = 200, message = "删除成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除文件失败: {Path}", path);
            return Ok(new { code = 500, message = $"删除失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    [HttpGet("exists")]
    public async Task<IActionResult> Exists([FromQuery] string path)
    {
        if (string.IsNullOrEmpty(path))
            return Ok(new { code = 400, message = "缺少文件路径" });

        var exists = await _storage.ExistsAsync(path);
        return Ok(new { code = 200, data = exists });
    }

    /// <summary>
    /// 列出指定前缀下的所有文件
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return Ok(new { code = 400, message = "缺少前缀路径" });

        var files = await _storage.ListObjectsAsync(prefix);
        return Ok(new { code = 200, data = files });
    }
}
