using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Models;
using AbstractionsActionDescriptor = Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor;

namespace YZH.Core.Api.Filters
{
    /// <summary>
    /// 权限校验过滤器
    ///
    /// 功能：
    /// 1. 接口级权限校验（RequirePermission 特性）
    /// 2. 数据权限过滤（RequireDataScope 特性）
    /// 3. 操作日志记录
    /// </summary>
    public class PermissionFilter : IAuthorizationFilter, IActionFilter
    {
        private readonly IUserContext _userContext;
        private readonly ILogger<PermissionFilter> _logger;

        public PermissionFilter(IUserContext userContext, ILogger<PermissionFilter> logger)
        {
            _userContext = userContext;
            _logger = logger;
        }

        /// <summary>
        /// 授权校验（在 Action 执行前调用）
        /// </summary>
        public void OnAuthorization(AuthorizationFilterContext context)
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
                var hasPermission = CheckPermission(_userContext, requirePermission.PermissionCode);
                if (!hasPermission)
                {
                    _logger.LogWarning(
                        "用户 {UserCode} 无权限访问接口 {Action}",
                        _userContext.UserCode,
                        context.ActionDescriptor.DisplayName
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
        /// Action 执行前
        /// </summary>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // 记录操作日志
            _logger.LogInformation(
                "[权限校验] 用户: {UserCode}, 接口: {Action}, IP: {Ip}",
                _userContext.UserCode,
                context.ActionDescriptor.DisplayName,
                _userContext.ClientIp
            );
        }

        /// <summary>
        /// Action 执行后
        /// </summary>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            // 记录执行结果
            _logger.LogInformation(
                "[权限校验] 用户: {UserCode}, 接口: {Action}, 状态: {Status}",
                _userContext.UserCode,
                context.ActionDescriptor.DisplayName,
                context.HttpContext.Response.StatusCode
            );
        }

        /// <summary>
        /// 检查用户权限
        /// </summary>
        private bool CheckPermission(IUserContext userContext, string permissionCode)
        {
            // TODO: 实现权限检查逻辑
            // 1. 从缓存或数据库获取用户权限列表
            // 2. 检查是否包含所需权限

            // 临时实现：所有已认证用户都有权限
            return true;
        }

        /// <summary>
        /// 获取数据权限范围
        /// </summary>
        private DataScope GetDataScope(IUserContext userContext, DataScopeType scopeType)
        {
            var scope = new DataScope
            {
                UserCode = userContext.UserCode,
                RoleId = 0, // TODO: 从 userContext 获取角色 ID
                Type = scopeType
            };

            // TODO: 根据用户角色和部门查询数据权限

            return scope;
        }
    }

    /// <summary>
    /// 权限校验特性
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
    /// 数据权限校验特性
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
    /// 数据权限类型
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
    /// 数据权限对象
    /// </summary>
    public class DataScope
    {
        public string UserCode { get; set; }
        public int RoleId { get; set; }
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
