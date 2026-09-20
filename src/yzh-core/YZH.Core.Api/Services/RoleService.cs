using SqlSugar;
using YZH.Core.Api.Models.System;
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

        var dict = await _db.Client.Queryable<Sys_RoleUser, Sys_Role>(
                (ru, r) => ru.RoleCode == r.Code)
            .Where((ru, r) => codeList.Contains(ru.UserCode) && !r.IsDeleted)
            .Select((ru, r) => new { ru.UserCode, r.RoleName })
            .ToListAsync();

        var result = new Dictionary<string, string>();
        foreach (var item in dict)
        {
            if (!string.IsNullOrEmpty(item.UserCode) && !string.IsNullOrEmpty(item.RoleName))
                result[item.UserCode] = item.RoleName;
        }

        return Result<Dictionary<string, string>>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<Result<string?>> GetRoleCodeByUserCodeAsync(string userCode)
    {
        var roleCode = await _db.Client.Queryable<Sys_RoleUser, Sys_Role>(
                (ru, r) => ru.RoleCode == r.Code)
            .Where((ru, r) => ru.UserCode == userCode && !r.IsDeleted)
            .Select((ru, r) => r.Code)
            .FirstAsync();

        return Result<string?>.Ok(roleCode);
    }
}
