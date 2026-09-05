using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly VOLContext _db;
    private readonly JwtHelper _jwt;
    private readonly PasswordHelper _password;

    public AuthController(VOLContext db, JwtHelper jwt, PasswordHelper password)
    {
        _db = db;
        _jwt = jwt;
        _password = password;
    }

    /// <summary>登录</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrEmpty(req.UserName) || string.IsNullOrEmpty(req.Password))
            return BadRequest(ApiResponse.Fail("用户名和密码不能为空"));

        var user = await _db.Set<Sys_User>()
            .FirstOrDefaultAsync(u => u.UserName == req.UserName);

        if (user == null)
            return Unauthorized(ApiResponse.Fail("用户不存在"));

        if (user.Enable == 0)
            return Unauthorized(ApiResponse.Fail("账号已被禁用"));

        // Vol 兼容密码验证
        if (!_password.VerifyDes(req.Password, user.UserPwd ?? ""))
            return Unauthorized(ApiResponse.Fail("密码错误"));

        // 生成 Token
        var token = _jwt.GenerateToken(user.User_Id, user.UserName, user.Role_Id);

        // 更新 Token
        user.Token = token;
        user.LastLoginDate = DateTime.Now;
        _db.Set<Sys_User>().Update(user);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            Token = token,
            UserName = user.UserName,
            UserTrueName = user.UserTrueName,
            RoleId = user.Role_Id
        }, "登录成功"));
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
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserTrueName { get; set; }
    public int RoleId { get; set; }
}
