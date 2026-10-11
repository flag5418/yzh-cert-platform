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
/// <para>★ 身份：OnConnectedAsync 用 JwtHelper 验票 → 取 <c>code</c> claim（= <c>Sys_User.Code</c>）进组。
/// 组名只来自已验证 token，⛔ 不接受客户端自报身份
/// （旧架构 HomePageMessageHub 的 <c>?userName=</c> 可伪造任意用户收消息 —— 此处已修）。</para>
///
/// <para>★★ <b>2026-10-11 分组键由登录名改为用户 Code</b>：<c>CreateQueueAsync</c> 已按契约把
/// <c>CreateBy</c> 写成 <c>Sys_User.Code</c>，推送方（<c>IYzhMessagePusher.SendToUserAsync</c>）
/// 也按 Code 寻址 ⇒ Hub 必须同键，否则消息进一个<b>没人在里面</b>的分组且<b>不报错</b>。
/// ⛔ 不要再改回 <c>ClaimTypes.Name</c>（登录名）。</para>
///
/// <para>⚠️ <b>取不到 <c>code</c> claim 时直接掐断（不回落登录名）</b>：回落会造成「进组了但没人按这个名字发」
/// —— 静默收不到比连不上更难排查。缺失只可能是**旧版 token**（<c>code</c> claim 早于本次改造即有，
/// 见 <c>JwtHelper.GenerateToken</c>）⇒ 重新登录即可。</para>
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
        var userCode = principal?.FindFirst("code")?.Value;
        if (string.IsNullOrEmpty(userCode))
        {
            // 无票 / 票无效 / 票缺 code claim：直接掐断，不给进任何分组
            // （⛔ 不回落登录名 —— 见类注释：回落 = 进一个没人发的组，静默收不到）
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, YzhPushProtocol.UserGroup(userCode));
        await base.OnConnectedAsync();
    }
}
