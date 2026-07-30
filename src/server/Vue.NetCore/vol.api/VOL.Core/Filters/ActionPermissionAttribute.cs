using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using VOL.Core.Enums;
using VOL.Core.Extensions;

namespace VOL.Core.Filters
{
    public class ActionPermissionAttribute : TypeFilterAttribute
    {
        public ActionPermissionAttribute(bool isApi = false)
            : base(typeof(ActionPermissionFilter))
        {
            Arguments = new object[] { new ActionPermissionRequirement() { IsApi = isApi } };
        }
        /// <summary>
        /// 限定角色访问
        /// </summary>
        /// <param name="roles"></param>
        public ActionPermissionAttribute(int roleId, bool isApi = false)
       : base(typeof(ActionPermissionFilter))
        {
            Arguments = new object[] { new ActionPermissionRequirement() { RoleIds = new int[] { roleId }, IsApi = isApi } };
        }
        public ActionPermissionAttribute(ActionRolePermission actionRolePermission, bool isApi = false)
        : base(typeof(ActionPermissionFilter))
        {
            Array array = Enum.GetValues(typeof(ActionRolePermission));
            List<int> roles = new List<int>();
            foreach (ActionRolePermission item in array)
            {
                if (actionRolePermission.HasFlag(item))
                {
                    roles.Add((int)item);
                }
            }
            Arguments = new object[] { new ActionPermissionRequirement() { RoleIds = roles.ToArray(), IsApi = isApi } };
        }
        /// <summary>
        /// 限定角色访问
        /// </summary>
        /// <param name="roles"></param>
        public ActionPermissionAttribute(int[] roleIds, bool isApi = false)
       : base(typeof(ActionPermissionFilter))
        {
            Arguments = new object[] { new ActionPermissionRequirement() { RoleIds = roleIds, IsApi = isApi } };
        }

        public ActionPermissionAttribute(string tableName, ActionPermissionOptions tableAction, bool sysController = false, bool isApi = false)
            : base(typeof(ActionPermissionFilter))
        {
            this.SetActionPermissionRequirement(tableName, tableAction, sysController, isApi);
        }

        public ActionPermissionAttribute(string tableName, string roleIds, ActionPermissionOptions tableAction, bool sysController = false, bool isApi = false)
           : base(typeof(ActionPermissionFilter))
        {
            this.SetActionPermissionRequirement(tableName, tableAction, (roleIds ?? "").Split(",").Select(x => x.GetInt()).ToArray(), sysController, isApi);
        }

        public ActionPermissionAttribute(ActionPermissionOptions tableAction, bool isApi = false)
        : base(typeof(ActionPermissionFilter))
        {
            this.SetActionPermissionRequirement("", tableAction, true, isApi);
        }
        /// <summary>
        /// 解析组合枚举值为权限数组
        /// </summary>
        private string[] ParseActionPermissionOptions(ActionPermissionOptions tableAction)
        {
            List<string> actions = new List<string>();
            Array enumValues = Enum.GetValues(typeof(ActionPermissionOptions));

            foreach (ActionPermissionOptions item in enumValues)
            {
                // 跳过值为0的项（如果存在）
                int itemValue = (int)item;
                if (itemValue == 0) continue;

                // 使用 HasFlag 检查是否包含该权限
                // 对于位标志枚举，HasFlag 会检查是否设置了该位
                if (tableAction.HasFlag(item))
                {
                    actions.Add(item.ToString());
                }
            }

            return actions.ToArray();
        }

        private void SetActionPermissionRequirement(string tableName, ActionPermissionOptions tableAction,
            int[] roleId, bool sysController = false, bool isApi = false)
        {
            string[] tableActions = ParseActionPermissionOptions(tableAction);

            Arguments = new object[] { new ActionPermissionRequirement() {
                 SysController=sysController,
                 TableActions=tableActions, // 存储权限数组
                 TableName=tableName,
                 IsApi = isApi,
                 RoleIds=roleId
            } };
        }

        private void SetActionPermissionRequirement(string tableName, ActionPermissionOptions tableAction, bool sysController = false, bool isApi = false, int? roleId = null)
        {
            SetActionPermissionRequirement(tableName, tableAction, roleId == null ? null : new int[] { (int)roleId }, sysController, isApi);
        }
    }
}
