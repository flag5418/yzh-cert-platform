using YZH.Core.Stand.Models.Push;

namespace YZH.Core.Stand.Interfaces;

/// <summary>
/// 消息推送抽象（yzh 框架核心能力：服务端 → 浏览器实时通道）
///
/// <para>★ 能力边界（与 IYzhQueueNotifier 同一分层原则）：本接口只负责「把消息送出去」；
/// 消息存哪张业务表（如 cert_message）、消息文案由业务侧决定 —— 业务表不进框架。</para>
///
/// <para>★ 目标标识 = 登录名（UserName），⛔ 不是 UserCode —— 因为三处必须同源：
/// ① <c>yzh_queue.CreateBy</c> 由 QueueManager 写入的就是创建者登录名
/// （CreateQueueAsync: <c>CreateBy = req.UserName</c>）；
/// ② Hub 侧从已验证 JWT 的 <c>ClaimTypes.Name</c> 取到的也是登录名；
/// ③ 旧架构 HomePageMessageHub 也按登录名分组。
/// ⚠️ 这里的 userName 是**传输路由键**（连接分组），不是业务键 —— 不受双关键字准则约束；
/// 组名只由服务端从已验证 token 派生，客户端无法自报（旧架构 <c>?userName=</c> 伪造漏洞已堵）。</para>
///
/// <para>实现：YZH.Core.Web 的 SignalRMessagePusher（SignalR）。
/// 调用方应自行兜底：推送失败只记日志，绝不允许影响主流程。</para>
/// </summary>
public interface IYzhMessagePusher
{
    /// <summary>推送通道是否可用（SignalR 实现恒 true；替换实现可表达不可用）</summary>
    bool IsAvailable { get; }

    /// <summary>推给单个用户（按登录名路由到 user:{userName} 分组；userName 空则静默跳过）</summary>
    Task SendToUserAsync(string? userName, YzhPushMessage message, CancellationToken cancellationToken = default);

    /// <summary>推给指定分组</summary>
    Task SendToGroupAsync(string group, YzhPushMessage message, CancellationToken cancellationToken = default);

    /// <summary>广播给全部在线连接</summary>
    Task SendToAllAsync(YzhPushMessage message, CancellationToken cancellationToken = default);
}
