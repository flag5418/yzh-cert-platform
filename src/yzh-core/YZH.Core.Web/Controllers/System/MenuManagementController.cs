using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     菜单管理控制器（新架构版）
///     
///     继承 YzhControllerBase 获得：
///     - 标准 CRUD（Add/Update/Delete/GetPage/GetConfig）
///     - 原子方法（AddCore/UpdateCore/DeleteCore/GetPageCore）
///     - 生命周期钩子
///     - 行操作注册
///     
///     路由：api/SysMenu
///     
///     注意：菜单是树形结构，GetPageCore 会返回扁平列表，
///     前端需要自行构建树形结构
/// </summary>
[ApiController]
[Route("api/System/MenuManagement")]
[Authorize]
public class MenuManagementController : YzhControllerBase<Sys_Menu>
{
    private readonly MenuPermissionService _menuPermission;

    public MenuManagementController(
        EntityService<Sys_Menu> entityService,
        MenuPermissionService menuPermission,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        _menuPermission = menuPermission;
        // 注册行操作
        RegisterRowAction("Enable", EnableMenu);
        RegisterRowAction("Disable", DisableMenu);
        RegisterRowAction("GetTree", GetMenuTreeAsync);
    }

    /// <summary>
    ///     菜单采用物理删除
    ///
    ///     原因：Sys_Menu 表没有 DeleteTime / DeleteBy 列，而基类默认软删除会执行
    ///     UpdateAsync(entity, ["IsDeleted", "DeleteTime", "DeleteBy"])（EntityService.SoftDelete），
    ///     必然 SQL 异常 → 删除功能整体不可用。禁用请走 action/Disable（IsValid=0）。
    /// </summary>
    protected override bool HardDelete => true;

    #region 查询钩子

    /// <summary>查询后处理 - 填充子菜单数量</summary>
    protected override void OnQueried(PagedResult<Sys_Menu> result)
    {
        // TODO: 可在结果中填充子菜单数量等统计信息
        base.OnQueried(result);
    }

    /// <summary>
    ///     获取菜单树（特殊查询，不走分页）
    ///     GET api/SysMenu/tree
    ///     按当前登录用户的角色过滤（超级管理员返回全部）
    /// </summary>
    [HttpGet("tree")]
    public virtual async Task<ApiResponse<List<Sys_Menu>>> GetTree()
    {
        try
        {
            // 获取当前用户可见菜单（按角色过滤 + 祖先补全）
            var visibleMenus = await _menuPermission.GetVisibleMenusAsync(UserContext);

            // 构建树形结构（扁平化输出，前端自行嵌套）
            var tree = BuildMenuTree(visibleMenus);

            return ApiResponse<List<Sys_Menu>>.Ok(tree);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<Sys_Menu>>.Fail($"获取菜单树失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     获取全量菜单树（管理端维护用：不做当前用户权限过滤）
    ///     GET api/System/MenuManagement/tree/all
    ///
    ///     与 /tree 的区别：
    ///     - /tree     = 当前登录用户【可见】菜单（侧边栏与权限展示使用）
    ///     - /tree/all = 全量菜单（菜单管理页维护使用），否则非超管只能看到被授权的子集
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

    #endregion

    #region 新增钩子

    /// <summary>新增前处理 - 自动生成编码、校验唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_Menu entity)
    {
        // 自动生成 Code
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = $"MENU_{Guid.NewGuid():N}".Substring(0, 50);

        // 校验编码唯一性
        var exists = await Entity.ExistsByCodeAsync(entity.Code);
        if (exists.Data == true)
            return (false, $"菜单编码 {entity.Code} 已存在");

        // 设置默认值
        entity.IsValid = 1;
        if (entity.OrderNo == null)
            entity.OrderNo = 0;
        if (string.IsNullOrEmpty(entity.ParentCode))
            entity.ParentCode = "0";

        return (true, null);
    }

    #endregion

    #region 修改钩子

    /// <summary>
    ///     修改前处理 - 同级菜单名称唯一性（排除自身）
    ///
    ///     ⚠️ 旧实现用 GetByCode(entity.Code) 判断"编码已存在"，但 Code 就是待修改记录的定位键，
    ///     该查询命中的永远是记录自身 → 所有修改请求都被拒绝（修改功能整体失效）。
    ///     Code 由唯一索引保证，不需要在这里重复校验。
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_Menu entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            return (false, "菜单编码不能为空");

        // 上级编码缺省为根（NULL 会破坏 ParentCode NOT NULL 约束）
        entity.ParentCode = string.IsNullOrEmpty(entity.ParentCode) ? "0" : entity.ParentCode;

        // 防环：上级菜单不能是自己
        if (entity.ParentCode == entity.Code)
            return (false, "上级菜单不能是自己");

        // 同级菜单名称唯一（排除自身）
        var nameExists = await Entity.ExistsAsync(m =>
            m.Code != entity.Code &&
            m.ParentCode == entity.ParentCode &&
            m.MenuName == entity.MenuName);
        if (nameExists.Data)
            return (false, $"同级下已存在菜单「{entity.MenuName}」");

        return (true, null);
    }

    #endregion

    #region 行操作

    /// <summary>启用菜单（POST api/SysMenu/action/Enable）</summary>
    private async Task<Result<ApiResponse<object?>>> EnableMenu(Sys_Menu entity)
    {
        // GetByCodeAny：已禁用菜单 IsValid=0，GetByCode 查不到
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

    /// <summary>禁用菜单（POST api/SysMenu/action/Disable）</summary>
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

    /// <summary>获取菜单树（POST api/SysMenu/action/GetTree）</summary>
    private async Task<Result<ApiResponse<object?>>> GetMenuTreeAsync(Sys_Menu entity)
    {
        var result = await GetTree();
        if (!result.Success)
            return Result<ApiResponse<object?>>.Fail(result.Message);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok(result.Data));
    }

    #endregion

    #region 私有方法

    /// <summary>构建菜单树（返回带层级结构的扁平列表）</summary>
    private List<Sys_Menu> BuildMenuTree(List<Sys_Menu> menus)
    {
        var result = new List<Sys_Menu>();

        // 先添加根节点（ParentCode = "0"）
        foreach (var menu in menus.Where(m => m.ParentCode == "0").OrderBy(m => m.OrderNo ?? 0))
        {
            result.Add(menu);
            AddChildMenus(result, menus, menu.Code);
        }

        return result;
    }

    /// <summary>递归添加子菜单</summary>
    private void AddChildMenus(List<Sys_Menu> result, List<Sys_Menu> allMenus, string parentCode)
    {
        foreach (var child in allMenus.Where(m => m.ParentCode == parentCode).OrderBy(m => m.OrderNo ?? 0))
        {
            result.Add(child);
            AddChildMenus(result, allMenus, child.Code);
        }
    }

    #endregion
}
