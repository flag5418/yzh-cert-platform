using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Interfaces;
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
///     角色-接口权限管理控制器（左树右表选择器）
///
///     复用 RoleMenu 模式：
///     - 左侧：角色树
///     - 右侧：接口分组树 + 接口勾选
///
///     关联表：sys_role_api (RoleCode + ApiCode)
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Route("api/[controller]")]
[Authorize]
public class RoleApiController : TreeTableControllerBase<Sys_Role, SysApi>
{
    private readonly IApiRepository _apiRepo;
    private readonly EntityService<Sys_Role> _roleService;

    public RoleApiController(
        EntityService<Sys_Role> treeEntityService,
        EntityService<SysApi> tableEntityService,
        IApiRepository apiRepo,
        IDbOrm dbOrm,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _apiRepo = apiRepo;
        _roleService = treeEntityService;

        TreeConfig = new TreeConfig
        {
            NameField = "RoleName",
            CodeField = "Code",
            ParentCodeField = "ParentCode",
            RelateField = "ParentCode",
        };
    }

    // ========================================================
    // 树形表格选择器 - 角色-接口关联（通过 sys_role_api 关联表）
    // ========================================================

    /// <summary>获取接口树数据（含勾选状态）</summary>
    [ApiDescription("获取角色接口树", "系统", "系统管理/接口权限", true)]
    [HttpPost("checkTree")]
    public override async Task<ActionResult<ApiResponse<CheckTreeNodeDto[]>>> GetCheckTree(
        [FromBody] CheckTreeRequest request)
    {
        try
        {
            // 1. 校验角色存在
            var roleResult = await _roleService.GetByCode(request.ContextCode);
            if (!roleResult.Success || roleResult.Data == null)
                return BadRequest(ApiResponse<CheckTreeNodeDto[]>.Fail("角色不存在"));

            // 2. 全部接口
            var apis = await _apiRepo.GetAllAsync();

            // 3. 当前角色已授权的接口 Code
            var granted = await _apiRepo.GetApiCodesByRoleCodeAsync(request.ContextCode);

            // 4. 构建扁平节点（分组作为父节点）
            var grantedSet = granted.ToHashSet();
            var groupPaths = apis
                .Where(a => !string.IsNullOrEmpty(a.GroupPath))
                .Select(a => a.GroupPath)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            var nodes = new List<CheckTreeNodeDto>();

            foreach (var groupPath in groupPaths)
            {
                // 父节点：分组
                var groupCode = $"group:{groupPath}";
                nodes.Add(new CheckTreeNodeDto
                {
                    Code = groupCode,
                    Name = groupPath,
                    ParentCode = null,
                    NodeType = "group",
                    CheckFlag = false,
                    Extra = new Dictionary<string, object>
                    {
                        ["IsGroup"] = true,
                    },
                });

                // 子节点：接口
                var groupApis = apis
                    .Where(a => a.GroupPath == groupPath)
                    .OrderBy(a => a.Path);
                foreach (var api in groupApis)
                {
                    nodes.Add(new CheckTreeNodeDto
                    {
                        Code = api.Code,
                        Name = $"{api.Method} {api.Path} - {api.Name}",
                        ParentCode = groupCode,
                        NodeType = "api",
                        CheckFlag = grantedSet.Contains(api.Code),
                        Extra = new Dictionary<string, object>
                        {
                            ["Method"] = api.Method,
                            ["Path"] = api.Path,
                            ["ApiName"] = api.Name,
                            ["Enable"] = api.Enable,
                        },
                    });
                }
            }

            return Ok(ApiResponse<CheckTreeNodeDto[]>.Ok(nodes.ToArray()));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<CheckTreeNodeDto[]>.Fail($"获取角色接口数据失败：{ex.Message}"));
        }
    }

    /// <summary>勾选保存 - 给角色授权接口（写入 sys_role_api）</summary>
    [ApiDescription("保存角色接口权限", "系统", "系统管理/接口权限", true)]
    [HttpPost("check/add")]
    public override async Task<ActionResult<ApiResponse<object?>>> CheckAdd(
        [FromBody] CheckActionRequest request)
    {
        try
        {
            var roleCode = request.ContextCode;

            var selectedApiCodes = request.Selections
                .Where(s => s.NodeType == "api")
                .Select(s => s.Code)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            if (selectedApiCodes.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            // 查询已存在的关联
            var existing = await _apiRepo.GetApiCodesByRoleCodeAsync(roleCode);
            var existingSet = existing.ToHashSet();

            int inserted = 0;
            foreach (var apiCode in selectedApiCodes)
            {
                if (existingSet.Contains(apiCode))
                    continue;

                var result = await _apiRepo.InsertRoleApiAsync(roleCode, apiCode);
                if (result > 0) inserted++;
            }

            return Ok(ApiResponse<object?>.Ok(new { Updated = inserted }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"保存失败：{ex.Message}"));
        }
    }

    /// <summary>取消勾选 - 移除角色与接口的关联（删除 sys_role_api 记录）</summary>
    [ApiDescription("移除角色接口权限", "系统", "系统管理/接口权限", true)]
    [HttpPost("check/remove")]
    public override async Task<ActionResult<ApiResponse<object?>>> CheckRemove(
        [FromBody] CheckActionRequest request)
    {
        try
        {
            var roleCode = request.ContextCode;

            var apiCodes = request.Selections
                .Where(s => s.NodeType == "api")
                .Select(s => s.Code)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            if (apiCodes.Count == 0)
                return Ok(ApiResponse<object?>.Ok(new { Updated = 0 }));

            string apiCodesStr = string.Join(",", apiCodes);
            var sql = "DELETE FROM sys_role_api WHERE role_code = @roleCode AND api_code IN @apiCodes";
            var result = await _apiRepo.ExecuteNonQueryAsync(sql, new { roleCode, apiCodes });

            return Ok(ApiResponse<object?>.Ok(new { Updated = result }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object?>.Fail($"移除失败：{ex.Message}"));
        }
    }

    /// <summary>获取所有角色-接口关联（用于前端本地缓存）</summary>
    [ApiDescription("获取所有角色接口关联", "系统", "系统管理/接口权限", true)]
    [HttpPost("check/all")]
    public override async Task<ActionResult<ApiResponse<AssociationDto[]>>> GetAllAssociations()
    {
        try
        {
            var items = await _apiRepo.GetAllRoleApiAssociationsAsync();

            var associations = items
                .Select(r => new AssociationDto
                {
                    ContextCode = r.RoleCode,
                    TargetCode = r.ApiCode,
                    NodeType = "api",
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
