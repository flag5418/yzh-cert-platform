using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Api.Services;

/// <summary>
/// 角色查询服务（封装跨表 SQL 查询）
/// </summary>
public interface IRoleService
{
    /// <summary>根据用户编码列表批量查询角色名称</summary>
    Task<Result<Dictionary<string, string>>> GetRoleNamesByUserCodesAsync(IEnumerable<string> userCodes);

    /// <summary>根据用户编码查询角色编码（用于权限检查）</summary>
    Task<Result<string?>> GetRoleCodeByUserCodeAsync(string userCode);
}
