using Microsoft.Extensions.Logging;
using YZH.Core.Api.Interfaces;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Services;

/// <summary>
///     接口级权限服务默认实现
///     
///     优先级：
///     1. 用户直接权限（sys_user_permission）
///     2. 角色权限（sys_role_api）
///     
///     容错：当 sys_api / sys_role_api / sys_user_permission 表尚未创建时，
///     捕获异常并默认拒绝（安全优先），同时记录警告日志。
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IApiRepository _apiRepository;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(IApiRepository apiRepository, ILogger<PermissionService> logger)
    {
        _apiRepository = apiRepository;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(IUserContext userContext, string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            _logger.LogWarning("权限码为空，默认拒绝访问");
            return false;
        }

        try
        {
            // 1. 查用户直接权限（用户级权限覆盖角色级权限）
            var userPermissions = await _apiRepository.GetApiCodesByUserCodeAsync(userContext.UserCode);
            if (userPermissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogDebug("用户 {UserCode} 通过用户权限拥有 {PermissionCode}", userContext.UserCode, permissionCode);
                return true;
            }

            // 2. 查角色权限
            var roleCodes = userContext.GetRoleCodes().ToList();
            if (roleCodes.Count == 0)
            {
                _logger.LogDebug("用户 {UserCode} 无角色，拒绝 {PermissionCode}", userContext.UserCode, permissionCode);
                return false;
            }

            foreach (var roleCode in roleCodes)
            {
                if (string.IsNullOrWhiteSpace(roleCode))
                    continue;

                var rolePermissions = await _apiRepository.GetApiCodesByRoleCodeAsync(roleCode);
                if (rolePermissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "用户 {UserCode} 通过角色 {RoleCode} 拥有 {PermissionCode}",
                        userContext.UserCode, roleCode, permissionCode);
                    return true;
                }
            }

            _logger.LogInformation(
                "用户 {UserCode} 无权限访问 {PermissionCode}",
                userContext.UserCode, permissionCode);
            return false;
        }
        catch (Exception ex)
        {
            // 安全优先：表不存在或查询异常时默认拒绝
            _logger.LogWarning(
                ex,
                "权限查询异常（可能是 sys_api / sys_role_api / sys_user_permission 表尚未创建），" +
                "用户 {UserCode} 访问 {PermissionCode} 被拒绝",
                userContext.UserCode, permissionCode);
            return false;
        }
    }
}
