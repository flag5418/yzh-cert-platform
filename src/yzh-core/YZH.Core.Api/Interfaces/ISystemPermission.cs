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

    /// <summary>
    /// 清理 sys_role_api 中无效的 api_code
    /// </summary>
    Task<int> CleanupRoleApiAsync(List<string> invalidCodes);

    /// <summary>
    /// 清理 sys_user_permission 中无效的 api_code
    /// </summary>
    Task<int> CleanupUserPermissionAsync(List<string> invalidCodes);

    // ---- SysRoleApi 方法 ----

    /// <summary>插入角色-接口关联</summary>
    Task<int> InsertRoleApiAsync(string roleCode, string apiCode);

    /// <summary>删除角色的所有接口关联</summary>
    Task<int> DeleteByRoleCodeAsync(string roleCode);

    /// <summary>获取角色的接口编码列表</summary>
    Task<List<string>> GetApiCodesByRoleCodeAsync(string roleCode);

    // ---- SysUserPermission 方法 ----

    /// <summary>插入用户权限</summary>
    Task<int> InsertUserPermissionAsync(string userCode, string apiCode);

    /// <summary>删除用户的所有权限</summary>
    Task<int> DeleteByUserCodeAsync(string userCode);

    /// <summary>获取用户的接口编码列表</summary>
    Task<List<string>> GetApiCodesByUserCodeAsync(string userCode);

    /// <summary>获取所有角色-接口关联</summary>
    Task<List<RoleApiAssociation>> GetAllRoleApiAssociationsAsync();
}

/// <summary>角色-接口关联 DTO</summary>
public class RoleApiAssociation
{
    public string RoleCode { get; set; } = "";
    public string ApiCode { get; set; } = "";
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
