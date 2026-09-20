extern alias VolFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SqlSugar;
using System.Text.Json.Serialization;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using VolUtilities = VolFramework.YZH.Core.Utilities;
using StandJwtHelper = YZH.Core.Stand.Helpers.JwtHelper;
using StandPasswordHelper = YZH.Core.Stand.Helpers.PasswordHelper;

namespace YZH.Core.Web.Controllers;

[Route("api/User")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IDbOrm _db;
    private readonly StandJwtHelper _jwt;
    private readonly StandPasswordHelper _password;
    private readonly IMemoryCache _cache;
    private readonly TokenVersionService _tokenVersion;
    private readonly IRoleService _roleService;

    public AuthController(IDbOrm db, StandJwtHelper jwt, StandPasswordHelper password, IMemoryCache cache, TokenVersionService tokenVersion, IRoleService roleService)
    {
        _db = db;
        _jwt = jwt;
        _password = password;
        _cache = cache;
        _tokenVersion = tokenVersion;
        _roleService = roleService;
    }

    /// <summary>登录（兼容 Vol 路由：api/User/login）</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ApiDescription("用户登录", "系统", "系统管理/认证管理", true)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrEmpty(req.UserName) || string.IsNullOrEmpty(req.Password))
            return BadRequest(ApiResponse.Fail("用户名和密码不能为空"));

        // 验证码校验（DEBUG 模式跳过验证码）
#if DEBUG
        // DEBUG 模式下跳过验证码校验，方便开发调试
#else
        if (!string.IsNullOrEmpty(req.Captcha) && !string.IsNullOrEmpty(req.Uuid))
        {
            var captchaKey = $"captcha_{req.Uuid}";
            if (!_cache.TryGetValue(captchaKey, out string? cachedCode) ||
                string.IsNullOrEmpty(cachedCode) ||
                !string.Equals(cachedCode, req.Captcha, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse.Fail("验证码错误或已失效"));
            }
            _cache.Remove(captchaKey);
        }
#endif

        // 查询用户
        var user = await _db.Client.Queryable<Sys_User>()
            .Where(x => x.UserName == req.UserName && !x.IsDeleted)
            .FirstAsync();

        if (user == null)
            return Unauthorized(ApiResponse.Fail("用户不存在"));

        // 检查账号是否禁用
        if (user.Enable == 0)
            return Unauthorized(ApiResponse.Fail("账号已被禁用"));

        // 验证密码
        if (!_password.VerifyDes(req.Password, user.UserPwd ?? ""))
            return Unauthorized(ApiResponse.Fail("密码错误"));

        // 生成 SSO 版本号（新登录生成新版本，挤掉旧 Token）
        string userCode = user.Code ?? user.UserName;
        var ssoVersion = await _tokenVersion.BumpVersionAsync(userCode);

        // 查询角色编码（通过 Sys_RoleUser 关联表，Code 关联）
        var roleResult = await _roleService.GetRoleCodeByUserCodeAsync(userCode);
        var roleCode = roleResult.Data;

        // 生成 Token（含 SSO 版本号；roles 携带角色编码）
        var roleClaims = new List<string>();
        if (!string.IsNullOrEmpty(roleCode))
            roleClaims.Add(roleCode);
        var token = _jwt.GenerateToken(userCode, user.UserName, roleClaims, ssoVersion);

        // 更新 Token 和登录时间
        await _db.Client.Updateable<Sys_User>()
            .SetColumns(x => new Sys_User { Token = token, LastLoginDate = DateTime.Now })
            .Where(x => x.UserName == req.UserName)
            .ExecuteCommandAsync();

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            UserCode = userCode,
            UserName = user.UserName,
            UserTrueName = user.UserTrueName,
            RoleCode = roleCode
        }, "登录成功"));
    }

    /// <summary>获取验证码</summary>
    [HttpGet("captcha")]
    [AllowAnonymous]
    [ApiDescription("获取登录验证码", "系统", "系统管理/认证管理", true)]
    public IActionResult GetCaptcha()
    {
        string code = VolUtilities.VierificationCode.RandomText();
        var data = new
        {
            img = VolUtilities.VierificationCodeHelpers.CreateBase64Image(code),
            uuid = Guid.NewGuid().ToString()
        };
        _cache.Set(data.uuid, code, TimeSpan.FromMinutes(5));
        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>健康检查</summary>
    [HttpGet("ping")]
    [AllowAnonymous]
    [ApiDescription("健康检查", "系统", "系统管理/认证管理", true)]
    public IActionResult Ping() => Ok(ApiResponse.Ok("pong"));
}

public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Captcha { get; set; }
    public string? Uuid { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserTrueName { get; set; }
    public string? RoleCode { get; set; }
}
