using System.Collections.Generic;
using System.Threading.Tasks;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Interfaces;

/// <summary>
/// 接口仓储接口
/// </summary>
public interface IApiRepository
{
    /// <summary>
    /// 获取所有接口
    /// </summary>
    Task<List<SysApi>> GetAllAsync();
    
    /// <summary>
    /// 根据 ApiCode 获取接口
    /// </summary>
    Task<SysApi?> GetByCodeAsync(string code);
    
    /// <summary>
    /// 批量插入接口
    /// </summary>
    Task<int> InsertBatchAsync(List<SysApi> apis);
    
    /// <summary>
    /// 批量更新接口
    /// </summary>
    Task<int> UpdateBatchAsync(List<SysApi> apis);
    
    /// <summary>
    /// 批量删除接口
    /// </summary>
    Task<int> DeleteBatchAsync(List<string> codes);
    
    /// <summary>
    /// 执行原始 SQL
    /// </summary>
    Task<int> ExecuteNonQueryAsync(string sql, object? param = null);
}

/// <summary>
/// 权限缓存服务接口
/// </summary>
public interface IPermissionCacheService
{
    /// <summary>
    /// 获取用户权限列表
    /// </summary>
    Task<List<string>> GetPermissionsAsync(string userCode);
    
    /// <summary>
    /// 刷新所有用户权限缓存
    /// </summary>
    Task RefreshAllAsync();
    
    /// <summary>
    /// 刷新指定用户权限缓存
    /// </summary>
    Task RefreshAsync(string userCode);
}
