using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Services;
using YZH.Core.Api.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     接口同步控制器
///     负责触发接口扫描同步、获取接口列表
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Route("api/[controller]")]
[Authorize]
public class ApiSyncController : ControllerBase
{
    private readonly ApiScanner _scanner;
    private readonly ApiSyncService _syncService;
    private readonly IApiRepository _apiRepo;

    public ApiSyncController(
        ApiScanner scanner,
        ApiSyncService syncService,
        IApiRepository apiRepo)
    {
        _scanner = scanner;
        _syncService = syncService;
        _apiRepo = apiRepo;
    }

    /// <summary>
    /// 获取所有接口列表
    /// </summary>
    [ApiDescription("获取接口列表", "系统", "系统管理/接口权限", true)]
    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<object>>> GetList()
    {
        var apis = await _apiRepo.GetAllAsync();
        return Ok(ApiResponse<object>.Ok(apis));
    }

    /// <summary>
    /// 触发接口扫描并同步到数据库
    /// </summary>
    [ApiDescription("同步接口", "系统", "系统管理/接口权限", true)]
    [HttpPost("sync")]
    public async Task<ActionResult<ApiResponse<object>>> Sync()
    {
        try
        {
            // 1. 扫描所有接口
            var discoveredApis = _scanner.Scan();

            // 2. 同步到数据库（增量更新 + 清理无效记录）
            var result = await _syncService.SyncAsync(discoveredApis);

            if (!result.Success)
                return BadRequest(ApiResponse<object>.Fail(result.Error ?? "同步失败"));

            return Ok(ApiResponse<object>.Ok(new
            {
                Added = result.Added,
                Updated = result.Updated,
                Deleted = result.Deleted,
                Total = discoveredApis.Count
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"同步失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 扫描接口（不保存，仅返回扫描结果）
    /// </summary>
    [ApiDescription("扫描接口", "系统", "系统管理/接口权限", true)]
    [HttpGet("scan")]
    public ActionResult<ApiResponse<object>> Scan()
    {
        try
        {
            var apis = _scanner.Scan();
            return Ok(ApiResponse<object>.Ok(new
            {
                Total = apis.Count,
                Apis = apis.Select(a => new
                {
                    a.ApiCode,
                    a.ControllerName,
                    a.ActionName,
                    a.Method,
                    a.Path,
                    a.Description,
                    a.Author,
                    a.TreePath
                })
            }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail($"扫描失败：{ex.Message}"));
        }
    }
}
