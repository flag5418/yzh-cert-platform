using YZH.Core.Api.Models.System;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Services;

/// <summary>
///     菜单权限服务：按当前登录用户的角色过滤可见菜单
///
///     规则：
///     1. 超级管理员（RoleId=1 或角色编码 ROLE_SUPER_ADMIN）→ 全部菜单
///     2. 其他角色 → 仅返回 Sys_RoleMenu 中已授权的菜单
///     3. 自动补全祖先菜单（即便历史数据缺少祖先，也不会出现断链菜单）
///
///     数据来源：Sys_RoleMenu（RoleCode + MenuCode，Code 关联）
/// </summary>
public class MenuPermissionService
{
    /// <summary>超级管理员角色编码</summary>
    public const string SuperAdminRoleCode = "ROLE_SUPER_ADMIN";

    /// <summary>超级管理员角色 ID</summary>
    public const int SuperAdminRoleId = 1;

    private readonly EntityService<Sys_Menu> _menuService;
    private readonly EntityService<Sys_RoleMenu> _roleMenuService;

    public MenuPermissionService(
        EntityService<Sys_Menu> menuService,
        EntityService<Sys_RoleMenu> roleMenuService)
    {
        _menuService = menuService;
        _roleMenuService = roleMenuService;
    }

    /// <summary>是否为超级管理员</summary>
    public static bool IsSuperAdmin(IUserContext? ctx)
    {
        if (ctx == null) return false;
        if (ctx.RoleId == SuperAdminRoleId) return true;
        return ctx.GetRoleCodes()?.Any(c =>
            string.Equals(c, SuperAdminRoleCode, StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>获取全部菜单（不过滤）</summary>
    public async Task<List<Sys_Menu>> GetAllMenusAsync()
    {
        var result = await _menuService.GetListAsync();
        return result.Success ? result.Data ?? new() : new();
    }

    /// <summary>获取当前用户可见的菜单（含祖先补全）</summary>
    public async Task<List<Sys_Menu>> GetVisibleMenusAsync(IUserContext? ctx)
    {
        var all = await GetAllMenusAsync();

        // 超级管理员：全部菜单
        if (IsSuperAdmin(ctx))
            return all;

        var roleCodes = ctx?.GetRoleCodes()?
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .ToList() ?? new List<string>();

        if (roleCodes.Count == 0)
            return new List<Sys_Menu>();

        // 查询该用户角色已授权的菜单 Code
        var linkResult = await _roleMenuService.GetListAsync(x => roleCodes.Contains(x.RoleCode));
        var links = linkResult.Success ? linkResult.Data ?? new() : new();

        var granted = links
            .Select(x => x.MenuCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (granted.Count == 0)
            return new List<Sys_Menu>();

        // 祖先补全（防御：避免父菜单未授权导致子菜单断链）
        var byCode = all
            .Where(m => !string.IsNullOrEmpty(m.Code))
            .GroupBy(m => m.Code)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var code in granted.ToList())
        {
            if (!byCode.TryGetValue(code, out var current))
                continue;

            var guard = 0;
            while (guard++ < 50)
            {
                var parentCode = current.ParentCode;
                if (string.IsNullOrEmpty(parentCode) || parentCode == "0")
                    break;
                if (!byCode.TryGetValue(parentCode, out var parent))
                    break;

                granted.Add(parent.Code);
                current = parent;
            }
        }

        return all
            .Where(m => !string.IsNullOrEmpty(m.Code) && granted.Contains(m.Code))
            .ToList();
    }
}
