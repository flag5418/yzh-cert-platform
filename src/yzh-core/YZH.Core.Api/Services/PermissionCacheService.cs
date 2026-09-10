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
        // 清空旧数据
        var sql = @"
            INSERT INTO sys_user_permission (user_code, api_code)
            SELECT ur.user_code, ra.api_code
            FROM sys_user_role ur
            JOIN sys_role_api ra ON ur.role_code = ra.role_code
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
            SELECT ur.user_code, ra.api_code
            FROM sys_user_role ur
            JOIN sys_role_api ra ON ur.role_code = ra.role_code
            WHERE ur.user_code = @userCode
            ON DUPLICATE KEY UPDATE update_date = NOW()";
        await _apiRepo.ExecuteNonQueryAsync(sql, new { userCode });
    }
}
