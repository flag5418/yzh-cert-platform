
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Admin.Entities.Cert;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
/// 目录模板管理控制器
/// 路由前缀：/api/Foundation/DirectoryTemplate
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Foundation/[controller]")]
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
        return Ok(ApiResponse<object?>.Ok(data: tree));
    }

    [HttpPost("addFolder")]
    public async Task<IActionResult> AddFolder([FromBody] DirectoryTemplate folder)
    {
        var (ok, error, result) = await _service.AddFolderAsync(folder);
        return Ok(ok ? ApiResponse<object?>.Ok(data: result) : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("updateFolder")]
    public async Task<IActionResult> UpdateFolder([FromBody] DirectoryTemplate folder)
    {
        var (ok, error) = await _service.UpdateFolderAsync(folder);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("deleteFolder")]
    public async Task<IActionResult> DeleteFolder([FromQuery] string code)
    {
        var (ok, error) = await _service.DeleteFolderAsync(code);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("uploadTemplateFile")]
    public async Task<IActionResult> UploadTemplateFile(IFormFile file, [FromQuery] string configCode)
    {
        if (file == null || file.Length == 0)
            return Ok(ApiResponse<object?>.Fail("请选择文件"));

        using var stream = file.OpenReadStream();
        var (ok, error, storagePath) = await _service.UploadTemplateFileAsync(stream, file.FileName, configCode);
        return Ok(ok ? ApiResponse<object?>.Ok(data: storagePath) : ApiResponse<object?>.Fail(error));
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
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail("操作失败"));
    }

    [HttpPost("renameTemplateFile")]
    public async Task<IActionResult> RenameTemplateFile([FromQuery] string oldPath, [FromQuery] string newPath)
    {
        var ok = await _service.RenameTemplateFileAsync(oldPath, newPath);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail("操作失败"));
    }
}
