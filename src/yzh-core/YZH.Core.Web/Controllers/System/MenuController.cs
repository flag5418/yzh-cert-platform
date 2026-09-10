using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
/// 菜单管理 Controller - 基于角色的动态菜单系统（Dapper 版）
/// </summary>
[Route("api/System/[controller]")]
[ApiController]
public class MenuController : ControllerBase
{
    private readonly IDbOrm _db;

    public MenuController(IDbOrm db)
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

        // 获取所有启用的 PC 端菜单
        var result = await _db.SqlQueryAsync(
            @"SELECT Menu_Id AS Id, ParentId, Code, MenuName AS Name, Url, Icon, Tag, OrderNo 
              FROM Sys_Menu 
              WHERE Enable = 1 AND IFNULL(MenuType, 0) = 0
              ORDER BY OrderNo DESC");

        if (!result.Success)
            return Ok(ApiResponse.Fail("获取菜单失败"));

        var allMenus = result.Data ?? new List<dynamic>();

        // 非超级管理员：过滤有权限的菜单
        if (!isSuperAdmin)
        {
            var primaryRoleCode = roleCodes.FirstOrDefault() ?? "0";
            int roleId = int.TryParse(primaryRoleCode, out var rid) ? rid : 0;

            var permResult = await _db.SqlQueryAsync(
                "SELECT Menu_Id FROM Sys_RoleAuth WHERE Role_Id = @RoleId",
                new { RoleId = roleId });

            if (permResult.Success && permResult.Data != null)
            {
                var permittedIds = permResult.Data.Select(d => (int)d.Menu_Id).ToHashSet();
                allMenus = allMenus.Where(m => permittedIds.Contains(m.Id)).ToList();
            }
        }

        // 构建树形结构
        var tree = BuildTree(allMenus, 0);
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
            @"SELECT Menu_Id AS id, MenuName AS name, Url AS url, ParentId AS parentId,
                     Icon AS icon, Enable AS enable, Tag AS tableName
              FROM Sys_Menu 
              WHERE Enable = 1
              ORDER BY OrderNo ASC");

        return Ok(new { menu = result.Data ?? new List<dynamic>(), asyncApi = new List<string>() });
    }

    /// <summary>递归构建菜单树</summary>
    private static List<MenuNode> BuildTree(List<dynamic> menus, int parentId)
    {
        return menus
            .Where(m => m.ParentId == parentId)
            .Select(m => new MenuNode
            {
                Id = m.Id,
                ParentId = m.ParentId,
                Code = m.Code ?? "",
                Name = m.Name,
                Url = m.Url ?? "",
                Icon = m.Icon ?? "",
                Tag = m.Tag ?? "",
                OrderNo = m.OrderNo ?? 0,
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
