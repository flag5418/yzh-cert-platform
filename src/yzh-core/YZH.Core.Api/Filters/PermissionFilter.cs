using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Interfaces;
using AbstractionsActionDescriptor = Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor;

namespace YZH.Core.Api.Filters
{
    /// <summary>
    ///     权限校验过滤器（异步版）
    ///
    ///     功能：
    ///     1. 接口级权限校验（RequirePermission 特性）
    ///     2. 数据权限过滤（RequireDataScope 特性）
    ///     3. 操作日志记录
    ///
    ///     安全策略：
    ///     - 超级管理员（配置文件 YZH:SuperAdmin）→ 全部放行
    ///     - 超级管理员角色（ROLE_SUPER_ADMIN）→ 全部放行
    ///     - 其他用户 → 按 IPermissionService 校验
    ///     - 未标记 RequirePermission 的接口 → 仅要求已认证
    /// </summary>
    public class PermissionFilter : IAsyncAuthorizationFilter, IActionFilter
    {
        private readonly IUserContext _userContext;
        private readonly ILogger<PermissionFilter> _logger;
        private readonly IPermissionService _permissionService;
        private readonly string _superAdminUserName;

        public PermissionFilter(
            IUserContext userContext,
            ILogger<PermissionFilter> logger,
            IPermissionService permissionService,
            IConfiguration configuration)
        {
            _userContext = userContext;
            _logger = logger;
            _permissionService = permissionService;
            _superAdminUserName = configuration["YZH:SuperAdmin"] ?? "admin";
        }

        /// <summary>
        ///     授权校验（异步，在 Action 执行前调用）
        /// </summary>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // 1. 检查是否跳过授权
            var actionDescriptor = context.ActionDescriptor;
            var methodInfo = (actionDescriptor as ControllerActionDescriptor)?.MethodInfo;
            if (methodInfo != null && methodInfo.IsDefined(typeof(AllowAnonymousAttribute), true))
                return;

            // 2. 检查用户是否已登录
            if (!_userContext.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // 3. 检查接口权限
            var requirePermission = context.ActionDescriptor
                .GetMethodAttribute<RequirePermissionAttribute>();

            if (requirePermission != null)
            {
                var hasPermission = await CheckPermissionAsync(_userContext, requirePermission.PermissionCode);
                if (!hasPermission)
                {
                    _logger.LogWarning(
                        "用户 {UserCode}({UserName}) 无权限访问接口 {Action}，所需权限：{PermissionCode}",
                        _userContext.UserCode,
                        _userContext.UserName,
                        context.ActionDescriptor.DisplayName,
                        requirePermission.PermissionCode
                    );
                    context.Result = new ForbidResult();
                    return;
                }
            }

            // 4. 添加数据权限到 HttpContext
            var dataScope = context.ActionDescriptor
                .GetMethodAttribute<RequireDataScopeAttribute>();

            if (dataScope != null)
            {
                context.HttpContext.Items["DataScope"] = GetDataScope(_userContext, dataScope.ScopeType);
            }
        }

        /// <summary>
        ///     Action 执行前（记录操作日志）
        /// </summary>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            _logger.LogInformation(
                "[权限校验] 用户: {UserCode}({UserName}), 接口: {Action}, IP: {Ip}",
                _userContext.UserCode,
                _userContext.UserName,
                context.ActionDescriptor.DisplayName,
                _userContext.ClientIp
            );
        }

        /// <summary>
        ///     Action 执行后（记录执行结果）
        /// </summary>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            _logger.LogInformation(
                "[权限校验] 用户: {UserCode}({UserName}), 接口: {Action}, 状态: {Status}",
                _userContext.UserCode,
                _userContext.UserName,
                context.ActionDescriptor.DisplayName,
                context.HttpContext.Response.StatusCode
            );
        }

        /// <summary>
        ///     检查用户权限（分层策略）
        /// </summary>
        private async Task<bool> CheckPermissionAsync(IUserContext userContext, string permissionCode)
        {
            // 1. 配置文件超级管理员白名单
            if (string.Equals(userContext.UserName, _superAdminUserName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "用户 {UserName} 是配置文件指定的超级管理员（YZH:SuperAdmin={SuperAdmin}），直接放行",
                    userContext.UserName, _superAdminUserName);
                return true;
            }

            // 2. 超级管理员角色白名单
            if (MenuPermissionService.IsSuperAdmin(userContext))
            {
                _logger.LogDebug("用户 {UserName} 是超级管理员角色，直接放行", userContext.UserName);
                return true;
            }

            // 3. 接口级权限校验
            return await _permissionService.HasPermissionAsync(userContext, permissionCode);
        }

        /// <summary>
        ///     获取数据权限范围
        /// </summary>
        private DataScope GetDataScope(IUserContext userContext, DataScopeType scopeType)
        {
            var scope = new DataScope
            {
                UserCode = userContext.UserCode,
                RoleCode = userContext.RoleCode,
                Type = scopeType
            };

            // TODO: 根据用户角色和部门查询数据权限
            // 框架提供注入点，具体实现由各项目自定义

            return scope;
        }
    }

    /// <summary>
    ///     权限校验特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequirePermissionAttribute : Attribute
    {
        public string PermissionCode { get; }

        public RequirePermissionAttribute(string permissionCode)
        {
            PermissionCode = permissionCode;
        }
    }

    /// <summary>
    ///     数据权限校验特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequireDataScopeAttribute : Attribute
    {
        public DataScopeType ScopeType { get; }

        public RequireDataScopeAttribute(DataScopeType scopeType)
        {
            ScopeType = scopeType;
        }
    }

    /// <summary>
    ///     数据权限类型
    /// </summary>
    public enum DataScopeType
    {
        /// <summary>全部数据</summary>
        All,

        /// <summary>本部门数据</summary>
        Department,

        /// <summary>本部门及下级部门</summary>
        DepartmentAndSub,

        /// <summary>仅本人数据</summary>
        Self,

        /// <summary>自定义</summary>
        Custom
    }

    /// <summary>
    ///     数据权限对象
    /// </summary>
    public class DataScope
    {
        public string UserCode { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public DataScopeType Type { get; set; }
        public List<string> DeptCodes { get; set; } = new();
        public List<string> UserCodes { get; set; } = new();
    }

    /// <summary>
    ///     ActionDescriptor 扩展方法
    /// </summary>
    public static class ActionDescriptorExtensions
    {
        /// <summary>
        ///     获取方法上的特性
        /// </summary>
        public static T? GetMethodAttribute<T>(this AbstractionsActionDescriptor actionDescriptor) where T : Attribute
        {
            if (actionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                return controllerActionDescriptor.MethodInfo.GetCustomAttribute<T>();
            }
            return null;
        }
    }
}
