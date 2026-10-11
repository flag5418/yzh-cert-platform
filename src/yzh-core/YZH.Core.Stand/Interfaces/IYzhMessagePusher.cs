using YZH.Core.Stand.Models.Push;

namespace YZH.Core.Stand.Interfaces;

/// <summary>
/// 消息推送抽象（yzh 框架核心能力：服务端 → 浏览器实时通道）
///
/// <para>★ 能力边界（与 IYzhQueueNotifier 同一分层原则）：本接口只负责「把消息送出去」；
/// 消息存哪张业务表（如 cert_message）、消息文案由业务侧决定 —— 业务表不进框架。</para>
///
/// <para>★★ <b>目标标识 = 用户 Code（<c>Sys_User.Code</c>）</b> —— 2026-10-11 由登录名统一改为 Code，
/// 与全仓 <c>CreateBy</c> / <c>IUserContext.UserCode</c> <b>同键</b>（口径见 AGENTS.md 准则 A 附则）。
/// 三处必须同源，否则推送会进一个「没人在里面」的分组且<b>不报错</b>：
/// ① <c>yzh_queue.CreateBy</c> 由 QueueManager 写的就是创建者 Code
/// （CreateQueueAsync: <c>CreateBy = req.UserCode</c>）；
/// ② Hub 侧从已验证 JWT 的 <c>code</c> claim（= UserCode）取；
/// ③ 业务侧落库（<c>cert_message.UserCode</c>）本来就是 Code。</para>
/// <para>⚠️ 这里的 userCode 是**传输路由键**（连接分组），不是业务外键 —— 不受双关键字准则约束；
/// 组名只由服务端从已验证 token 派生，客户端无法自报（旧架构 <c>?userName=</c> 伪造漏洞已堵）。
/// ⛔ 不要再改回按登录名分组：登录名可变、可重名歧义，而 Code 是与业务表唯一对齐的键。</para>
///
/// <para>实现：YZH.Core.Web 的 SignalRMessagePusher（SignalR）。
/// 调用方应自行兜底：推送失败只记日志，绝不允许影响主流程。</para>
/// </summary>
public interface IYzhMessagePusher
{
    /// <summary>推送通道是否可用（SignalR 实现恒 true；替换实现可表达不可用）</summary>
    bool IsAvailable { get; }

    /// <summary>推给单个用户（按<b>用户 Code</b>路由到 user:{userCode} 分组；userCode 空则静默跳过）</summary>
    Task SendToUserAsync(string? userCode, YzhPushMessage message, CancellationToken cancellationToken = default);

    /// <summary>推给指定分组</summary>
    Task SendToGroupAsync(string group, YzhPushMessage message, CancellationToken cancellationToken = default);

    /// <summary>广播给全部在线连接</summary>
    Task SendToAllAsync(YzhPushMessage message, CancellationToken cancellationToken = default);
}
