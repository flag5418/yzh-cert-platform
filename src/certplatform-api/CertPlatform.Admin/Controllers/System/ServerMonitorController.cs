using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Services.Monitoring;

namespace CertPlatform.Admin.Controllers.System;

/// <summary>
/// 服务监控控制器
/// <para>路由前缀：<see cref="Route"/>  = <c>api/System/ServerMonitor</c></para>
/// <para>读取当前运行机器（macOS 宿主机）的 CPU / 内存 / 磁盘 / 网络 / OS 信息。</para>
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/System/[controller]")]
public class ServerMonitorController : WebControllerBase
{
    private readonly ServerMonitorService _service;

    public ServerMonitorController(ServerMonitorService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取系统资源监控快照
    /// <para>GET /api/System/ServerMonitor/overview</para>
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
    {
        var data = await _service.GetMetricsAsync();
        return Ok(ApiResponse<ServerMonitorDto>.Ok(data));
    }
}
