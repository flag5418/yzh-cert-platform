using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Models;
using YZH.Core.Utilities;
using YZH.Entity.DomainModels;
using StandJwtHelper = YZH.Core.Stand.Helpers.JwtHelper;
using StandPasswordHelper = YZH.Core.Stand.Helpers.PasswordHelper;

namespace YZH.Core.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly VOLContext _db;
    private readonly StandJwtHelper _jwt;
    private readonly StandPasswordHelper _password;
    private readonly IMemoryCache _cache;

    public AuthController(VOLContext db, StandJwtHelper jwt, StandPasswordHelper password, IMemoryCache cache)
    {
        _db = db;
        _jwt = jwt;
        _password = password;
        _cache = cache;
    }

    /// <summary>登录</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrEmpty(req.UserName) || string.IsNullOrEmpty(req.Password))
            return BadRequest(ApiResponse.Fail("用户名和密码不能为空"));

        // 验证码校验
        if (!string.IsNullOrEmpty(req.Captcha) && !string.IsNullOrEmpty(req.Uuid))
        {
            if (!_cache.TryGetValue(req.Uuid, out string? cachedCode) ||
                string.IsNullOrEmpty(cachedCode) ||
                !string.Equals(cachedCode, req.Captcha, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse.Fail("验证码错误或已失效"));
            }
            // 验证成功后移除缓存（一次性使用）
            _cache.Remove(req.Uuid);
        }

        var user = await _db.Set<Sys_User>()
            .FirstOrDefaultAsync(u => u.UserName == req.UserName);

        if (user == null)
            return Unauthorized(ApiResponse.Fail("用户不存在"));

        if (user.Enable == 0)
            return Unauthorized(ApiResponse.Fail("账号已被禁用"));

        // Vol 兼容密码验证
        if (!_password.VerifyDes(req.Password, user.UserPwd ?? ""))
            return Unauthorized(ApiResponse.Fail("密码错误"));

        // 生成 Token（Code 优先，兼容旧用户取 UserName）
        var userCode = string.IsNullOrEmpty(user.Code) ? user.UserName : user.Code;
        var roleCodes = new[] { user.Role_Id.ToString() }; // TODO: 后续改为 RoleCode
        var token = _jwt.GenerateToken(userCode, user.UserName, roleCodes);

        // 更新 Token
        user.Token = token;
        user.LastLoginDate = DateTime.Now;
        _db.Set<Sys_User>().Update(user);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            UserCode = userCode,
            UserName = user.UserName,
            UserTrueName = user.UserTrueName,
            RoleId = user.Role_Id
        }, "登录成功"));
    }

    /// <summary>获取验证码</summary>
    [HttpGet("captcha")]
    [AllowAnonymous]
    public IActionResult GetCaptcha()
    {
        string code = VierificationCode.RandomText();
        var data = new
        {
            img = VierificationCodeHelpers.CreateBase64Image(code),
            uuid = Guid.NewGuid().ToString()
        };
        // 缓存验证码，5 分钟有效
        _cache.Set(data.uuid, code, TimeSpan.FromMinutes(5));
        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>健康检查</summary>
    [HttpGet("ping")]
    [AllowAnonymous]
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
    public int RoleId { get; set; }
}
