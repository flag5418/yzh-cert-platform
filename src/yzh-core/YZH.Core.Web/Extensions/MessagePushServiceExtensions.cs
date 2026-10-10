using Microsoft.Extensions.DependencyInjection;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Web.Hubs;

namespace YZH.Core.Web.Extensions;

/// <summary>
/// 实时推送 DI 注册扩展（与 AddYzhQueue 同一层级的框架能力）
/// </summary>
public static class MessagePushServiceExtensions
{
    /// <summary>
    /// 注册 SignalR 与 <see cref="IYzhMessagePusher"/>。
    /// <para>Hub 端点由宿主显式挂载：<c>app.MapHub&lt;YzhMessageHub&gt;("/api/yzh-msg")</c>（Program.cs）。</para>
    /// <para>⚠️ SignalR 的 JSON 协议保持默认 camelCase —— 推送信封契约（E1 同口径）依赖它，
    /// ⛔ 不要在这里 AddJsonProtocol 改成 PascalCase。</para>
    /// </summary>
    public static IServiceCollection AddYzhMessagePush(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IYzhMessagePusher, SignalRMessagePusher>();
        return services;
    }
}
