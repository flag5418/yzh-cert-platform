using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Models.Request;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     角色管理控制器（新架构版 - 树形表格）
///     
///     继承 TreeTableControllerBase 获得：
///     - 全部单表 CRUD（/filter /add /update /delete /config）
///     - 树能力（/tree/root /tree/children /tree/add /tree/update /tree/delete）
///     - 树→表格联动（选中树节点后自动注入过滤条件到 /filter）
///     
///     路由：api/Role
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Authorize]
public class RoleController : TreeTableControllerBase<Sys_Role, Sys_Role>
{
    public RoleController(
        EntityService<Sys_Role> treeEntityService,
        EntityService<Sys_Role> tableEntityService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        // 树配置
        TreeConfig = new TreeConfig
        {
            RelateField = "ParentCode",
            NameField = "RoleName",
        };

        // 注册行操作
        RegisterRowAction("Enable", EnableRole);
        RegisterRowAction("Disable", DisableRole);
        RegisterRowAction("SetPermission", SetPermissionAsync);
    }

    // ========================================================
    // 钩子
    // ========================================================

    /// <summary>新增前处理 - 校验编码唯一性 + 设置 ParentCode</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_Role entity)
    {
        var result = await Entity.ExistsByCodeAsync(entity.Code);
        if (result.Data == true)
        {
            return (false, $"角色编码 {entity.Code} 已存在");
        }

        // 设置默认启用
        entity.Enable = 1;

        // 如果有 ParentId，查找父节点的 Code 来设置 ParentCode
        if (entity.ParentId > 0 && string.IsNullOrEmpty(entity.ParentCode))
        {
            var parentResult = await TreeEntity.GetOne(r => r.ParentId == entity.ParentId && r.Id != entity.Id);
            if (parentResult.Success && parentResult.Data != null)
            {
                entity.ParentCode = parentResult.Data.Code;
            }
        }

        return (true, null);
    }

    /// <summary>修改前处理 - 校验编码唯一性 + 更新 ParentCode</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_Role entity)
    {
        var result = await Entity.ExistsByCodeAsync(entity.Code);
        if (result.Data == true)
        {
            // 排除自身
            var existing = await Entity.GetByCodeAny(entity.Code);
            if (existing.Success && existing.Data != null && existing.Data.Id != entity.Id)
            {
                return (false, $"角色编码 {entity.Code} 已存在");
            }
        }

        // 更新 ParentCode
        if (entity.ParentId > 0 && string.IsNullOrEmpty(entity.ParentCode))
        {
            var parentResult = await TreeEntity.GetOne(r => r.ParentId == entity.ParentId && r.Id != entity.Id);
            if (parentResult.Success && parentResult.Data != null)
            {
                entity.ParentCode = parentResult.Data.Code;
            }
        }
        else if (entity.ParentId == 0)
        {
            entity.ParentCode = null;
        }

        return (true, null);
    }

    /// <summary>删除前处理 - 检查是否有子角色</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        foreach (var code in codes)
        {
            var result = await TreeEntity.GetByCode(code);
            if (!result.Success || result.Data == null)
                continue;

            var entity = result.Data;

            // 检查是否有子角色
            var children = await TreeEntity.GetChildren(entity.Code);
            if (children != null && children.Count > 0)
            {
                return (false, $"角色「{entity.RoleName}」下有子角色，不能删除");
            }

            // 检查是否为超级管理员
            if (entity.RoleName == "超级管理员")
            {
                return (false, "不能删除超级管理员角色");
            }
        }
        return (true, null);
    }

    // ========================================================
    // 行操作
    // ========================================================

    /// <summary>启用角色</summary>
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

    /// <summary>禁用角色</summary>
    private async Task<Result<ApiResponse<object?>>> DisableRole(Sys_Role entity)
    {
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("角色不存在");

        var role = result.Data;
        if (role.RoleName == "超级管理员")
            return Result<ApiResponse<object?>>.Fail("不能禁用超级管理员角色");

        role.Enable = 0;
        var updateResult = await Entity.Update(role, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该角色"));
    }

    /// <summary>设置权限</summary>
    private async Task<Result<ApiResponse<object?>>> SetPermissionAsync(Sys_Role entity)
    {
        // TODO: 实现权限设置逻辑
        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("权限设置功能待实现"));
    }
}
