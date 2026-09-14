extern alias SharedEntities;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
}
