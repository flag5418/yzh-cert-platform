using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
/// 菜单管理 Controller - 基于角色的动态菜单系统（兼容旧 Vol 路由）
/// 权限来源：Sys_RoleMenu（RoleCode + MenuCode）
/// </summary>
[Route("api/System/[controller]")]
[ApiController]
public class MenuController : ControllerBase
{
    private readonly IDbOrm _db;
    private readonly MenuPermissionService _menuPermission;
    private readonly IUserContext _userContext;

    public MenuController(IDbOrm db, MenuPermissionService menuPermission, IUserContext userContext)
    {
        _db = db;
        _menuPermission = menuPermission;
        _userContext = userContext;
    }

    /// <summary>
    /// 获取当前用户的菜单树（基于角色权限）
    /// GET /api/Menu/tree
    /// </summary>
    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTree()
    {
        // 按当前用户角色过滤菜单（超级管理员返回全部）
        var visibleMenus = await _menuPermission.GetVisibleMenusAsync(_userContext);

        var nodes = visibleMenus.Select(m => new MenuNode
        {
            Id = int.TryParse(m.Id, out var id) ? id : 0,
            ParentCode = m.ParentCode ?? "0",
            Code = m.Code ?? "",
            Name = m.MenuName,
            Url = m.Url ?? "",
            Icon = m.Icon ?? "",
            Tag = m.Tag ?? "",
            OrderNo = m.OrderNo ?? 0
        }).ToList();

        // 构建树形结构
        var tree = BuildTree(nodes, "0");
        return Ok(ApiResponse<object>.Ok(new { menu = tree }));
    }

    /// <summary>
    /// 获取菜单树（兼容旧 Vol 路由：GET /api/Menu/getTreeMenu）
    /// Vol 风格返回扁平列表，前端自行构建树
    /// </summary>
    [HttpGet("getTreeMenu")]
    [HttpGet("~/api/Menu/getTreeMenu")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTreeMenu()
    {
        var result = await _db.SqlQueryAsync(
            @"SELECT Menu_Id AS id, MenuName AS name, Url AS url, ParentCode AS parentId,
                      Icon AS icon, Enable AS enable, Tag AS tableName
               FROM Sys_Menu
               WHERE Enable = 1
               ORDER BY OrderNo ASC");

        return Ok(new { menu = result.Data ?? new List<dynamic>(), asyncApi = new List<string>() });
    }

    /// <summary>递归构建菜单树</summary>
    private static List<MenuNode> BuildTree(List<MenuNode> menus, string parentCode)
    {
        return menus
            .Where(m => m.ParentCode == parentCode)
            .Select(m => new MenuNode
            {
                Id = m.Id,
                ParentId = int.TryParse(parentCode, out var pid) ? pid : 0,
                ParentCode = m.ParentCode,
                Code = m.Code,
                Name = m.Name,
                Url = m.Url,
                Icon = m.Icon,
                Tag = m.Tag,
                OrderNo = m.OrderNo,
                Children = BuildTree(menus, m.Code)
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
    public string ParentCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int OrderNo { get; set; }
    public List<MenuNode> Children { get; set; } = new();
}
