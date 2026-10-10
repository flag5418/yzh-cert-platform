using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models.Push;

namespace YZH.Core.Web.Hubs;

/// <summary>
/// yzh 实时推送 Hub（框架核心能力）
///
/// <para>连接地址：<c>{origin}/api/yzh-msg?access_token={jwt}</c>
/// （signalr 客户端 accessTokenFactory 自动把票带在 query —— WebSocket 握手带不了 Authorization 头）。</para>
///
/// <para>★ 身份：OnConnectedAsync 用 JwtHelper 验票 → 从 ClaimTypes.Name 取登录名进组。
/// 组名只来自已验证 token，⛔ 不接受客户端自报身份
/// （旧架构 HomePageMessageHub 的 <c>?userName=</c> 可伪造任意用户收消息 —— 此处已修）。</para>
///
/// <para>服务端 → 客户端唯一事件：<see cref="YzhPushProtocol.ReceiveEvent"/>（消息体 <see cref="YzhPushMessage"/>）。</para>
/// </summary>
public class YzhMessageHub : Hub
{
    private readonly JwtHelper _jwtHelper;

    public YzhMessageHub(JwtHelper jwtHelper)
    {
        _jwtHelper = jwtHelper;
    }

    public override async Task OnConnectedAsync()
    {
        // 票的两种携带方式：① 浏览器 WebSocket 无法带自定义头 → signalr 走 ?access_token=（主路径）
        // ② Node/非浏览器客户端走 Authorization: Bearer 头 —— 一并接受（都经 ValidateToken 验签）
        var http = Context.GetHttpContext();
        var token = http?.Request.Query["access_token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token) && http?.Request.Headers.Authorization.Count > 0)
            token = http.Request.Headers.Authorization.ToString();

        var principal = string.IsNullOrEmpty(token) ? null : _jwtHelper.ValidateToken(token);
        var userName = principal?.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(userName))
        {
            // 无票 / 票无效：直接掐断，不给进任何分组
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, YzhPushProtocol.UserGroup(userName));
        await base.OnConnectedAsync();
    }
}
