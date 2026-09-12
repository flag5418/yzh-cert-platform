using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     角色-菜单管理控制器（左树右表选择器）
///
///     继承 TreeTableControllerBase&lt;Sys_Role, Sys_Menu&gt; 获得：
///     - 左侧角色树（/tree/root /tree/children）
///     - 树形表格选择器虚方法（/checkTree /check/add /check/remove /check/all）
///
///     本控制器复写 4 个 check 虚方法，实现「角色 ↔ 菜单」关联：
///     - 关联表：Sys_RoleMenu（RoleCode + MenuCode，Code 关联）
///     - 勾选子菜单时自动补全祖先菜单，避免侧边栏断链
///
///     路由：api/System/RoleMenu、api/RoleMenu
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Route("api/[controller]")]
[Authorize]
public class RoleMenuController : TreeTableControllerBase<Sys_Role, Sys_Menu>
{
    private readonly EntityService<Sys_Menu> _menuService;
    private readonly EntityService<Sys_RoleMenu> _roleMenuService;
    private readonly IDbOrm _dbOrm;

    public RoleMenuController(
        EntityService<Sys_Role> treeEntityService,
        EntityService<Sys_Menu> tableEntityService,
        EntityService<Sys_RoleMenu> roleMenuService,
        IDbOrm dbOrm,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _menuService = tableEntityService;
        _roleMenuService = roleMenuService;
        _dbOrm = dbOrm;

        // 树配置：左侧为角色树，节点名取 RoleName
        TreeConfig = new TreeConfig
        {
            NameField = "RoleName",
            CodeField = "Code",
            ParentCodeField = "ParentCode",
            RelateField = "ParentCode",
        };
    }

    // ========================================================
    // 树形表格选择器 - 角色-菜单关联（通过 Sys_RoleMenu 关联表）
    // ========================================================

