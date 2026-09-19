using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Api.Services;

/// <summary>
/// 角色查询服务实现（封装跨表 SQL，从 Controller 中抽取）
/// </summary>
public class RoleService : IRoleService
{
    private readonly IDbOrm _db;

    public RoleService(IDbOrm db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Result<Dictionary<string, string>>> GetRoleNamesByUserCodesAsync(IEnumerable<string> userCodes)
    {
        var codeList = userCodes.Where(c => !string.IsNullOrEmpty(c)).ToList();
        if (!codeList.Any())
            return Result<Dictionary<string, string>>.Ok(new Dictionary<string, string>());

        var roleResult = await _db.SqlQueryAsync<RoleNameDto>(
            @"SELECT ru.UserCode, r.RoleName FROM Sys_RoleUser ru
              INNER JOIN Sys_Role r ON ru.RoleCode = r.Code
              WHERE ru.UserCode IN @UserCodes AND ru.IsDeleted = 0 AND r.IsDeleted = 0",
            new { UserCodes = codeList });

        var dict = new Dictionary<string, string>();
        if (roleResult.Success && roleResult.Data != null)
        {
            foreach (var item in roleResult.Data)
            {
                if (!string.IsNullOrEmpty(item.UserCode) && !string.IsNullOrEmpty(item.RoleName))
                    dict[item.UserCode] = item.RoleName;
            }
        }

        return Result<Dictionary<string, string>>.Ok(dict);
    }

    /// <inheritdoc />
    public async Task<Result<string?>> GetRoleCodeByUserCodeAsync(string userCode)
    {
        var roleResult = await _db.QueryFirstOrDefaultAsync<RoleCodeCheckDto>(
            @"SELECT r.Code FROM Sys_RoleUser ru
              INNER JOIN Sys_Role r ON ru.RoleCode = r.Code
              WHERE ru.UserCode = @UserCode AND ru.IsDeleted = 0 AND r.IsDeleted = 0",
            new { UserCode = userCode });

        return Result<string?>.Ok(roleResult.Data?.Code);
    }

    // ── DTOs（内部使用，替代 Controller 中的重复定义）──
    private class RoleNameDto
    {
        public string? UserCode { get; set; }
        public string? RoleName { get; set; }
    }

    private class RoleCodeCheckDto
    {
        public string? Code { get; set; }
    }
}
