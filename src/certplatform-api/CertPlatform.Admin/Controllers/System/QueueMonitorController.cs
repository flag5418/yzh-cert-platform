using Microsoft.AspNetCore.Mvc;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Controllers.System;

/// <summary>
/// 队列监控控制器
/// 路由前缀：/api/System/QueueMonitor
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
public class QueueMonitorController : ControllerBase
{
    private readonly QueueManager _queueManager;

    public QueueMonitorController(QueueManager queueManager)
    {
        _queueManager = queueManager;
    }

    [HttpPost("list")]
    public async Task<IActionResult> GetList([FromBody] QueueListRequest req)
    {
        var result = await _queueManager.GetQueueListAsync(
            req.Type, req.Status, req.StartTime, req.EndTime, req.Page, req.Rows);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    [HttpPost("detail")]
    public async Task<IActionResult> GetDetail([FromBody] QueueDetailRequest req)
    {
        var result = await _queueManager.GetQueueDetailAsync(req.QueueCode);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] QueueCancelRequest req)
    {
        var (ok, error) = await _queueManager.CancelQueueAsync(req.QueueCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry([FromBody] QueueRetryRequest req)
    {
        var (ok, error) = await _queueManager.RetryQueueAsync(req.QueueCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("task/retry")]
    public async Task<IActionResult> RetryTask([FromBody] TaskRetryRequest req)
    {
        var (ok, error) = await _queueManager.RetryTaskAsync(req.TaskCode);
        return Ok(ok ? ApiResponse<object?>.Ok() : ApiResponse<object?>.Fail(error));
    }

    [HttpPost("status")]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _queueManager.GetQueueStatsAsync();
        return Ok(ApiResponse<object?>.Ok(data: result));
    }

    [HttpPost("resource/locked")]
    public async Task<IActionResult> GetResourceLocked([FromBody] ResourceLockRequest req)
    {
        var result = await _queueManager.FindResourceLockAsync(req.ResourceTable, req.ResourceCodes);
        return Ok(ApiResponse<object?>.Ok(data: result));
    }
}

#region 请求 DTO

public class QueueListRequest
{
    public string? Type { get; set; }
    public string? Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 20;
}

public class QueueDetailRequest
{
    public string QueueCode { get; set; } = "";
}

public class QueueCancelRequest
{
    public string QueueCode { get; set; } = "";
}

public class QueueRetryRequest
{
    public string QueueCode { get; set; } = "";
}

public class TaskRetryRequest
{
    public string TaskCode { get; set; } = "";
}

public class ResourceLockRequest
{
    public string ResourceTable { get; set; } = "";
    public List<string> ResourceCodes { get; set; } = new();
}

#endregion