    /// <summary>获取菜单树数据（含勾选状态）</summary>
    [HttpPost("checkTree")]
    public override async Task<ActionResult<ApiResponse<CheckTreeNodeDto[]>>> GetCheckTree(
        [FromBody] CheckTreeRequest request)
    {
        try
        {
            // 1. 校验角色存在（用 Code 关联）
            var roleResult = await TreeEntity.GetByCode(request.ContextCode);
            if (!roleResult.Success || roleResult.Data == null)
                return BadRequest(ApiResponse<CheckTreeNodeDto[]>.Fail("角色不存在"));

            // 2. 全部菜单
            var menus = await LoadMenusAsync();

            // 3. 当前角色已授权的菜单 Code
            var linkResult = await _roleMenuService.GetListAsync(r => r.RoleCode == request.ContextCode);
            var granted = linkResult.Success
                ? (linkResult.Data ?? new()).Select(r => r.MenuCode).ToHashSet()
                : new HashSet<string>();

            // 4. 构建扁平节点（前端自行构建嵌套树）
            var nodes = menus
                .Where(m => !string.IsNullOrEmpty(m.Code))
                .OrderBy(m => m.OrderNo ?? 0)
                .Select(m => new CheckTreeNodeDto
                {
                    Code = m.Code,
                    Name = m.MenuName,
                    ParentCode = NormalizeParent(m.ParentCode),
                    NodeType = "menu",
                    CheckFlag = granted.Contains(m.Code),
                    Extra = new Dictionary<string, object>
                    {
                        ["Url"] = m.Url ?? string.Empty,
                        ["Icon"] = m.Icon ?? string.Empty,
                        ["Tag"] = m.Tag ?? string.Empty,
                        ["Enable"] = m.Enable ?? 0,
                    },
                })
                .ToArray();

            return Ok(ApiResponse<CheckTreeNodeDto[]>.Ok(nodes));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<CheckTreeNodeDto[]>.Fail($"获取角色菜单数据失败：{ex.Message}"));
        }
    }

    /// <summary>勾选保存 - 给角色授权菜单（写入 Sys_RoleMenu，Code 关联）</summary>
    [HttpPost("check/add")]
    public override async Task<ActionResult<ApiResponse<object?>>> CheckAdd(
        [FromBody] CheckActionRequest request)
    {
        try
        {
            // 验证角色存在
            var roleResult = await TreeEntity.GetByCode(request.ContextCode);
            if (!roleResult.Success || roleResult.Data == null)
                return BadRequest(ApiResponse<object?>.Fail("角色不存在"));

            var roleCode = request.ContextCode;

            var selected = request.Selections
                .Where(s => s.NodeType == "menu")
                .Select(s => s.Code)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            if (selected.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            // 补全祖先菜单：勾选子菜单时自动授权其所有上级，避免侧边栏断链
            var menus = await LoadMenusAsync();
            var menuMap = menus
                .Where(m => !string.IsNullOrEmpty(m.Code))
                .GroupBy(m => m.Code)
                .ToDictionary(g => g.Key, g => g.First());
            var target = ExpandWithAncestors(selected, menuMap);

            // 查询已存在的关联（避免重复插入）
            var existingResult = await _roleMenuService.GetListAsync(r => r.RoleCode == roleCode);
            var existing = existingResult.Success
                ? (existingResult.Data ?? new()).Select(r => r.MenuCode).ToHashSet()
                : new HashSet<string>();

            int inserted = 0;
            foreach (var menuCode in target)
            {
                if (existing.Contains(menuCode))
                    continue;

                var entity = new Sys_RoleMenu
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Code = Guid.NewGuid().ToString("N"),
                    RoleCode = roleCode,
                    MenuCode = menuCode,
                };
                var result = await _roleMenuService.Insert(entity, UserContext.ClientIp);
                if (result.Success) inserted++;
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = inserted }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"保存失败：{ex.Message}"));
        }
    }

    /// <summary>取消勾选 - 移除角色与菜单的关联（删除 Sys_RoleMenu 记录）</summary>
    [HttpPost("check/remove")]
    public override async Task<ActionResult<ApiResponse<object?>>> CheckRemove(
        [FromBody] CheckActionRequest request)
    {
        try
        {
            // 验证角色存在
            var roleResult = await TreeEntity.GetByCode(request.ContextCode);
            if (!roleResult.Success || roleResult.Data == null)
                return BadRequest(ApiResponse<object?>.Fail("角色不存在"));

            var roleCode = request.ContextCode;

            var menuCodes = request.Selections
                .Where(s => s.NodeType == "menu")
                .Select(s => s.Code)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            if (menuCodes.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            // Sys_RoleMenu 无业务 Code 列，按 RoleCode + MenuCode 原生删除
            int deleted = 0;
            foreach (var menuCode in menuCodes)
            {
                var result = await _dbOrm.SqlExecuteAsync(
                    "DELETE FROM Sys_RoleMenu WHERE RoleCode = @RoleCode AND MenuCode = @MenuCode",
                    new { RoleCode = roleCode, MenuCode = menuCode });
                if (result.Success && result.Data > 0) deleted++;
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = deleted }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"移除失败：{ex.Message}"));
        }
    }

    /// <summary>获取所有角色-菜单关联（用于前端本地缓存，减少切换角色的网络请求）</summary>
    [HttpPost("check/all")]
    public override async Task<ActionResult<ApiResponse<AssociationDto[]>>> GetAllAssociations()
    {
        try
        {
            var result = await _roleMenuService.GetListAsync();
            var list = result.Success ? result.Data ?? new() : new();

            var associations = list
                .Where(r => !string.IsNullOrEmpty(r.RoleCode) && !string.IsNullOrEmpty(r.MenuCode))
                .Select(r => new AssociationDto
                {
                    ContextCode = r.RoleCode,
                    TargetCode = r.MenuCode,
                    NodeType = "menu",
                })
                .ToArray();

            return Ok(ApiResponse<AssociationDto[]>.Ok(associations));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AssociationDto[]>.Fail($"获取关联数据失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 私有辅助
    // ========================================================

    private async Task<List<Sys_Menu>> LoadMenusAsync()
    {
        var result = await _menuService.GetListAsync();
        return result.Success ? result.Data ?? new() : new();
    }

    /// <summary>根节点父编码统一为 null（Sys_Menu 用 "0" 表示根）</summary>
    private static string? NormalizeParent(string? parentCode)
        => string.IsNullOrEmpty(parentCode) || parentCode == "0" ? null : parentCode;

    /// <summary>向上补全所有祖先菜单 Code（防环保护：最多 50 层）</summary>
    private static HashSet<string> ExpandWithAncestors(
        IEnumerable<string> codes, Dictionary<string, Sys_Menu> menuMap)
    {
        var result = new HashSet<string>(codes);
        foreach (var code in codes.ToList())
        {
            if (!menuMap.TryGetValue(code, out var current))
                continue;

            var guard = 0;
            while (guard++ < 50)
            {
                var parentCode = NormalizeParent(current.ParentCode);
                if (parentCode == null || !menuMap.TryGetValue(parentCode, out var parent))
                    break;

                result.Add(parent.Code);
                current = parent;
            }
        }
        return result;
    }
}
