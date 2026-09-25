using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     菜单管理控制器（左树右表 · TreeTableControllerBase）
///
///     左树：Sys_Menu（菜单树，懒加载 + 增删改 + 启停）
///     右表：Sys_Menu（选中节点后展示其子级）
///     关联：Sys_Menu.ParentCode = 节点 Code；根哨兵 ParentCode = "0"
///
///     API（前端 controllerName = System/MenuManagement）：
///     POST  /api/System/MenuManagement/tree/root|children|add|update|delete|toggle-valid
///     POST  /api/System/MenuManagement/filter|add|update|delete
///     GET   /api/System/MenuManagement/treepconfig|config
///     GET   /api/System/MenuManagement/tree        当前用户可见菜单（侧栏）
///     GET   /api/System/MenuManagement/tree/all    全量菜单（管理端维护）
/// </summary>
[ApiController]
[Route("api/System/MenuManagement")]
[Route("api/[controller]")]
[Authorize]
public class MenuManagementController : TreeTableControllerBase<Sys_Menu, Sys_Menu>
{
    private readonly MenuPermissionService _menuPermission;

    public MenuManagementController(
        EntityService<Sys_Menu> treeEntityService,
        EntityService<Sys_Menu> tableEntityService,
        MenuPermissionService menuPermission,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _menuPermission = menuPermission;

        TreeConfig = new TreeConfig
        {
            RootParentCode = "0",
            NameField = "MenuName",
            CodeField = "Code",
            ParentCodeField = "ParentCode",
            RelateField = "ParentCode",
            MaxLevel = 2,
            EnableField = "IsValid",
            AllowDeleteWithChildren = false,
        };

        TreeFormConfigName = "System/Sys_MenuForm";

        RegisterRowAction("Enable", EnableMenu);
        RegisterRowAction("Disable", DisableMenu);
        // GetTree 是侧栏菜单 HTTP 端点（[HttpGet("tree")]），不是行操作，禁止 RegisterRowAction
    }

    /// <summary>
    ///     菜单采用物理删除（Sys_Menu 无 DeleteTime/DeleteBy，软删会 SQL 异常）。
    ///     禁用走 /tree/toggle-valid（IsValid=0）。
    /// </summary>
    protected override bool HardDelete => true;

    /// <summary>树节点表单配置严格加载（缺失即抛错，避免页面空白无报错）</summary>
    protected override bool StrictConfigLoad => true;

    // ========================================================
    // 一、树节点生命周期钩子
    // ========================================================

    /// <summary>
    ///     新增菜单前：生成 MENU_ 前缀 Code、唯一性校验、默认值
    ///     注意：Code 兜底（Guid）在本钩子之后执行，此处可安全生成业务前缀 Code。
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(Sys_Menu entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = $"MENU_{Guid.NewGuid():N}".Substring(0, 50);

        var exists = await TreeEntity.ExistsByCodeAsync(entity.Code);
        if (exists.Data == true)
            return (false, $"菜单编码 {entity.Code} 已存在");

        if (string.IsNullOrWhiteSpace(entity.MenuName))
            return (false, "菜单名称不能为空");

        entity.IsValid = 1;
        if (entity.OrderNo == null)
            entity.OrderNo = 0;
        if (string.IsNullOrEmpty(entity.ParentCode))
            entity.ParentCode = "0";

        return (true, null);
    }

    /// <summary>修改菜单前：同级名称唯一（排除自身）+ 防环</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(Sys_Menu entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            return (false, "菜单编码不能为空");
        if (string.IsNullOrWhiteSpace(entity.MenuName))
            return (false, "菜单名称不能为空");

        entity.ParentCode = string.IsNullOrEmpty(entity.ParentCode) ? "0" : entity.ParentCode;

        if (entity.ParentCode == entity.Code)
            return (false, "上级菜单不能是自己");

        var nameExists = await TreeEntity.ExistsAsync(m =>
            m.Code != entity.Code &&
            m.ParentCode == entity.ParentCode &&
            m.MenuName == entity.MenuName);
        if (nameExists.Data)
            return (false, $"同级下已存在菜单「{entity.MenuName}」");

