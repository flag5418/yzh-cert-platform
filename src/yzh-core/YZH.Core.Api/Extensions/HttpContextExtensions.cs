using Microsoft.AspNetCore.Http;

namespace YZH.Core.Api.Extensions;

/// <summary>
///     HttpContext 扩展方法
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    ///     获取客户端 IP 地址
    ///     优先级：X-Real-IP > X-Forwarded-For > RemoteIpAddress
    ///     对标 Vol 框架的 HttpContextExtension.GetUserIp()
    /// </summary>
    public static string GetClientIp(this HttpContext httpContext)
    {
        if (httpContext == null) return "0.0.0.0";

        try
        {
            var remoteIpAddress = httpContext.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            // 优先 X-Real-IP（Nginx 反向代理设置）
            if (httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                var ip = realIp.ToString();
                if (!string.IsNullOrEmpty(ip) && ip != remoteIpAddress)
                    return ip;
            }

            // 其次 X-Forwarded-For（多级代理场景，取第一个 IP）
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var forwarded = forwardedFor.ToString();
                if (!string.IsNullOrEmpty(forwarded))
                {
                    var ip = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
                    if (!string.IsNullOrEmpty(ip))
                        return ip;
                }
            }

            // 回退到直接连接 IP（::1 转 127.0.0.1）
            return remoteIpAddress == "::1" ? "127.0.0.1" : remoteIpAddress;
        }
        catch
        {
            return "0.0.0.0";
        }
    }
}
