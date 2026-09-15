extern alias SharedEntities;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using YZH.Core.Stand.Interfaces;
using YZH.Core.DataBase.Services;
using CertPlatform.Admin.Services.StandardDirectory;
using SharedEntities::YZH.Entity.Admin.Platform.Dir;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 标准目录管理控制器
/// 路由前缀：/api/Workflow/StandardDirectory
/// </summary>
[ApiController]
[Route("api/Workflow/[controller]")]
public class StandardDirectoryController : ControllerBase
{
    private readonly StandardDirectoryService _service;
    private readonly DirectoryTemplateService _templateService;
    private readonly QueueManager _queueManager;
    private readonly IUserContext _userContext;

    public StandardDirectoryController(
        StandardDirectoryService service,
        DirectoryTemplateService templateService,
        QueueManager queueManager,
        IUserContext userContext)
    {
        _service = service;
        _templateService = templateService;
        _queueManager = queueManager;
        _userContext = userContext;
    }

    #region 组织树

    [HttpGet("organization-tree")]
    public async Task<IActionResult> GetOrganizationTree()
    {
        var tree = await _service.GetOrganizationTreeAsync();
        return Ok(new { code = 200, data = tree });
    }

    #endregion

    #region 配置 CRUD

    [HttpGet("configs")]
    public async Task<IActionResult> GetConfigs()
    {
        var configs = await _service.GetConfigsAsync();
        return Ok(new { code = 200, data = configs });
    }

    [HttpGet("configs/{directoryCode}")]
    public async Task<IActionResult> GetConfig(string directoryCode)
    {
        var config = await _service.GetConfigAsync(directoryCode);
        if (config == null) return NotFound(new { code = 404, msg = "配置不存在" });
        return Ok(new { code = 200, data = config });
    }

    [HttpPost("configs/create")]
    public async Task<IActionResult> CreateConfig([FromBody] StandardDirectoryConfig config)
    {
        var result = await _service.CreateConfigAsync(config);
        return Ok(new { code = 200, data = result });
    }

    [HttpPost("configs/{directoryCode}")]
    public async Task<IActionResult> UpdateConfig(string directoryCode, [FromBody] StandardDirectoryConfig config)
    {
        config.DirectoryCode = directoryCode;
        var ok = await _service.UpdateConfigAsync(config);
        return Ok(new { code = ok ? 200 : 400, msg = ok ? "更新成功" : "更新失败" });
    }

    [HttpPost("configs/{directoryCode}/delete")]
    public async Task<IActionResult> DeleteConfig(string directoryCode)
    {
        var ok = await _service.DeleteConfigAsync(directoryCode);
        return Ok(new { code = ok ? 200 : 400, msg = ok ? "删除成功" : "删除失败" });
    }

    #endregion

    #region 文件夹

    [HttpGet("configs/{directoryCode}/folders")]
    public async Task<IActionResult> GetFolderTree(string directoryCode)
    {
        var tree = await _service.GetFolderTreeAsync(directoryCode);
        return Ok(new { code = 200, data = tree });
    }

    [HttpGet("configs/{directoryCode}/folders-flat")]
    public async Task<IActionResult> GetFoldersFlat(string directoryCode)
    {
        var folders = await _service.GetFoldersFlatAsync(directoryCode);
        return Ok(new { code = 200, data = folders });
    }

    [HttpPost("configs/{directoryCode}/folders/create")]
    public async Task<IActionResult> CreateFolder(string directoryCode, [FromBody] SharedEntities::YZH.Entity.Admin.Platform.Dir.StandardDirectoryFolder folder)
    {
        folder.DirectoryCode = directoryCode;
        var (ok, error, result) = await _service.CreateFolderAsync(folder);
        return Ok(new { code = ok ? 200 : 400, msg = error, data = result });
    }

