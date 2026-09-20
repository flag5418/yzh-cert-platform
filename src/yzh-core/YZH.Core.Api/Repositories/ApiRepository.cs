using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.Api.Interfaces;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Repositories;

/// <summary>
/// 接口仓储实现
/// </summary>
public class ApiRepository : IApiRepository
{
    private readonly IDbOrm _dbOrm;
    private readonly ILogger<ApiRepository> _logger;

    public ApiRepository(IDbOrm dbOrm, ILogger<ApiRepository> logger)
    {
        _dbOrm = dbOrm;
        _logger = logger;
    }

    public async Task<List<SysApi>> GetAllAsync()
    {
        return await _dbOrm.Client.Queryable<SysApi>()
            .OrderBy(x => x.GroupPath)
            .OrderBy(x => x.Path)
            .ToListAsync();
    }

    public async Task<SysApi?> GetByCodeAsync(string code)
    {
        return await _dbOrm.Client.Queryable<SysApi>()
            .Where(x => x.Code == code)
            .FirstAsync();
    }

    public async Task<int> InsertBatchAsync(List<SysApi> apis)
    {
        if (!apis.Any()) return 0;
        return await _dbOrm.Client.Insertable(apis).ExecuteCommandAsync();
    }

    public async Task<int> UpdateBatchAsync(List<SysApi> apis)
    {
        if (!apis.Any()) return 0;
        return await _dbOrm.Client.Updateable(apis)
            .UpdateColumns(x => new { x.Path, x.Name, x.Author, x.GroupPath, x.UpdateTime })
            .ExecuteCommandAsync();
    }

    public async Task<int> DeleteBatchAsync(List<string> codes)
    {
        if (!codes.Any()) return 0;
        return await _dbOrm.Client.Deleteable<SysApi>()
            .In(x => x.Code, codes)
            .ExecuteCommandAsync();
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, object? param = null)
    {
        var result = await _dbOrm.SqlExecuteAsync(sql, param);
        return result.Success ? result.Data : 0;
    }

    /// <summary>
    /// 清理 sys_role_api 中无效的 api_code
    /// </summary>
    public async Task<int> CleanupRoleApiAsync(List<string> invalidCodes)
    {
        if (!invalidCodes.Any()) return 0;
        return await _dbOrm.Client.Deleteable<SysRoleApi>()
            .In(x => x.ApiCode, invalidCodes)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 清理 sys_user_permission 中无效的 api_code
    /// </summary>
    public async Task<int> CleanupUserPermissionAsync(List<string> invalidCodes)
    {
        if (!invalidCodes.Any()) return 0;
        return await _dbOrm.Client.Deleteable<SysUserPermission>()
            .In(x => x.ApiCode, invalidCodes)
            .ExecuteCommandAsync();
    }

    // SysRoleApi 相关方法
    public async Task<int> InsertRoleApiAsync(string roleCode, string apiCode)
    {
        return await _dbOrm.Client.Insertable(new SysRoleApi
        {
            RoleCode = roleCode,
            ApiCode = apiCode,
            CreateTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    public async Task<int> DeleteByRoleCodeAsync(string roleCode)
    {
        return await _dbOrm.Client.Deleteable<SysRoleApi>()
            .Where(x => x.RoleCode == roleCode)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 按角色 + 接口编码列表批量删除（SqlSugar 方式，禁止手写 SQL）
    /// </summary>
    public async Task<int> DeleteByRoleAndApiCodesAsync(string roleCode, List<string> apiCodes)
    {
        return await _dbOrm.Client.Deleteable<SysRoleApi>()
            .Where(x => x.RoleCode == roleCode && apiCodes.Contains(x.ApiCode))
            .ExecuteCommandAsync();
    }

    public async Task<List<string>> GetApiCodesByRoleCodeAsync(string roleCode)
    {
        return await _dbOrm.Client.Queryable<SysRoleApi>()
            .Where(x => x.RoleCode == roleCode)
            .Select(x => x.ApiCode)
            .ToListAsync();
    }

    // SysUserPermission 相关方法
    public async Task<int> InsertUserPermissionAsync(string userCode, string apiCode)
    {
        return await _dbOrm.Client.Storageable(new SysUserPermission
        {
            UserCode = userCode,
            ApiCode = apiCode,
            CreateTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    public async Task<int> DeleteByUserCodeAsync(string userCode)
    {
        return await _dbOrm.Client.Deleteable<SysUserPermission>()
            .Where(x => x.UserCode == userCode)
            .ExecuteCommandAsync();
    }

    public async Task<List<string>> GetApiCodesByUserCodeAsync(string userCode)
    {
        return await _dbOrm.Client.Queryable<SysUserPermission>()
            .Where(x => x.UserCode == userCode)
            .Select(x => x.ApiCode)
            .ToListAsync();
    }

    /// <summary>获取所有角色-接口关联</summary>
    public async Task<List<RoleApiAssociation>> GetAllRoleApiAssociationsAsync()
    {
        return await _dbOrm.Client.Queryable<SysRoleApi>()
            .Select(x => new RoleApiAssociation { RoleCode = x.RoleCode, ApiCode = x.ApiCode })
            .ToListAsync();
    }
}
