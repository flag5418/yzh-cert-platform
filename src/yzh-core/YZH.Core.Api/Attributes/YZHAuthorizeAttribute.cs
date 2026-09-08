using Microsoft.AspNetCore.Authorization;

namespace YZH.Core.Api.Attributes;

/// <summary>
///     YZH 强认证特性（默认行为）
///     
///     功能：
///     1. 要求用户登录（基于 JWT Token）
///     2. Token 自动续租（滑动过期，临近过期时签发新 Token）
///     3. SSO 挤号校验（同账号新登录后旧 Token 全部失效）
///     
///     使用方式：
///     - 不标记任何特性 → 自动继承 YZHAuthorize（通过 Program.cs 配置默认策略）
///     - 显式标记 [YZHAuthorize] → 强制认证
///     - 标记 [YZHAnonymous] → 免认证（仅开发用）
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class YZHAuthorizeAttribute : AuthorizeAttribute
{
    public YZHAuthorizeAttribute()
    {
        AuthenticationSchemes = "Bearer";
        Policy = "YZHAuthorizePolicy";
    }
}
