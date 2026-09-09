using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace YZH.Core.Api.Attributes;

/// <summary>
///     YZH 免认证特性（临时测试用）
///     
///     功能：
///     - 标记此特性的方法/方法所属类无需 JWT Token 即可访问
///     - 可通过请求参数传入真实用户 Code，注入该用户的真实身份；未传则匿名访问
///     - 当 AuthSettings.RequireAuth = true 时，此特性强制失效（生产环境兜底）
///     
///     userCode 参数获取优先级：
///     1. Query String: ?usercode=xxx
///     2. Header: X-User-Code: xxx
///     3. Form Data: usercode=xxx
///     
///     使用方式：临时测试一些特殊接口时，在行为方法上标记此特性即可
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
[AllowAnonymous]
public class YZHAnonymousAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string ConfigSection = "AuthSettings";
    private const string RequireAuthKey = "RequireAuth";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var authSection = config.GetSection(ConfigSection);

        // 生产模式（RequireAuth=true）：免认证特性无效，走正常 JWT 认证流程
        if (bool.TryParse(authSection[RequireAuthKey], out var requireAuth) && requireAuth)
        {
            return;
        }

        // 开发模式：从请求中读取 userCode
        var userCode = ReadUserCodeFromRequest(context.HttpContext.Request);

        if (!string.IsNullOrEmpty(userCode))
        {
            // 查询用户信息，注入真实用户 ClaimsPrincipal
            var principal = await TryBuildUserPrincipalAsync(context, userCode);
            if (principal != null)
            {
                context.HttpContext.User = principal;
                return;
            }
        }

        // 未传 userCode 或用户不存在 → 注入匿名访问身份
        context.HttpContext.User = BuildAnonymousPrincipal();
        await Task.CompletedTask;
    }

    /// <summary>从请求中读取 userCode（Query / Header / Form）</summary>
    private static string? ReadUserCodeFromRequest(HttpRequest request)
    {
        if (request.Query.TryGetValue("usercode", out var queryCode) && !string.IsNullOrEmpty(queryCode))
            return queryCode!;

        if (request.Headers.TryGetValue("X-User-Code", out var headerCode) && !string.IsNullOrEmpty(headerCode))
            return headerCode!;

        if (request.HasFormContentType && request.Form.TryGetValue("usercode", out var formCode) && !string.IsNullOrEmpty(formCode))
            return formCode!;

        return null;
    }

    /// <summary>查询用户并构建 ClaimsPrincipal</summary>
    private static async Task<ClaimsPrincipal?> TryBuildUserPrincipalAsync(AuthorizationFilterContext context, string userCode)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<YZH.Core.DataBase.Interfaces.IDbOrm>();
        var result = await db.QueryFirstOrDefaultAsync<YZH.Core.Api.Models.Users.Sys_User>(
            @"SELECT UserName, UserTrueName, Role_Id, Enable, Code 
              FROM Sys_User 
              WHERE Code = @Code AND IsDeleted = 0",
            new { Code = userCode });

        if (!result.Success || result.Data == null)
            return null;

        var user = result.Data;
        return BuildUserPrincipal(user.Code, user.UserName, user.UserTrueName, user.RoleId);
    }

    private static ClaimsPrincipal BuildUserPrincipal(string code, string userName, string? userTrueName, int roleId)
    {
        var claims = new List<Claim>
        {
            new("code", code),
            new(ClaimTypes.Name, userName ?? ""),
            new(ClaimTypes.GivenName, userTrueName ?? ""),
            new("role_id", roleId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "MockAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal BuildAnonymousPrincipal()
    {
        var claims = new List<Claim>
        {
            new("code", "anonymous"),
            new(ClaimTypes.Name, "anonymous"),
            new("role_id", "0"),
        };
        var identity = new ClaimsIdentity(claims, "Anonymous");
        return new ClaimsPrincipal(identity);
    }
}
