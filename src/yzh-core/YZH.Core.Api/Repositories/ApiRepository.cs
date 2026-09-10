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
        return await _dbOrm.QueryAsync<SysApi>("SELECT * FROM sys_api ORDER BY path");
    }
    
    public async Task<SysApi?> GetByCodeAsync(string code)
    {
        return await _dbOrm.QuerySingleAsync<SysApi>("SELECT * FROM sys_api WHERE code = @code", new { code });
    }
    
    public async Task<int> InsertBatchAsync(List<SysApi> apis)
    {
        if (!apis.Any()) return 0;
        
        var sql = @"
            INSERT INTO sys_api (code, method, path, tree_path, name, author, enable, create_date)
            VALUES @apis";
        
        return await _dbOrm.ExecuteNonQueryAsync(sql, new { apis });
    }
    
    public async Task<int> UpdateBatchAsync(List<SysApi> apis)
    {
        if (!apis.Any()) return 0;
        
        var sql = @"
            UPDATE sys_api SET
                name = @name,
                author = @author,
                update_date = NOW()
            WHERE code = @code";
        
        var count = 0;
        foreach (var api in apis)
        {
            count += await _dbOrm.ExecuteNonQueryAsync(sql, api);
        }
        return count;
    }
    
    public async Task<int> DeleteBatchAsync(List<string> codes)
    {
        if (!codes.Any()) return 0;
        
        var sql = "DELETE FROM sys_api WHERE code IN @codes";
        return await _dbOrm.ExecuteNonQueryAsync(sql, new { codes });
    }
    
    public async Task<int> ExecuteNonQueryAsync(string sql, object? param = null)
    {
        return await _dbOrm.ExecuteNonQueryAsync(sql, param);
    }
    
    // SysRoleApi 相关方法
    public async Task<int> InsertRoleApiAsync(string roleCode, string apiCode)
    {
        var sql = "INSERT INTO sys_role_api (role_code, api_code) VALUES (@roleCode, @apiCode)";
        return await _dbOrm.ExecuteNonQueryAsync(sql, new { roleCode, apiCode });
    }
    
    public async Task<int> DeleteByRoleCodeAsync(string roleCode)
    {
        var sql = "DELETE FROM sys_role_api WHERE role_code = @roleCode";
        return await _dbOrm.ExecuteNonQueryAsync(sql, new { roleCode });
    }
    
    public async Task<List<string>> GetApiCodesByRoleCodeAsync(string roleCode)
    {
        var sql = "SELECT api_code FROM sys_role_api WHERE role_code = @roleCode";
        return await _dbOrm.QueryAsync<string>(sql, new { roleCode });
    }
    
    // SysUserPermission 相关方法
    public async Task<int> InsertUserPermissionAsync(string userCode, string apiCode)
    {
        var sql = @"INSERT IGNORE INTO sys_user_permission (user_code, api_code) VALUES (@userCode, @apiCode)";
        return await _dbOrm.ExecuteNonQueryAsync(sql, new { userCode, apiCode });
    }
    
    public async Task<int> DeleteByUserCodeAsync(string userCode)
    {
        var sql = "DELETE FROM sys_user_permission WHERE user_code = @userCode";
        return await _dbOrm.ExecuteNonQueryAsync(sql, new { userCode });
    }
    
    public async Task<List<string>> GetApiCodesByUserCodeAsync(string userCode)
    {
        var sql = "SELECT api_code FROM sys_user_permission WHERE user_code = @userCode";
        return await _dbOrm.QueryAsync<string>(sql, new { userCode });
    }
}
