using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Interfaces;

namespace YZH.Core.Api.Services;

/// <summary>
/// 权限缓存服务
/// 负责维护用户权限缓存和展开表
/// </summary>
public class PermissionCacheService : IPermissionCacheService
{
    private readonly IApiRepository _apiRepo;
    private readonly ILogger<PermissionCacheService> _logger;
    
    public PermissionCacheService(
        IApiRepository apiRepo,
        ILogger<PermissionCacheService> logger)
    {
        _apiRepo = apiRepo;
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
        await _apiRepo.ExecuteNonQueryAsync("DELETE FROM sys_user_permission");

        // 注意：用户-角色关联表是 Sys_RoleUser（不存在 sys_user_role，写错会导致
        // 整条权限展开语句失败 → sys_user_permission 永远为空、用户级接口权限失效）
        var sql = @"
            INSERT INTO sys_user_permission (user_code, api_code)
            SELECT ur.UserCode, ra.api_code
            FROM Sys_RoleUser ur
            JOIN sys_role_api ra ON ur.RoleCode = ra.role_code
            ON DUPLICATE KEY UPDATE update_date = NOW()";
        await _apiRepo.ExecuteNonQueryAsync(sql);
    }
    
    private async Task ExpandUserPermissionsAsync(string userCode)
    {
        // 清空用户旧权限
        await _apiRepo.DeleteByUserCodeAsync(userCode);
        
        // 重新展开
        var sql = @"
            INSERT INTO sys_user_permission (user_code, api_code)
            SELECT ur.UserCode, ra.api_code
            FROM Sys_RoleUser ur
            JOIN sys_role_api ra ON ur.RoleCode = ra.role_code
            WHERE ur.UserCode = @userCode
            ON DUPLICATE KEY UPDATE update_date = NOW()";
        await _apiRepo.ExecuteNonQueryAsync(sql, new { userCode });
    }
}
