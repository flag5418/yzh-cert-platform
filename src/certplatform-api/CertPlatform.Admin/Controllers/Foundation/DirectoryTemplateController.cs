
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Shared.Entities.Cert;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
/// 目录模板管理控制器
/// 路由前缀：/api/Foundation/DirectoryTemplate
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class DirectoryTemplateController : ControllerBase
{
    private readonly DirectoryTemplateService _service;

    public DirectoryTemplateController(DirectoryTemplateService service)
    {
        _service = service;
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] string configCode)
    {
        var tree = await _service.GetTreeAsync(configCode);
        return Ok(new { code = 200, data = tree });
    }

    [HttpPost("addFolder")]
    public async Task<IActionResult> AddFolder([FromBody] DirectoryTemplate folder)
    {
        var (ok, error, result) = await _service.AddFolderAsync(folder);
        return Ok(new { code = ok ? 200 : 400, msg = error, data = result });
    }

    [HttpPost("updateFolder")]
    public async Task<IActionResult> UpdateFolder([FromBody] DirectoryTemplate folder)
    {
        var (ok, error) = await _service.UpdateFolderAsync(folder);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    [HttpPost("deleteFolder")]
    public async Task<IActionResult> DeleteFolder([FromQuery] string code)
    {
        var (ok, error) = await _service.DeleteFolderAsync(code);
        return Ok(new { code = ok ? 200 : 400, msg = error });
    }

    [HttpPost("uploadTemplateFile")]
    public async Task<IActionResult> UploadTemplateFile(IFormFile file, [FromQuery] string configCode)
    {
        if (file == null || file.Length == 0)
            return Ok(new { code = 400, msg = "请选择文件" });

        using var stream = file.OpenReadStream();
        var (ok, error, storagePath) = await _service.UploadTemplateFileAsync(stream, file.FileName, configCode);
        return Ok(new { code = ok ? 200 : 400, msg = error, data = storagePath });
    }

    [HttpGet("downloadTemplateFile")]
    public async Task<IActionResult> DownloadTemplateFile([FromQuery] string storagePath)
    {
        var result = await _service.DownloadTemplateFileAsync(storagePath);
        if (result == null)
            return Ok(ApiResponse.Fail("文件不存在"));

        var (stream, contentType) = result.Value;
        var fileName = global::System.IO.Path.GetFileName(storagePath.Replace('\\', '/'));
        return File(stream, contentType, fileName);
    }

    [HttpPost("deleteTemplateFile")]
    public async Task<IActionResult> DeleteTemplateFile([FromQuery] string storagePath)
    {
        var ok = await _service.DeleteTemplateFileAsync(storagePath);
        return Ok(new { code = ok ? 200 : 400 });
    }

    [HttpPost("renameTemplateFile")]
    public async Task<IActionResult> RenameTemplateFile([FromQuery] string oldPath, [FromQuery] string newPath)
    {
        var ok = await _service.RenameTemplateFileAsync(oldPath, newPath);
        return Ok(new { code = ok ? 200 : 400 });
    }
}
