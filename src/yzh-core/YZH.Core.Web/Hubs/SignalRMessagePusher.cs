using Microsoft.AspNetCore.SignalR;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Push;

namespace YZH.Core.Web.Hubs;

/// <summary>
/// <see cref="IYzhMessagePusher"/> 的 SignalR 实现。
/// 用 IHubContext（非 Hub 类内）—— 可从 QueueManager 终态通知等任意后台上下文调用。
/// </summary>
public class SignalRMessagePusher : IYzhMessagePusher
{
    private readonly IHubContext<YzhMessageHub> _hubContext;

    public SignalRMessagePusher(IHubContext<YzhMessageHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public bool IsAvailable => true;

    public Task SendToUserAsync(string? userName, YzhPushMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userName)) return Task.CompletedTask;
        return SendAsync(_hubContext.Clients.Group(YzhPushProtocol.UserGroup(userName)), message, cancellationToken);
    }

    public Task SendToGroupAsync(string group, YzhPushMessage message, CancellationToken cancellationToken = default)
        => SendAsync(_hubContext.Clients.Group(group), message, cancellationToken);

    public Task SendToAllAsync(YzhPushMessage message, CancellationToken cancellationToken = default)
        => SendAsync(_hubContext.Clients.All, message, cancellationToken);

    private static Task SendAsync(IClientProxy proxy, YzhPushMessage message, CancellationToken cancellationToken)
        => proxy.SendAsync(YzhPushProtocol.ReceiveEvent, message, cancellationToken);
}
