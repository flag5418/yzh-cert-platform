using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YZH.Core.Api.Attributes;
using YZH.Core.Stand.Helpers;

namespace YZH.Core.Api.Filters;

/// <summary>
///     YZH 认证过滤器（SSO 校验 + Token 续租）
///     
///     执行时机：Authorization 阶段
///     功能链路：
///     1. [YZHAnonymous] → 跳过，Mock 用户已由特性注入
///     2. SSO 版本校验（JWT.ver vs Redis.ver）
///     3. Token 续租（剩余有效期 < 30min 时签发新 Token）
///     4. 新 Token 写入 Response Header "X-New-Token"
/// </summary>
public class YzhAuthFilter : IAsyncAuthorizationFilter
{
    private readonly JwtHelper _jwt;
    private readonly TokenVersionService _tokenVersion;

    // 续租阈值：Token 剩余有效期 < 30 分钟时签发新 Token
    private static readonly TimeSpan RenewThreshold = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan NewTokenExpiration = TimeSpan.FromDays(7);

    public YzhAuthFilter(JwtHelper jwt, TokenVersionService tokenVersion)
    {
        _jwt = jwt;
        _tokenVersion = tokenVersion;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 跳过标记了 [YZHAnonymous] 的端点
        var hasAnonymous = context.ActionDescriptor.EndpointMetadata
            .Any(m => m is YZHAnonymousAttribute);
        if (hasAnonymous)
            return;

        var userCode = context.HttpContext.User.FindFirst("code")?.Value;

        // 无 Token 或解析失败 → 由框架 Authorization 中间件返回 401
        if (string.IsNullOrEmpty(userCode))
            return;

        // 匿名 Mock 用户 → 跳过 SSO 校验
        if (userCode == "anonymous")
            return;

        // SSO 版本校验
        var tokenVersion = context.HttpContext.User.FindFirst("ver")?.Value;
        if (!string.IsNullOrEmpty(tokenVersion))
        {
            var isValid = await _tokenVersion.ValidateVersionAsync(userCode, tokenVersion);
            if (!isValid)
            {
                context.Result = new JsonResult(new SSOExpiredResponse
                {
                    Success = false,
                    Message = "您的账号已在其他设备登录，请重新登录",
                    Code = 4011,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                })
                {
                    StatusCode = 401
                };
                return;
            }

            // Token 续租判断
            await TryRenewToken(context, userCode);
        }
    }

    /// <summary>尝试续租 Token</summary>
    private async Task TryRenewToken(AuthorizationFilterContext context, string userCode)
    {
        try
        {
            var expClaim = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (expClaim == null) return;

            var expTime = long.Parse(expClaim);
            var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expTime).UtcDateTime;
            var remaining = expDateTime - DateTime.UtcNow;

            if (remaining < RenewThreshold && remaining > TimeSpan.Zero)
            {
                var userName = context.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "";
                var roles = context.HttpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value);
                var tokenVer = context.HttpContext.User.FindFirst("ver")?.Value ?? "";

                var newToken = _jwt.RenewToken(userCode, userName, roles, tokenVer, NewTokenExpiration);
                context.HttpContext.Response.Headers["X-New-Token"] = newToken;
            }
        }
        catch
        {
            // 续租失败不影响当前请求
        }
    }
}

/// <summary>SSO 过期响应体</summary>
public class SSOExpiredResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int Code { get; set; }
    public long Timestamp { get; set; }
}

