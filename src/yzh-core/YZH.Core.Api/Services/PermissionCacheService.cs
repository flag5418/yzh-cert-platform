using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.Api.Interfaces;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
/// 权限缓存服务
/// 负责维护用户权限缓存和展开表
/// </summary>
public class PermissionCacheService : IPermissionCacheService
{
    private readonly IApiRepository _apiRepo;
    private readonly IDbOrm _dbOrm;
    private readonly ILogger<PermissionCacheService> _logger;

    public PermissionCacheService(
        IApiRepository apiRepo,
        IDbOrm dbOrm,
        ILogger<PermissionCacheService> logger)
    {
        _apiRepo = apiRepo;
        _dbOrm = dbOrm;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取用户权限列表
    /// </summary>
    public async Task<List<string>> GetPermissionsAsync(string userCode)
    {
        return await _apiRepo.GetApiCodesByUserCodeAsync(userCode);
    }
    
    /// <summary>
    /// 刷新所有用户权限缓存
    /// </summary>
    public async Task RefreshAllAsync()
    {
        _logger.LogInformation("开始刷新所有用户权限缓存");
        await ExpandPermissionsAsync();
        _logger.LogInformation("权限缓存刷新完成");
    }
    
    /// <summary>
    /// 刷新指定用户权限缓存
    /// </summary>
    public async Task RefreshAsync(string userCode)
    {
        _logger.LogInformation("刷新用户 {UserCode} 权限缓存", userCode);
        await ExpandUserPermissionsAsync(userCode);
    }
    
    /// <summary>
    /// 展开权限到 sys_user_permission 表
    /// </summary>
    private async Task ExpandPermissionsAsync()
    {
        // 清空旧数据（展开表是派生缓存：按「用户-角色-接口」重新推导）
        // 只 INSERT 不 DELETE 会导致撤销授权后残留旧权限，而 HasPermissionAsync
        // 优先命中用户级权限 → 撤销形同无效。
        await _dbOrm.Client.Deleteable<SysUserPermission>().ExecuteCommandAsync();
        await ExpandPermissionsInnerAsync(null);
    }

    private async Task ExpandUserPermissionsAsync(string userCode)
    {
        // 清空用户旧权限
        await _apiRepo.DeleteByUserCodeAsync(userCode);
        await ExpandPermissionsInnerAsync(userCode);
    }

    /// <summary>
    /// 核心展开逻辑：从 Sys_RoleUser + sys_role_api 关联推导用户权限
    /// 用 SqlSugar 分表查询 + 内存拼接 + Storageable 批量 upsert，消除手写 SQL。
    /// 注意：用户-角色关联表是 Sys_RoleUser（不存在 sys_user_role）
    /// </summary>
    private async Task ExpandPermissionsInnerAsync(string? userCode)
    {
        // 1. 查询用户-角色关联（可选按 userCode 过滤）
        var userRoles = userCode == null
            ? await _dbOrm.Client.Queryable<Sys_RoleUser>().Select(ur => new { ur.UserCode, ur.RoleCode }).ToListAsync()
            : await _dbOrm.Client.Queryable<Sys_RoleUser>().Where(ur => ur.UserCode == userCode).Select(ur => new { ur.UserCode, ur.RoleCode }).ToListAsync();

        if (userRoles.Count == 0) return;

        // 2. 查询角色-接口关联（取涉及的 RoleCode 即可，数据量小）
        var roleCodes = userRoles.Select(ur => ur.RoleCode).Distinct().ToList();
        var roleApis = await _dbOrm.Client.Queryable<SysRoleApi>()
            .Where(ra => roleCodes.Contains(ra.RoleCode))
            .Select(ra => new { ra.RoleCode, ra.ApiCode })
            .ToListAsync();

        if (roleApis.Count == 0) return;

        // 3. 内存拼接为 SysUserPermission 实体列表
        var roleApiByRole = roleApis.ToLookup(ra => ra.RoleCode);
        var utcNow = DateTime.UtcNow;
        var records = (
            from ur in userRoles
            from ra in roleApiByRole[ur.RoleCode]
            select new SysUserPermission
            {
                UserCode = ur.UserCode,
                ApiCode = ra.ApiCode,
                CreateTime = utcNow,
            }
        ).ToList();

        if (records.Count == 0) return;

        // 4. 批量 INSERT（前置已 DELETE 旧数据，无需 upsert 语义）
        await _dbOrm.Client.Insertable(records).ExecuteCommandAsync();
    }
}
