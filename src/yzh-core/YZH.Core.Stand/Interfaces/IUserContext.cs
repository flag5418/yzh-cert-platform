namespace YZH.Core.Stand.Interfaces;

/// <summary>
///     用户上下文接口 - 提供当前请求的用户信息、IP 地址
/// </summary>
public interface IUserContext
{
    /// <summary>用户 ID（GUID 主键，旧版兼容）</summary>
    string UserId { get; }

    /// <summary>用户 Code（业务编码，新版核心关联字段）</summary>
    string UserCode { get; }

    /// <summary>用户名称</summary>
    string UserName { get; }

    /// <summary>用户真实姓名</summary>
    string? UserTrueName { get; }

    /// <summary>客户端 IP 地址</summary>
    string ClientIp { get; }

    /// <summary>User-Agent</summary>
    string? UserAgent { get; }

    /// <summary>是否已认证</summary>
    bool IsAuthenticated { get; }

    /// <summary>获取用户角色 Code 列表</summary>
    IEnumerable<string> GetRoleCodes();

    /// <summary>获取完整请求上下文（用于审计日志）</summary>
    RequestContext GetRequestContext();

    /// <summary>是否匿名用户（开发模式 Mock 用户）</summary>
    bool IsAnonymous { get; }

    /// <summary>角色 ID</summary>
    int RoleId { get; }
}

/// <summary>
///     请求上下文信息（用于审计日志）
/// </summary>
public class RequestContext
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string RequestPath { get; set; } = string.Empty;
    public string RequestMethod { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