    [HttpPost("folders/{folderCode}")]
    public async Task<IActionResult> UpdateFolder(string folderCode, [FromBody] SharedEntities::YZH.Entity.Admin.Platform.Dir.StandardDirectoryFolder folder)
    {
        folder.FolderCode = folderCode;
        var (ok, error) = await _service.UpdateFolderAsync(folder);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    [HttpPost("folders/{folderCode}/delete")]
    public async Task<IActionResult> DeleteFolder(string folderCode)
    {
        var (ok, error, foldersDeleted, filesDeleted) = await _service.DeleteFolderAsync(folderCode);
        return Ok(new { code = ok ? 200 : 400, msg = error, foldersDeleted, filesDeleted });
    }

    #endregion

    #region 文件

    [HttpGet("folders/{folderCode}/files")]
    public async Task<IActionResult> GetFiles(string folderCode)
    {
        var files = await _service.GetFilesAsync(folderCode);
        return Ok(new { code = 200, data = files });
    }

    [HttpGet("directories/{directoryCode}/root-files")]
    public async Task<IActionResult> GetRootFiles(string directoryCode)
    {
        var files = await _service.GetRootFilesAsync(directoryCode);
        return Ok(new { code = 200, data = files });
    }

    [HttpPost("files/{fileCode}")]
    public async Task<IActionResult> UpdateFile(string fileCode, [FromBody] SharedEntities::YZH.Entity.Admin.Platform.Dir.StandardDirectoryFile file)
    {
        file.FileCode = fileCode;
        var (ok, error) = await _service.UpdateFileAsync(file);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    [HttpPost("files/{fileCode}/delete")]
    public async Task<IActionResult> DeleteFile(string fileCode)
    {
        var (ok, error) = await _service.DeleteFileAsync(fileCode);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string storagePath)
    {
        // 安全校验：仅允许 standard-directory/ 前缀，禁止路径穿越（防御性双保险，
        // MinIO 对象键虽无文件系统穿越风险，但防止越权读取 bucket 内其他模块对象）
        if (string.IsNullOrWhiteSpace(storagePath) || !this.IsAllowedStoragePath(storagePath))
            return BadRequest(new { code = 400, msg = "非法的文件路径" });

        var result = await _service.DownloadFileAsync(storagePath);
        if (result == null)
            return NotFound(new { code = 404, msg = "文件不存在" });

        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
    }

    #endregion

    #region 上传 4 步

    [HttpPost("upload-init")]
    public async Task<IActionResult> UploadInit([FromBody] UploadManifestRequest manifest)
    {
        var (ok, error, response) = await _service.UploadInitAsync(manifest);
        if (!ok) return Ok(new { code = 400, msg = error });
        return Ok(new { code = 200, data = response });
    }

    [HttpPost("upload-file-v2")]
    public async Task<IActionResult> UploadFileV2([FromForm] UploadFileV2Dto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return Ok(new { code = 400, msg = "请选择文件" });

        using var stream = dto.File.OpenReadStream();
        var (ok, error) = await _service.UploadFileAsync(stream, dto.File.Length, dto.FileCode, dto.TaskId);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    [HttpPost("upload-confirm")]
    public async Task<IActionResult> UploadConfirm([FromBody] UploadConfirmRequest req)
    {
        var (ok, error, convertQueueCode) = await _service.UploadConfirmAsync(req.TaskId);
        return Ok(new { code = ok ? 200 : 400, msg = error, convertQueueCode });
    }

    [HttpPost("upload-cancel")]
    public async Task<IActionResult> UploadCancel([FromBody] UploadConfirmRequest req)
    {
        var (ok, error, deleted, restored) = await _service.UploadCancelAsync(req.TaskId);
        return Ok(new { code = ok ? 200 : 400, msg = error, deleted, restored });
    }

    [HttpGet("upload-status")]
    public async Task<IActionResult> GetUploadStatus([FromQuery] string taskId)
    {
        var status = await _service.GetUploadStatusAsync(taskId);
        if (status == null) return NotFound(new { code = 404, msg = "任务不存在" });
        return Ok(new { code = 200, data = status });
    }

    #endregion

    #region 队列相关

    [HttpGet("active-queue")]
    public async Task<IActionResult> GetActiveQueue([FromQuery] string directoryCode)
    {
        var queue = await _service.GetActiveQueueAsync(directoryCode);
        return Ok(new { code = 200, data = queue });
    }

    [HttpPost("convert/progress")]
    public async Task<IActionResult> GetConvertProgress([FromQuery] string taskId)
    {
        var progress = await _service.GetConvertProgressAsync(taskId);
        return Ok(new { code = 200, data = progress });
    }

    [HttpPost("convert/cancel")]
    public async Task<IActionResult> CancelConvert([FromQuery] string queueCode)
    {
        var (ok, error) = await _service.CancelConvertAsync(queueCode);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    #endregion

    #region 阶段文件树（文档提取规则页面）

    /// <summary>
    /// 获取阶段的完整文件树（含规则属性）
    /// 用于文档提取规则管理页面
    /// </summary>
    [HttpGet("stage-files/{directoryCode}")]
    public async Task<IActionResult> GetStageFileTree(string directoryCode)
    {
        var result = await _service.GetStageFileTreeAsync(directoryCode);
        return Ok(new { code = 200, data = result });
    }

    #endregion

    #region 目录级文件查询

    /// <summary>
    /// 获取目录下所有文件（不含子文件夹中的文件）
    /// </summary>
    [HttpGet("directory-files")]
    public async Task<IActionResult> GetDirectoryFiles([FromQuery] string directoryCode)
    {
        var files = await _service.GetFilesByDirectoryAsync(directoryCode);
        return Ok(new { code = 200, data = files });
    }

    #endregion

    #region 手动创建文件记录

    /// <summary>
    /// 手动创建文件记录（不经上传流程）
    /// </summary>
    [HttpPost("folders/{folderCode}/files/create")]
    public async Task<IActionResult> CreateFile(string folderCode, [FromBody] SharedEntities::YZH.Entity.Admin.Platform.Dir.StandardDirectoryFile file)
    {
        file.FolderCode = folderCode;
        var (ok, error, result) = await _service.CreateFileAsync(file);
        return Ok(new { code = ok ? 200 : 400, msg = error, data = result });
    }

    #endregion

    #region 导出打包 ZIP

    /// <summary>
    /// 将选中的文件夹和文件打包成 ZIP
    /// </summary>
    [HttpPost("configs/{directoryCode}/export")]
    public async Task<IActionResult> ExportAsZip(string directoryCode, [FromBody] ExportRequest request)
    {
        if ((request?.FolderCodes == null || request.FolderCodes.Count == 0) &&
            (request?.FileCodes == null || request.FileCodes.Count == 0))
        {
            return BadRequest(new { code = 400, msg = "请至少选择一个文件夹或文件" });
        }

        var stream = await _service.ExportAsZipAsync(directoryCode, request.FolderCodes, request.FileCodes);
        var fileName = $"StandardDirectory_{directoryCode}_{DateTime.Now:yyyyMMddHHmmss}.zip";
        return File(stream, "application/zip", fileName);
    }

    #endregion

    #region 旧版单文件上传（兼容旧前端）

    /// <summary>
    /// 旧版单文件直接上传（兼容旧前端直接上传场景）
    /// </summary>
    [HttpPost("upload-file")]
    public async Task<IActionResult> UploadFileLegacy([FromForm] UploadFileLegacyDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return Ok(new { code = 400, msg = "请选择文件" });

        using var stream = dto.File.OpenReadStream();
        var (ok, error, file) = await _service.UploadFileLegacyAsync(
            stream, dto.File.FileName, dto.DirectoryCode, dto.FolderCode,
            dto.OrgCode, dto.StandardCode, dto.PhaseCode);
        return Ok(new { code = ok ? 200 : 400, msg = error, data = file });
    }

    #endregion

    #region 文件锁定状态

    /// <summary>
    /// 批量查询文件在运行中队列中的锁定状态
    /// </summary>
    [HttpPost("file-lock-status")]
    public async Task<IActionResult> GetFileLockStatus([FromBody] FileLockStatusRequest request)
    {
        var result = await _service.GetFileLockStatusAsync(request.FileCodes);
        return Ok(new { code = 200, data = result });
    }

    #endregion

    #region 重试失败转换

    /// <summary>
    /// 重试失败的文档转换
    /// </summary>
    [HttpPost("retry-failed-conversions")]
    public async Task<IActionResult> RetryFailedConversions()
    {
        var (ok, error, enqueued, queueCount) = await _service.RetryFailedConversionsAsync();
        return Ok(new { code = ok ? 200 : 400, msg = error, enqueued, queueCount });
    }

    #endregion
}

/// <summary>
/// 存储路径白名单校验：必须位于 standard-directory/ 下，且不含穿越片段
/// </summary>
public static partial class ControllerSafetyExtensions
{
    private static readonly string[] AllowedPrefixes = { "standard-directory/", "/standard-directory/" };

    public static bool IsAllowedStoragePath(this StandardDirectoryController _, string path)
    {
        var p = path.Replace('\\', '/').TrimStart('/');
        if (!p.StartsWith("standard-directory/", StringComparison.OrdinalIgnoreCase))
            return false;
        // 禁止穿越片段与空段
        return !p.Split('/').Any(seg => seg == ".." || seg == "." || seg.Length == 0);
    }
}

/// <summary>
/// 导出请求 DTO
/// </summary>
public class ExportRequest
{
    public List<string> FolderCodes { get; set; } = new();
    public List<string> FileCodes { get; set; } = new();
}

/// <summary>
/// 旧版上传文件 DTO
/// </summary>
public class UploadFileLegacyDto
{
    public IFormFile File { get; set; }
    public string DirectoryCode { get; set; }
    public string FolderCode { get; set; }
    public string OrgCode { get; set; }
    public string StandardCode { get; set; }
    public string PhaseCode { get; set; }
}

/// <summary>
/// 文件锁定状态查询请求 DTO
/// </summary>
public class FileLockStatusRequest
{
    public List<string> FileCodes { get; set; } = new();
}