        return (true, null);
    }

    /// <summary>新增前（单表路径，右表新增时同样注入 MENU_ Code 与默认值）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_Menu entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = $"MENU_{Guid.NewGuid():N}".Substring(0, 50);

        var exists = await Entity.ExistsByCodeAsync(entity.Code);
        if (exists.Data == true)
            return (false, $"菜单编码 {entity.Code} 已存在");

        if (string.IsNullOrWhiteSpace(entity.MenuName))
            return (false, "菜单名称不能为空");

        entity.IsValid = 1;
        if (entity.OrderNo == null)
            entity.OrderNo = 0;
        if (string.IsNullOrEmpty(entity.ParentCode))
            entity.ParentCode = "0";

        return (true, null);
    }

    /// <summary>修改前（单表路径）</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_Menu entity)
        => OnBeforeUpdateTree(entity);

    // ========================================================
    // 二、树节点 DTO 扩展（补齐 Extra 缺口）
    // ========================================================

    /// <summary>
    ///     TreeMapper 只自动填 Enable/IsValid/Remark/Status/OrderNo 等，
    ///     缺 Url/Icon/Description/Tag/Auth → 编辑弹窗回填会丢字段。
    ///     此处按 PascalCase 补齐（与 EntityConfig FieldName 一致）。
    /// </summary>
    protected override TreeItemDto MapToTreeItem(Sys_Menu entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra ??= new Dictionary<string, object>();
        dto.Extra["IsValid"] = entity.IsValid;
        dto.Extra["OrderNo"] = entity.OrderNo ?? 0;
        dto.Extra["Url"] = entity.Url ?? string.Empty;
        dto.Extra["Icon"] = entity.Icon ?? string.Empty;
        dto.Extra["Description"] = entity.Description ?? string.Empty;
        dto.Extra["Tag"] = entity.Tag ?? string.Empty;
        dto.Extra["Auth"] = entity.Auth ?? string.Empty;
        return dto;
    }

    // ========================================================
    // 三、菜单树查询（侧栏/维护）
    // ========================================================

    /// <summary>
    ///     当前用户可见菜单树（按角色过滤）
    ///     GET /api/System/MenuManagement/tree
    /// </summary>
    [HttpGet("tree")]
    public virtual async Task<ApiResponse<List<Sys_Menu>>> GetTree()
    {
        try
        {
            var visibleMenus = await _menuPermission.GetVisibleMenusAsync(UserContext);
            return ApiResponse<List<Sys_Menu>>.Ok(BuildMenuTree(visibleMenus));
        }
        catch (Exception ex)
        {
            return ApiResponse<List<Sys_Menu>>.Fail($"获取菜单树失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     全量菜单树（管理端维护，不做权限过滤）
    ///     GET /api/System/MenuManagement/tree/all
    /// </summary>
    [HttpGet("tree/all")]
    public virtual async Task<ApiResponse<List<Sys_Menu>>> GetAllTree()
    {
        try
        {
            var all = await _menuPermission.GetAllMenusAsync();
            return ApiResponse<List<Sys_Menu>>.Ok(BuildMenuTree(all));
        }
        catch (Exception ex)
        {
            return ApiResponse<List<Sys_Menu>>.Fail($"获取全量菜单失败：{ex.Message}");
        }
    }

    // ========================================================
    // 四、行操作（兼容旧前端动作键）
    // ========================================================

    private async Task<Result<ApiResponse<object?>>> EnableMenu(Sys_Menu entity)
    {
        var result = await Entity.GetByCodeAny(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("菜单不存在");

        var menu = result.Data;
        menu.IsValid = 1;
        var updateResult = await Entity.Update(menu, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已启用该菜单"));
    }

    private async Task<Result<ApiResponse<object?>>> DisableMenu(Sys_Menu entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("菜单不存在");

        var menu = result.Data;
        menu.IsValid = 0;
        var updateResult = await Entity.Update(menu, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该菜单"));
    }

    // ========================================================
    // 五、私有辅助
    // ========================================================

    private List<Sys_Menu> BuildMenuTree(List<Sys_Menu> menus)
    {
        var result = new List<Sys_Menu>();
        foreach (var menu in menus.Where(m => m.ParentCode == "0").OrderBy(m => m.OrderNo ?? 0))
        {
            result.Add(menu);
            AddChildMenus(result, menus, menu.Code);
        }
        return result;
    }

    private void AddChildMenus(List<Sys_Menu> result, List<Sys_Menu> allMenus, string parentCode)
    {
        foreach (var child in allMenus.Where(m => m.ParentCode == parentCode).OrderBy(m => m.OrderNo ?? 0))
        {
            result.Add(child);
            AddChildMenus(result, allMenus, child.Code);
        }
    }
}
