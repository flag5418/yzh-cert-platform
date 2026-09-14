using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Services;

/// <summary>
///     接口级权限服务
///     为 PermissionFilter 提供权限判定能力
/// </summary>
public interface IPermissionService
{
    /// <summary>
    ///     检查用户是否拥有指定权限码
    /// </summary>
    /// <param name="userContext">当前用户上下文</param>
    /// <param name="permissionCode">权限码（由 RequirePermissionAttribute 传入）</param>
    /// <returns>true = 有权限；false = 无权限</returns>
    Task<bool> HasPermissionAsync(IUserContext userContext, string permissionCode);
}
