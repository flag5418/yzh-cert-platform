using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Organization;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
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
///     - 树形表格选择器（/checkTree /check/add /check/remove）
///     
///     路由：api/Role
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Route("api/[controller]")]
[Authorize]
public class RoleController : TreeTableControllerBase<Sys_Role, Sys_Role>
{
    private readonly EntityService<Sys_User> _userService;
    private readonly EntityService<Sys_Organization> _orgService;
    private readonly EntityService<Sys_RoleUser> _roleUserService;
    private readonly IDbOrm _dbOrm;

    public RoleController(
        EntityService<Sys_Role> treeEntityService,
        EntityService<Sys_Role> tableEntityService,
        EntityService<Sys_User> userService,
        EntityService<Sys_Organization> orgService,
        EntityService<Sys_RoleUser> roleUserService,
        IDbOrm dbOrm,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _userService = userService;
        _orgService = orgService;
        _roleUserService = roleUserService;
        _dbOrm = dbOrm;

        // 树配置
        TreeConfig = new TreeConfig
        {
            RelateField = "ParentCode",
            NameField = "RoleName",
        };

        // 注册行操作
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

        entity.Enable = 1;

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
            var existing = await Entity.GetByCodeAny(entity.Code);
            if (existing.Success && existing.Data != null && existing.Data.Id != entity.Id)
            {
                return (false, $"角色编码 {entity.Code} 已存在");
            }
        }

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

            var children = await TreeEntity.GetChildren(entity.Code);
            if (children != null && children.Count > 0)
            {
                return (false, $"角色「{entity.RoleName}」下有子角色，不能删除");
            }

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
        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("权限设置功能待实现"));
    }

    // ========================================================
    // 树形表格选择器 - 角色-用户关联（通过 Sys_RoleUser 关联表）
    // 设计原则：Code 是唯一业务键，所有关联必须用 Code
    // ========================================================

    /// <summary>
    /// 获取机构+用户混合树数据（含勾选状态）
    /// 左侧选择角色后调用，返回全量数据 + 当前角色的关联标记
    /// </summary>
    [HttpPost("checkTree")]
    public override async Task<ActionResult<ApiResponse<CheckTreeNodeDto[]>>> GetCheckTree(
        [FromBody] CheckTreeRequest request)
    {
        try
        {
            // 1. 验证角色存在（用 Code 关联）
            var roleResult = await TreeEntity.GetByCode(request.ContextCode);
            if (!roleResult.Success || roleResult.Data == null)
                return BadRequest(ApiResponse<CheckTreeNodeDto[]>.Fail("角色不存在"));

            // 2. 查所有机构
            var orgResult = await _orgService.GetListAsync();
            var orgs = orgResult.Success ? orgResult.Data ?? new() : new();

            // 3. 查所有用户
            var userResult = await _userService.GetListAsync();
            var users = userResult.Success ? userResult.Data ?? new() : new();

            // 4. 查当前角色已关联的用户 Code 集合（从 Sys_RoleUser 关联表，用 Code 关联）
            var roleUserResult = await _roleUserService.GetListAsync(
                r => r.RoleCode == request.ContextCode);
            var roleUserCodes = roleUserResult.Success
                ? (roleUserResult.Data ?? new()).Select(r => r.UserCode).ToHashSet()
                : new HashSet<string>();

            // 5. 构建 CheckTreeNodeDto[]
            var nodes = new List<CheckTreeNodeDto>();

            // 机构节点（Code 使用 GUID Code，与 ParentCode 值域一致）
            foreach (var org in orgs)
            {
                nodes.Add(new CheckTreeNodeDto
                {
                    Code = org.Code,
                    Name = org.OrgName,
                    ParentCode = org.ParentCode,
                    NodeType = "org",
                    CheckFlag = false,
                });
            }

            // 用户节点
            foreach (var user in users)
            {
                nodes.Add(new CheckTreeNodeDto
                {
                    Code = user.Code,
                    Name = user.UserTrueName ?? user.UserName,
                    ParentCode = user.OrgCode,
                    NodeType = "user",
                    CheckFlag = roleUserCodes.Contains(user.Code),
                    Extra = new Dictionary<string, object>
                    {
                        ["UserName"] = user.UserName,
                        ["OrgCode"] = user.OrgCode ?? "",
                    },
                });
            }

            return Ok(ApiResponse<CheckTreeNodeDto[]>.Ok(nodes.ToArray()));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<CheckTreeNodeDto[]>.Fail($"获取角色用户数据失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 勾选保存 - 给角色分配用户（写入 Sys_RoleUser 关联表，用 Code 关联）
    /// </summary>
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

            // 只处理用户类型的节点
            var userCodes = request.Selections
                .Where(s => s.NodeType == "user")
                .Select(s => s.Code)
                .ToList();

            if (userCodes.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            // 查询已存在的关联（避免重复插入）
            var existingResult = await _roleUserService.GetListAsync(
                r => r.RoleCode == roleCode && userCodes.Contains(r.UserCode));
            var existingUserCodes = existingResult.Success
                ? (existingResult.Data ?? new()).Select(r => r.UserCode).ToHashSet()
                : new HashSet<string>();

            // 插入新关联（RoleCode + UserCode 都是 Code）
            int inserted = 0;
            foreach (var userCode in userCodes)
            {
                if (existingUserCodes.Contains(userCode))
                    continue;

                var entity = new Sys_RoleUser
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Code = Guid.NewGuid().ToString("N"),
                    RoleCode = roleCode,
                    UserCode = userCode,
                };
                var result = await _roleUserService.Insert(entity, UserContext.ClientIp);
                if (result.Success) inserted++;
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = inserted }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"保存失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 取消勾选 - 移除角色与用户的关联（删除 Sys_RoleUser 记录，用 Code 关联）
    /// </summary>
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

            // 只处理用户类型的节点
            var userCodes = request.Selections
                .Where(s => s.NodeType == "user")
                .Select(s => s.Code)
                .ToList();

            if (userCodes.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            // 查询要删除的关联（用 Code 关联）
            var toDeleteResult = await _roleUserService.GetListAsync(
                r => r.RoleCode == roleCode && userCodes.Contains(r.UserCode));
            var toDelete = toDeleteResult.Success ? toDeleteResult.Data ?? new() : new();

            // 删除关联（Sys_RoleUser 没有 Code 列，直接用 Id 通过原生 SQL 删除）
            int deleted = 0;
            foreach (var entity in toDelete)
            {
                var result = await _dbOrm.SqlExecuteAsync(
                    "DELETE FROM Sys_RoleUser WHERE Id = @Id",
                    new { entity.Id });
                if (result.Success && result.Data > 0) deleted++;
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = deleted }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"移除失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 获取所有角色-用户关联（用于前端本地缓存）
    /// 页面加载时调用一次，后续切换角色时直接从本地缓存计算 CheckFlag
    /// </summary>
    [HttpPost("check/all")]
    public override async Task<ActionResult<ApiResponse<AssociationDto[]>>> GetAllAssociations()
    {
        try
        {
            var result = await _roleUserService.GetListAsync();
            var list = result.Success ? result.Data ?? new() : new();

            var associations = list
                .Where(r => !string.IsNullOrEmpty(r.RoleCode) && !string.IsNullOrEmpty(r.UserCode))
                .Select(r => new AssociationDto
                {
                    ContextCode = r.RoleCode,
                    TargetCode = r.UserCode,
                    NodeType = "user",
                })
                .ToArray();

            return Ok(ApiResponse<AssociationDto[]>.Ok(associations));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AssociationDto[]>.Fail($"获取关联数据失败：{ex.Message}"));
        }
    }
}
