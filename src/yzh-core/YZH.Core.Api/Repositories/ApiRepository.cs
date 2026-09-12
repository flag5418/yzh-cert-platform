using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        var result = await _dbOrm.SqlQueryAsync<SysApi>("SELECT * FROM sys_api ORDER BY group_path, path");
        return result.Success ? (result.Data ?? new List<SysApi>()) : new List<SysApi>();
    }

    public async Task<SysApi?> GetByCodeAsync(string code)
    {
        var result = await _dbOrm.QueryFirstOrDefaultAsync<SysApi>("SELECT * FROM sys_api WHERE code = @code", new { code });
        return result.Success ? result.Data : null;
    }

    public async Task<int> InsertBatchAsync(List<SysApi> apis)
    {
        if (!apis.Any()) return 0;

        var sql = @"
            INSERT INTO sys_api (code, method, path, group_path, name, author, enable, create_date)
            VALUES (@Code, @Method, @Path, @GroupPath, @Name, @Author, @Enable, @CreateDate)";

        var count = 0;
        foreach (var api in apis)
        {
            var result = await _dbOrm.SqlExecuteAsync(sql, api);
            if (result.Success) count += result.Data;
        }
        return count;
    }

    public async Task<int> UpdateBatchAsync(List<SysApi> apis)
    {
        if (!apis.Any()) return 0;

        var sql = @"
            UPDATE sys_api SET
                name = @Name,
                author = @Author,
                group_path = @GroupPath,
                update_date = NOW()
            WHERE code = @Code";

        var count = 0;
        foreach (var api in apis)
        {
            var result = await _dbOrm.SqlExecuteAsync(sql, api);
            if (result.Success) count += result.Data;
        }
        return count;
    }

    public async Task<int> DeleteBatchAsync(List<string> codes)
    {
        if (!codes.Any()) return 0;

        // SqlSugar 的 IN 查询需要手动构建参数占位符
        // 使用 ExpandoObject 以便 ToSugarParameters 能正确读取属性
        var parameters = new List<string>();
        dynamic paramObj = new System.Dynamic.ExpandoObject();
        var dict = (IDictionary<string, object>)paramObj;
        for (int i = 0; i < codes.Count; i++)
        {
            var paramName = $"c{i}";
            parameters.Add($"@{paramName}");
            dict[paramName] = codes[i];
        }
        var inClause = string.Join(",", parameters);
        var sql = $"DELETE FROM sys_api WHERE code IN ({inClause})";
        var result = await _dbOrm.SqlExecuteAsync(sql, paramObj);
        return result.Success ? result.Data : 0;
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, object? param = null)
    {
        var result = await _dbOrm.SqlExecuteAsync(sql, param);
        return result.Success ? result.Data : 0;
    }

    /// <summary>
    /// 构建 IN 查询的参数占位符（返回 ExpandoObject 以兼容 SqlSugar 的 ToSugarParameters）
    /// </summary>
    private static (string inClause, dynamic paramObj) BuildInClause(List<string> values, string prefix)
    {
        var parameters = new List<string>();
        dynamic paramObj = new System.Dynamic.ExpandoObject();
        var dict = (IDictionary<string, object>)paramObj;
        for (int i = 0; i < values.Count; i++)
        {
            var paramName = $"{prefix}{i}";
            parameters.Add($"@{paramName}");
            dict[paramName] = values[i];
        }
        return (string.Join(",", parameters), paramObj);
    }

    /// <summary>
    /// 清理 sys_role_api 中无效的 api_code
    /// </summary>
    public async Task<int> CleanupRoleApiAsync(List<string> invalidCodes)
    {
        if (!invalidCodes.Any()) return 0;
        var (inClause, paramObj) = BuildInClause(invalidCodes, "rc");
        var sql = $"DELETE FROM sys_role_api WHERE api_code IN ({inClause})";
        var result = await _dbOrm.SqlExecuteAsync(sql, paramObj);
        return result.Success ? result.Data : 0;
    }

    /// <summary>
    /// 清理 sys_user_permission 中无效的 api_code
    /// </summary>
    public async Task<int> CleanupUserPermissionAsync(List<string> invalidCodes)
    {
        if (!invalidCodes.Any()) return 0;
        var (inClause, paramObj) = BuildInClause(invalidCodes, "uc");
        var sql = $"DELETE FROM sys_user_permission WHERE api_code IN ({inClause})";
        var result = await _dbOrm.SqlExecuteAsync(sql, paramObj);
        return result.Success ? result.Data : 0;
    }

    // SysRoleApi 相关方法
    public async Task<int> InsertRoleApiAsync(string roleCode, string apiCode)
    {
        var sql = "INSERT INTO sys_role_api (role_code, api_code) VALUES (@roleCode, @apiCode)";
        var result = await _dbOrm.SqlExecuteAsync(sql, new { roleCode, apiCode });
        return result.Success ? result.Data : 0;
    }

    public async Task<int> DeleteByRoleCodeAsync(string roleCode)
    {
        var sql = "DELETE FROM sys_role_api WHERE role_code = @roleCode";
        var result = await _dbOrm.SqlExecuteAsync(sql, new { roleCode });
        return result.Success ? result.Data : 0;
    }

    public async Task<List<string>> GetApiCodesByRoleCodeAsync(string roleCode)
    {
        var sql = "SELECT api_code FROM sys_role_api WHERE role_code = @roleCode";
        var result = await _dbOrm.SqlQueryAsync<ApiCodeDto>(sql, new { roleCode });
        return result.Success ? (result.Data?.Select(d => d.api_code).ToList() ?? new List<string>()) : new List<string>();
    }

    // SysUserPermission 相关方法
    public async Task<int> InsertUserPermissionAsync(string userCode, string apiCode)
    {
        var sql = @"INSERT IGNORE INTO sys_user_permission (user_code, api_code) VALUES (@userCode, @apiCode)";
        var result = await _dbOrm.SqlExecuteAsync(sql, new { userCode, apiCode });
        return result.Success ? result.Data : 0;
    }

    public async Task<int> DeleteByUserCodeAsync(string userCode)
    {
        var sql = "DELETE FROM sys_user_permission WHERE user_code = @userCode";
        var result = await _dbOrm.SqlExecuteAsync(sql, new { userCode });
        return result.Success ? result.Data : 0;
    }

    public async Task<List<string>> GetApiCodesByUserCodeAsync(string userCode)
    {
        var sql = "SELECT api_code FROM sys_user_permission WHERE user_code = @userCode";
        var result = await _dbOrm.SqlQueryAsync<ApiCodeDto>(sql, new { userCode });
        return result.Success ? (result.Data?.Select(d => d.api_code).ToList() ?? new List<string>()) : new List<string>();
    }

    private class ApiCodeDto
    {
        public string api_code { get; set; } = "";
    }

    /// <summary>获取所有角色-接口关联</summary>
    public async Task<List<RoleApiAssociation>> GetAllRoleApiAssociationsAsync()
    {
        var sql = "SELECT role_code, api_code FROM sys_role_api";
        var result = await _dbOrm.SqlQueryAsync<RoleApiAssociationDto>(sql);
        return result.Success ? (result.Data?.Select(d => new RoleApiAssociation { RoleCode = d.role_code, ApiCode = d.api_code }).ToList() ?? new List<RoleApiAssociation>()) : new List<RoleApiAssociation>();
    }

    private class RoleApiAssociationDto
    {
        public string role_code { get; set; } = "";
        public string api_code { get; set; } = "";
    }
}
