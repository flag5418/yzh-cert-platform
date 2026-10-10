namespace YZH.Core.Stand.Models.Push;

/// <summary>
/// 推送消息体（服务端 → 浏览器信封）
///
/// <para>★ 字段契约沿用旧架构 HomePageMessage 形状（title/message/date/value/data），
/// 前端 yzhPush 按此消费。</para>
///
/// <para>★ 序列化口径 = **camelCase 信封例外**：SignalR 默认 JsonHubProtocol 输出
/// <c>{title, message, date, value, data}</c> —— 与 ApiResponse 的 E1 例外同源
/// （信封字段不走 PascalCase 逐字一致铁律；业务数据放在 Data 内随匿名对象属性名输出）。
/// ⛔ 不要给本类配 PascalCase 序列化「为了统一」—— 会打断前端 yzhPush 与旧契约的兼容。</para>
/// </summary>
public class YzhPushMessage
{
    /// <summary>标题（通知弹窗标题）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>正文</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>产生时间</summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>事件类型（见 <see cref="YzhPushValues"/>）—— 前端按此分发</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>业务载荷（结构随 Value 而定）</summary>
    public object? Data { get; set; }
}

/// <summary>
/// 推送 Value 取值（事件类型常量）
/// </summary>
public static class YzhPushValues
{
    /// <summary>队列进入终态
    /// data = {queueCode, queueType, status, totalCount, completedCount, failedCount, cancelledCount, scopeKey}</summary>
    public const string QueueProgress = "queue_progress";

    /// <summary>强制下线（旧架构同名 value；前端收到即清 token 跳登录，不弹通知）</summary>
    public const string Logout = "logout";
}

/// <summary>
/// 推送协议常量（分组名 / Hub 事件名）—— Hub 与 Pusher 两侧共用，防手抄分叉
/// </summary>
public static class YzhPushProtocol
{
    /// <summary>服务端 → 客户端的唯一 Hub 事件名</summary>
    public const string ReceiveEvent = "ReceiveYzhMessage";

    /// <summary>用户分组名（⛔ 只由服务端从已验证 JWT 派生，不接受客户端自报）</summary>
    public static string UserGroup(string userName) => $"user:{userName}";
}
