using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     用户上下文实现 — 基于 IHttpContextAccessor
///     获取当前请求的 IP 地址、用户信息
/// 
///     IP 获取逻辑对标 Vol 框架的 HttpContextExtension.GetUserIp():
///     1. 先取 X-Real-IP（Nginx/反向代理设置的真实 IP）
///     2. 再取 X-Forwarded-For（多级代理场景）
///     3. 最终回退到 Connection.RemoteIpAddress
/// </summary>
public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpContext? _httpContext;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpContext = httpContextAccessor.HttpContext;
    }

    public string UserId => _httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";

    public string UserCode => _httpContext?.User?.FindFirst("code")?.Value ?? "";

    public string UserName => _httpContext?.User?.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";

    public string? UserTrueName => _httpContext?.User?.FindFirst("UserTrueName")?.Value
        ?? _httpContext?.User?.FindFirst(ClaimTypes.GivenName)?.Value;

    /// <summary>
    ///     获取客户端 IP 地址
    ///     优先级：X-Real-IP > X-Forwarded-For > RemoteIpAddress
    /// </summary>
    public string ClientIp
    {
        get
        {
            if (_httpContext == null) return "0.0.0.0";

            try
            {
                var remoteIpAddress = _httpContext.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";

                // 优先 X-Real-IP（Nginx 反向代理设置）
                if (_httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
                {
                    var ip = realIp.ToString();
                    if (!string.IsNullOrEmpty(ip) && ip != remoteIpAddress)
                        return ip;
                }

                // 其次 X-Forwarded-For（多级代理场景，取第一个 IP）
                if (_httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                {
                    var forwarded = forwardedFor.ToString();
                    if (!string.IsNullOrEmpty(forwarded))
                    {
                        // X-Forwarded-For 可能包含多个 IP（逗号分隔），取第一个
                        var ip = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
                        if (!string.IsNullOrEmpty(ip))
                            return ip;
                    }
                }

                // 回退到直接连接 IP
                return remoteIpAddress == "::1" ? "127.0.0.1" : remoteIpAddress;
            }
            catch
            {
                return "0.0.0.0";
            }
        }
    }

    public string? UserAgent => _httpContext?.Request?.Headers["User-Agent"].ToString();

    public bool IsAuthenticated => _httpContext?.User?.Identity?.IsAuthenticated ?? false;

    public IEnumerable<string> GetRoleCodes()
    {
        return _httpContext?.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    ///     获取完整请求上下文（用于审计日志）
    /// </summary>
    public RequestContext GetRequestContext()
    {
        return new RequestContext
        {
            UserId = UserId,
            UserName = UserName,
            ClientIp = ClientIp,
            UserAgent = UserAgent,
            RequestPath = _httpContext?.Request?.Path.Value ?? "",
            RequestMethod = _httpContext?.Request?.Method ?? "",
            Timestamp = DateTime.UtcNow
        };
    }
}
