using Microsoft.AspNetCore.SignalR;
using YZH.Core.Stand.Models.Push;

namespace YZH.Core.Web.Hubs;

/// <summary>
/// 上传进度 SignalR Hub（yzh 框架核心能力）
///
/// <para>路由：<c>/api/yzh-upload</c>（Program.cs 显式挂载）。</para>
///
/// <para>分组命名：客户端连接后调用 <c>SubscribeAsync(taskId)</c> 进组 <c>upload:{taskId}</c>，
/// 服务端上传进度事件触发时调用 <c>BroadcastProgressAsync(taskId, data)</c> 推送。</para>
///
/// <para>生命周期：前端在上传开始时 subscribe、完成后 unsubscribe；
/// Hub 本身无状态，不维护任务列表。</para>
/// </summary>
public class UploadProgressHub : Hub
{
    /// <summary>前端订阅某 taskId 的进度推送；服务端进组名 upload:{taskId}</summary>
    public async Task SubscribeAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"upload:{taskId}");
    }

    /// <summary>前端取消订阅（上传完成/失败/取消后清理）</summary>
    public async Task UnsubscribeAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"upload:{taskId}");
    }

    /// <summary>服务端广播某 taskId 的进度数据（由业务控制器调用）</summary>
    public async Task BroadcastProgressAsync(string taskId, YzhPushMessage data)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return;
        await Clients.Group($"upload:{taskId}").SendAsync(YzhPushProtocol.ReceiveEvent, data);
    }
}
