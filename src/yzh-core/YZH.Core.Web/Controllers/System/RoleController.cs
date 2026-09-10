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
///     角色管理控制器（新架构版）
///     
///     继承 YzhControllerBase 获得：
///     - 标准 CRUD（Add/Update/Delete/GetPage/GetConfig）
///     - 原子方法（AddCore/UpdateCore/DeleteCore/GetPageCore）
///     - 生命周期钩子
///     - 行操作注册
///     
///     路由：api/SysRole
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Authorize]
public class RoleController : YzhControllerBase<Sys_Role>
{
    public RoleController(
        EntityService<Sys_Role> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        // 注册行操作
        RegisterRowAction("Enable", EnableRole);
        RegisterRowAction("Disable", DisableRole);
        RegisterRowAction("SetPermission", SetPermissionAsync);
    }

    #region 查询钩子

    /// <summary>查询后处理 - 填充子角色数量</summary>
    protected override void OnQueried(PagedResult<Sys_Role> result)
    {
        // TODO: 可在结果中填充子角色数量等统计信息
        base.OnQueried(result);
    }

    #endregion

    #region 新增钩子

    /// <summary>新增前处理 - 校验编码唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_Role entity)
    {
        // 校验编码唯一性
        var result = await Entity.ExistsByCodeAsync(entity.Code);
        if (result.Data == true)
        {
            return (false, $"角色编码 {entity.Code} 已存在");
        }

        // 设置默认启用
        entity.Enable = 1;
        return (true, null);
    }

    #endregion

    #region 修改钩子

    /// <summary>修改前处理</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_Role entity)
    {
        // 校验编码唯一性（排除自身）
        var result = await Entity.ExistsByCodeAsync(entity.Code);
        if (result.Data == true)
        {
            return (false, $"角色编码 {entity.Code} 已存在");
        }
        return (true, null);
    }

    #endregion

    #region 行操作

    /// <summary>启用角色（POST api/SysRole/action/Enable）</summary>
    private async Task<Result<ApiResponse<object?>>> EnableRole(Sys_Role entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("角色不存在");

        var role = result.Data;
        role.Enable = 1;
        var updateResult = await Entity.Update(role, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已启用该角色"));
    }

    /// <summary>禁用角色（POST api/SysRole/action/Disable）</summary>
    private async Task<Result<ApiResponse<object?>>> DisableRole(Sys_Role entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("角色不存在");

        var role = result.Data;
        
        // 禁止禁用超级管理员角色
        if (role.RoleName == "超级管理员")
            return Result<ApiResponse<object?>>.Fail("不能禁用超级管理员角色");

        role.Enable = 0;
        var updateResult = await Entity.Update(role, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该角色"));
    }

    /// <summary>设置权限（POST api/SysRole/action/SetPermission）</summary>
    private async Task<Result<ApiResponse<object?>>> SetPermissionAsync(Sys_Role entity)
    {
        // TODO: 实现权限设置逻辑
        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("权限设置功能待实现"));
    }

    #endregion
}
