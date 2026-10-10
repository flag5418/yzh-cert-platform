using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.Services.Sys;
using Microsoft.Extensions.Logging.Abstractions;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Push;
using YZH.Core.Stand.Models.Result;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CertPlatform.Admin.Controllers.System;

/// <summary>
/// 站内消息控制器（读操作 + 已读标记 + 强制下线推送）。
/// 路由前缀：/api/Admin/System/Message
/// ApiCode 与旧架构 /api/message/* 一致（末段都是 Message）——角色-接口关联不断裂。
/// </summary>
[ApiController]
[Route("api/Admin/System/[controller]")]
public class MessageController : ControllerBase
{
    private readonly CertMessageService _messageService;
    private readonly IYzhMessagePusher _pusher;
    private readonly ILogger<MessageController> _logger;

    public MessageController(
        CertMessageService messageService,
        IYzhMessagePusher pusher,
        ILogger<MessageController> logger)
    {
        _messageService = messageService;
        _pusher = pusher;
        _logger = logger;
    }

    /// <summary>未读消息数</summary>
    [HttpPost("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userCode = GetUserCode();
        if (userCode == null) return Ok(ApiResponse<object?>.Ok(data: 0));
        var count = await _messageService.GetUnreadCountByUserCodeAsync(userCode);
        return Ok(ApiResponse<object?>.Ok(data: count));
    }

    /// <summary>消息列表（分页，可按 unreadOnly 过滤）</summary>
    [HttpPost("list")]
    public async Task<IActionResult> GetList([FromBody] MessageListRequest req)
    {
        var userCode = GetUserCode();
        if (userCode == null) return Ok(ApiResponse<List<CertMessageService.MessageItem>>.Ok(data: []));

        var page = Math.Max(1, req?.Page ?? 1);
        var pageSize = Math.Min(100, Math.Max(1, req?.PageSize ?? 20));
        var list = await _messageService.GetListByUserCodeAsync(
            userCode, page, pageSize, req?.UnreadOnly == true);
        return Ok(ApiResponse<List<CertMessageService.MessageItem>>.Ok(data: list));
    }

    /// <summary>单条标记已读（按 Code 业务键）</summary>
    [HttpPost("read/{code}")]
    public async Task<IActionResult> MarkRead(string code)
    {
        var ok = await _messageService.MarkReadAsync(code);
        return Ok(ApiResponse<object?>.Ok(data: ok));
    }

    /// <summary>批量标记已读（可选按 MessageType 过滤）</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead([FromBody] MessageMarkAllRequest? req = null)
    {
        var userCode = GetUserCode();
        if (userCode == null) return Ok(ApiResponse<object?>.Ok());
        var affected = await _messageService.MarkAllReadAsync(userCode, req?.MessageType);
        return Ok(ApiResponse<object?>.Ok(data: affected));
    }

    /// <summary>
    /// 强制下线指定用户（按登录名推送 logout 事件，前端收到后弹窗确认并跳转登录页）。
    /// 同时落一条 system 类型消息记录（IsRead=0），进入消息中心可见。
    /// </summary>
    [HttpPost("force-logout")]
    public async Task<IActionResult> ForceLogout([FromBody] ForceLogoutRequest req)
    {
        var loginName = req?.LoginName?.Trim();
        if (string.IsNullOrWhiteSpace(loginName))
            return Ok(ApiResponse<object?>.Fail("登录名不能为空"));

        // 落库（收件人 = 被强制下线者，Admin 操作留痕）
        var actedBy = GetUserCode() ?? "system";
        await _messageService.CreateForLoginAsync(
            loginName,
            "系统强制下线",
            req?.Reason ?? "管理员强制下线",
            "system",
            JsonSerializer.Serialize(new { actedBy, reason = req?.Reason }));

        // SignalR 推送（旁路：失败只记日志，不影响主流程）
        try
        {
            await _pusher.SendToUserAsync(loginName, new YzhPushMessage
            {
                Value = YzhPushValues.Logout,
                Title = "系统通知",
                Message = req?.Reason ?? "您的账号已在其他设备登录，当前会话已失效。",
            });
        }
        catch (Exception ex)
        {
            // 推送失败不影响落库，静默记录
            (_logger as ILogger<MessageController>)?.LogWarning(
                ex, "force-logout 推送失败: {LoginName}", loginName);
        }

        return Ok(ApiResponse<object?>.Ok());
    }

    private string? GetUserCode()
    {
        // JWT claim "code" = Sys_User.Code（业务编码）；由 JwtHelper.GenerateToken 写入
        return HttpContext.User.FindFirst("code")?.Value;
    }
}

public class MessageListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool UnreadOnly { get; set; }
}

public class MessageMarkAllRequest
{
    /// <summary>按类型过滤（null/空 = 全部类型）；队列汇总弹窗传入 "queue"</summary>
    public string? MessageType { get; set; }
}

public class ForceLogoutRequest
{
    /// <summary>目标用户登录名（Sys_User.UserName）</summary>
    public string? LoginName { get; set; }

    /// <summary>下线原因（展示给用户 + 落库）</summary>
    public string? Reason { get; set; }
}
