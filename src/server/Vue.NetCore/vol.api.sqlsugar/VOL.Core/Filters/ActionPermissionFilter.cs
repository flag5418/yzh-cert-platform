using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Threading.Tasks;
using VOL.Core.Configuration;
using VOL.Core.Enums;
using VOL.Core.Filters;
using VOL.Core.ManageUser;
using VOL.Core.Services;
using VOL.Core.UserManager;
using VOL.Core.Utilities;
using VOL.Entity.AttributeManager;

namespace VOL.Core.Filters
{
    /// <summary>
    /// 1、控制器或controller设置了AllowAnonymousAttribute直接返回
    /// 2、TableName、TableActions 同时为null或空，SysController为false的，只判断是否登陆
    /// 3、TableName、TableActions 都不null根据表名与action判断是否有权限
    /// 4、SysController为true，通过httpcontext获取表名与action判断是否有权限
    /// 5、Roles对指定角色验证
    /// </summary>
    public class ActionPermissionFilter : IAsyncActionFilter
    {
        private WebResponseContent ResponseContent { get; set; }
        private ActionPermissionRequirement ActionPermission;
        private UserContext _userContext { get; set; }
        // private ResponseType responseType;
        public ActionPermissionFilter(ActionPermissionRequirement actionPermissionRequirement, UserContext userContext)
        {
            this.ResponseContent = new WebResponseContent();
            this.ActionPermission = actionPermissionRequirement;
            _userContext = userContext;
        }
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (OnActionExecutionPermission(context).Status)
            {
                await next();
                return;
            }
            FilterResponse.SetActionResult(context, ResponseContent);
        }
        private WebResponseContent OnActionExecutionPermission(ActionExecutingContext context)
        {
            //!context.Filters.Any(item => item is IFixedTokenFilter))固定token的是否验证权限
            //if ((context.Filters.Any(item => item is IAllowAnonymousFilter)
            //    && !context.Filters.Any(item => item is IFixedTokenFilter))
            //    || UserContext.Current.IsSuperAdmin
            //    )
            if (context.Filters.Any(item => item is IAllowAnonymousFilter)
                || UserContext.Current.IsSuperAdmin)
                return ResponseContent.OK();

            //演示环境除了admin帐号，其他帐号都不能增删改等操作
            if (!_userContext.IsSuperAdmin && AppSetting.GlobalFilter.Enable
                && AppSetting.GlobalFilter.Actions.Any(x => x.ToLower() == ((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor).ActionName.ToLower()))
            {
                return ResponseContent.Error(AppSetting.GlobalFilter.Message);
            }

            //如果没有指定表的权限，则默认为代码生成的控制器，优先获取PermissionTableAttribute指定的表，如果没有数据则使用当前控制器的名作为表名权限
            if (ActionPermission.SysController)
            {
                object[] permissionArray = ((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor)?.ControllerTypeInfo.GetCustomAttributes(typeof(PermissionTableAttribute), false);
                if (permissionArray == null || permissionArray.Length == 0)
                {
                    ActionPermission.TableName = ((Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)context.ActionDescriptor).ControllerName;
                }
                else
                {
                    ActionPermission.TableName = (permissionArray[0] as PermissionTableAttribute).Name;
                }
                if (string.IsNullOrEmpty(ActionPermission.TableName))
                {
                    //responseType = ResponseType.ParametersLack;
                    return ResponseContent.Error(ResponseType.ParametersLack);
                }
            }

            //如果没有给定权限，不需要判断
            if (string.IsNullOrEmpty(ActionPermission.TableName)
                && (ActionPermission.TableActions == null || ActionPermission.TableActions.Length == 0)
                && (ActionPermission.RoleIds == null || ActionPermission.RoleIds.Length == 0))
            {
                return ResponseContent.OK();
            }

            //是否限制的角色ID称才能访问
            //权限判断角色ID,
            if (ActionPermission.RoleIds != null && ActionPermission.RoleIds.Length > 0)
            {
                if (ActionPermission.RoleIds.Contains(_userContext.UserInfo.Role_Id)) return ResponseContent.OK();
                //如果角色ID没有权限。并且也没控制器权限
                if (ActionPermission.TableActions == null || ActionPermission.TableActions.Length == 0)
                {
                    return ResponseContent.Error(ResponseType.NoRolePermissions);
                }
            }
            //2020.05.05移除x.TableName.ToLower()转换,获取权限时已经转换成为小写
            // 使用 TableActions 数组进行权限判断
            string[] actionsToCheck = ActionPermission.TableActions ?? [];

            bool actionAuth = false;
            if (actionsToCheck.Length > 0)
            {
                //var permissions = _userContext.GetPermissions(x => x.TableName == ActionPermission.TableName.ToLower());
                //if (permissions?.UserAuthArr != null)
                //{
                //    // 检查用户权限数组中是否包含任意一个需要的权限
                //    actionAuth = actionsToCheck.Any(action => permissions.UserAuthArr.Contains(action));
                //}
                actionAuth = CheckPermission(actionsToCheck, ActionPermission.TableName);
            }

            if (!actionAuth)
            {
                if (UserContext.MenuType == 1 && actionsToCheck.Length > 0)
                {
                    actionAuth = _userContext.Permissions.Where(x => x.TableName == ActionPermission.TableName.ToLower())
                        .Any(c => c.UserAuthArr != null && actionsToCheck.Any(action => c.UserAuthArr.Contains(action)));
                }
                //明细表没权限的走主表权限
                if (!actionAuth)
                {
                    try
                    {
                        string table = TableColumnContext.TableInfo.Where(x => x.DetailName != null && x.DetailName.ToLower().Split(",").Contains(ActionPermission.TableName.ToLower()))
                        .Select(s => s.TableName)
                        .FirstOrDefault();
                        actionAuth = CheckPermission(actionsToCheck, table);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"获取权限异常:{ex.Message + ex?.InnerException}");
                    }
                }

                if (!actionAuth)
                {
                    string actionsStr = actionsToCheck.Length > 0 ? string.Join(",", actionsToCheck) : "无权限";
                    Logger.Info(LoggerType.Authorzie, $"没有权限操作," +
                   $"用户ID{_userContext.UserId}:{_userContext.UserTrueName}," +
                   $"操作权限{ActionPermission.TableName}:{actionsStr}");
                    return ResponseContent.Error(ResponseType.NoPermissions);
                }
            }
            return ResponseContent.OK();
        }

        private bool CheckPermission(string[] actionsToCheck, string table)
        {
            if (table == null)
            {
                return false;
            }
            var permissions = _userContext.GetPermissions(x => x.TableName == table.ToLower());
            if (permissions?.UserAuthArr != null)
            {
                // 检查用户权限数组中是否包含任意一个需要的权限
                return actionsToCheck.Any(action => permissions.UserAuthArr.Contains(action));
            }
            return false;
        }
    }
}
