using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Web.Controllers;

/// <summary>
/// 菜单管理 Controller - 基于角色的动态菜单系统（双键设计版）
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly VOLContext _db;

    public MenuController(VOLContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 获取当前用户的菜单树（基于角色权限）
    /// GET /api/Menu/tree
    /// </summary>
    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTree()
    {
        // 从 Claims 获取当前用户角色 Code
        var roleCodes = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        bool isSuperAdmin = roleCodes.Contains("1") || roleCodes.Contains("super_admin");

        // 获取所有启用的 PC 端菜单，按排序号降序
        var allMenus = await _db.Set<Sys_Menu>()
            .Where(m => m.Enable == 1 && (m.MenuType == null || m.MenuType == 0))
            .OrderByDescending(m => m.OrderNo ?? 0)
            .Select(m => new MenuNode
            {
                Id = m.Menu_Id,
                ParentId = m.ParentId,
                Code = m.Code ?? "",
                Name = m.MenuName,
                Url = m.Url ?? "",
                Icon = m.Icon ?? "",
                Tag = m.Tag ?? "",
                OrderNo = m.OrderNo ?? 0
            })
            .ToListAsync();

        // 非超级管理员：过滤有权限的菜单
        if (!isSuperAdmin)
        {
            // 使用第一个有效角色 Code/Id 查询权限（兼容过渡期）
            var primaryRoleCode = roleCodes.FirstOrDefault() ?? "0";
            int roleId = int.TryParse(primaryRoleCode, out var rid) ? rid : 0;

            var permittedMenuIds = await _db.Set<Sys_RoleAuth>()
                .Where(ra => ra.Role_Id == roleId)
                .Select(ra => ra.Menu_Id)
                .ToListAsync();

            allMenus = allMenus
                .Where(m => permittedMenuIds.Contains(m.Id))
                .ToList();
        }

        // 构建树形结构
        var tree = BuildTree(allMenus, 0);

        return Ok(ApiResponse<object>.Ok(new { menu = tree })); // ApiResponse 后续替换为统一 Result
    }

    /// <summary>
    /// 递归构建菜单树
    /// </summary>
    private static List<MenuNode> BuildTree(List<MenuNode> menus, int parentId)
    {
        return menus
            .Where(m => m.ParentId == parentId)
            .Select(m => new MenuNode
            {
                Id = m.Id,
                ParentId = m.ParentId,
                Code = m.Code,
                Name = m.Name,
                Url = m.Url,
                Icon = m.Icon,
                Tag = m.Tag,
                OrderNo = m.OrderNo,
                Children = BuildTree(menus, m.Id)
            })
            .Where(m => m.Children.Count > 0 || !string.IsNullOrEmpty(m.Url))
            .ToList();
    }
}

public class MenuNode
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int OrderNo { get; set; }
    public List<MenuNode> Children { get; set; } = new();
}
