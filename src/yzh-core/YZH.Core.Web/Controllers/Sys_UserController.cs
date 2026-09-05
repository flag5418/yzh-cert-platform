using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Web.Controllers;

/// <summary>
/// Sys_User 用户管理控制器
/// 兼容 Vol 风格的 API 路径，支撑新前端 certplatform-web 调用
/// 
/// 前端 YzhTable 发送参数格式：
/// { page: 1, rows: 20, sort: 'userName', order: 'asc', userName: 'xxx', userTrueName: 'xxx' }
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class Sys_UserController : ControllerBase
{
    private readonly VOLContext _db;
    private readonly PasswordHelper _password;

    public Sys_UserController(VOLContext db, PasswordHelper password)
    {
        _db = db;
        _password = password;
    }

    /// <summary>分页查询用户列表</summary>
    [HttpPost("getPageData")]
    public async Task<IActionResult> GetPageData([FromBody] PageRequest req)
    {
        var query = _db.Set<Sys_User>().AsQueryable();

        // 关键字搜索（前端直接发送顶级属性）
        if (!string.IsNullOrEmpty(req.UserName))
            query = query.Where(u => u.UserName != null && u.UserName.Contains(req.UserName));

        if (!string.IsNullOrEmpty(req.UserTrueName))
            query = query.Where(u => u.UserTrueName != null && u.UserTrueName.Contains(req.UserTrueName));

        if (req.Enable.HasValue)
            query = query.Where(u => u.Enable == req.Enable.Value);

        var total = await query.CountAsync();
        var ordered = query.OrderByDescending(u => u.User_Id);

        var items = await ordered
            .Skip((req.Page - 1) * req.Rows)
            .Take(req.Rows)
            .Select(u => new
            {
                u.User_Id,
                u.UserName,
                u.UserTrueName,
                u.Role_Id,
                u.Enable,
                u.PhoneNo,
                u.Email,
                u.CreateDate,
                u.Remark
            })
            .ToListAsync();

        return Ok(new { status = 0, msg = "ok", rows = items, total });
    }

    /// <summary>根据 ID 获取单个用户</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Set<Sys_User>().FindAsync(id);
        if (user == null)
            return Ok(new { status = 1, msg = "用户不存在" });
        return Ok(new { status = 0, data = user });
    }

    /// <summary>新增用户（注册）</summary>
    [HttpPost("add")]
    [AllowAnonymous]
    public async Task<IActionResult> Add([FromBody] Sys_User user)
    {
        // 自动补全字段
        user.CreateDate = DateTime.Now;
        user.Enable = 1;
        // AES 加密密码（Vol 兼容）
        if (!string.IsNullOrEmpty(user.UserPwd))
            user.UserPwd = _password.AesEncrypt(user.UserPwd);

        _db.Set<Sys_User>().Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { status = 0, msg = "新增成功", data = new { user.User_Id, user.UserName } });
    }

    /// <summary>更新用户</summary>
    [HttpPut("update")]
    public async Task<IActionResult> Update([FromBody] Sys_User user)
    {
        var existing = await _db.Set<Sys_User>().FindAsync(user.User_Id);
        if (existing == null)
            return Ok(new { status = 1, msg = "用户不存在" });

        existing.UserName = user.UserName ?? existing.UserName;
        existing.UserTrueName = user.UserTrueName ?? existing.UserTrueName;
        existing.Role_Id = user.Role_Id;
        existing.Enable = user.Enable;
        existing.PhoneNo = user.PhoneNo ?? existing.PhoneNo;
        existing.Email = user.Email ?? existing.Email;
        existing.Remark = user.Remark ?? existing.Remark;

        await _db.SaveChangesAsync();
        return Ok(new { status = 0, msg = "更新成功" });
    }

    /// <summary>删除用户</summary>
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return Ok(new { status = 1, msg = "请指定要删除的ID" });

        var idList = ids.Split(',').Select(int.Parse).ToList();
        var users = await _db.Set<Sys_User>().Where(u => idList.Contains(u.User_Id)).ToListAsync();
        _db.Set<Sys_User>().RemoveRange(users);
        await _db.SaveChangesAsync();
        return Ok(new { status = 0, msg = $"已删除 {users.Count} 条记录" });
    }
}

/// <summary>
/// 分页请求参数（匹配前端 YzhTable 发送的格式）
/// </summary>
public class PageRequest
{
    public int Page { get; set; } = 1;
    public int Rows { get; set; } = 20;
    public string? Sort { get; set; }
    public string? Order { get; set; }

    // 搜索条件（顶级属性，与前端 searchFields 对应）
    public string? UserName { get; set; }
    public string? UserTrueName { get; set; }
    public byte? Enable { get; set; }
}
