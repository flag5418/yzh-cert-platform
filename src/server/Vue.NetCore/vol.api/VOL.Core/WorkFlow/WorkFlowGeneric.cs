using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using VOL.Core.BaseProvider;
using VOL.Core.DBManager;
using VOL.Core.Extensions;
using VOL.Core.Infrastructure;
using VOL.Core.ManageUser;
using VOL.Entity.DomainModels;

namespace VOL.Core.WorkFlow
{
    public static class WorkFlowGeneric
    {

        /// <summary>
        /// 获取表当前审批流程数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static Sys_WorkFlowTable GetTableWorkflow<T>(this T entity, string table = null) where T : class
        {
            table = table ?? typeof(T).GetEntityTableName();
            string key = typeof(T).GetKeyProperty().GetValue(entity).ToString();
            var data = DBServerProvider.DbContext.Set<Sys_WorkFlowTable>().Include(x => x.Sys_WorkFlowTableStep)
                 .Where(x => x.WorkTable == table && x.WorkTableKey == key)
                 .FirstOrDefault();
            return data;
        }

        /// <summary>
        /// 获取表当前正在审批的节点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<Sys_WorkFlowTableStep> GetTableCurrentFlowStep<T>(this T entity, string table = null) where T : class
        {
            var data = entity.GetTableWorkflow(table);
            if (data == null)
            {
                return [];
            }
            var current = data.Sys_WorkFlowTableStep.Where(x => x.StepId == data.CurrentStepId && (x.AuditStatus == null || x.AuditStatus == 0)).ToList();
            return current;
        }
        /// <summary>
        /// 获取当前审批节点的用户id
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<int> GetTableCurrentStepAuditUserIds<T>(this T entity, string table = null) where T : class
        {
            var currentSteps = entity.GetTableCurrentFlowStep(table);
            if (currentSteps?.Count == 0)
            {
                return [];
            }
            List<int> userIds = new();
            foreach (var step in currentSteps)
            {
                var ids = GetAuditUserIds(step.StepType.GetInt(), step.StepValue);
                userIds.AddRange(ids);
            }
            return userIds.Distinct().ToList();
        }

        public static List<int> GetAuditUserIds(int stepType, string nextId = null)
        {
            List<int> userIds = [];
            if (stepType == 0 || string.IsNullOrEmpty(nextId))
            {
                return userIds;
            }
            if (stepType == (int)AuditType.角色审批)
            {
                int roleId = nextId.GetInt();
                userIds = DBServerProvider.DbContext.Set<Sys_User>().Where(s => s.Role_Id == roleId)
                    .Take(200).Select(s => s.User_Id).ToList();
            }
            else if (stepType == (int)AuditType.部门审批)
            {
                Guid departmentId = nextId.GetGuid() ?? Guid.Empty;
                userIds = DBServerProvider.DbContext.Set<Sys_UserDepartment>().Where(s => s.DepartmentId == departmentId && s.Enable == 1).Take(200).Select(s => s.UserId).ToList();
            }
            else
            {
                return nextId.Split(",").Select(c => c.GetInt()).ToList();
            }
            return userIds;
        }
        /// <summary>
        /// 获取表当前上一个审批节点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<Sys_WorkFlowTableStep> GetTablePreFlowStep<T>(this T entity, string table = null) where T : class
        {
            var data = entity.GetTableWorkflow(table);
            if (data == null)
            {
                return null;
            }
            var current = data.Sys_WorkFlowTableStep.Where(x => x.StepId == data.CurrentStepId).FirstOrDefault();
            if (current == null)
            {
                return null;
            }
            return data.GetPreStep<Sys_WorkFlowTable>(current);
        }

        /// <summary>
        /// 获取表当前下一个审批节点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static List<Sys_WorkFlowTableStep> GetTableNextFlowStep<T>(this T entity, string table = null) where T : class
        {
            var data = entity.GetTableWorkflow(table);
            if (data == null)
            {
                return null;
            }
            var current = data.Sys_WorkFlowTableStep.Where(x => x.StepId == data.CurrentStepId).FirstOrDefault();
            if (current == null)
            {
                return null;
            }
            return data.GetNextStep<Sys_WorkFlowTable>(current);
        }


        /// <summary>
        /// 获取下一个节点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflow"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public static List<Sys_WorkFlowTableStep> GetNextStep<T>(this Sys_WorkFlowTable workflow, Sys_WorkFlowTableStep current) where T : Sys_WorkFlowTable
        {
            var list = workflow.Sys_WorkFlowTableStep.Where(x => x.StepId == current.NextStepId).ToList();
            return list;
        }
        /// <summary>
        /// 获取上一个节点
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="workflow"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public static List<Sys_WorkFlowTableStep> GetPreStep<T>(this Sys_WorkFlowTable workflow, Sys_WorkFlowTableStep current) where T : Sys_WorkFlowTable
        {
            var list = workflow.Sys_WorkFlowTableStep.Where(x => x.NextStepId == current.StepId).ToList();
            return list;
        }
        public static string GetNextStepId<T>(this Sys_WorkFlowTable workflow, Sys_WorkFlowTableStep current) where T : Sys_WorkFlowTable
        {
            return workflow.GetNextStep<Sys_WorkFlowTable>(current).Select(x => x.StepId).FirstOrDefault();
        }

         public static List<string> AuditFields = new List<string>() { "auditid", "auditstatus", "auditor", "auditdate", "auditreason" };
        public static List<string> UpdateAuditInfo<T>(T entity, int auditStatus, string auditReason)
        {
            List<string> auditFields = new List<string>();
            var userInfo = UserContext.Current.UserInfo;
            var properties = typeof(T).GetProperties();
            PropertyInfo propertyInfo = typeof(T).GetAuditFieldPropertyInfo();
            foreach (var item in properties)
            {
                bool isAudit = true;
                propertyInfo?.SetValue(entity, auditStatus);
                switch (item.Name.ToLower())
                {
                    case "auditid":
                        item.SetValue(entity, userInfo.User_Id.ChangeType(item.PropertyType));
                        break;
                    case "auditstatus":
                        item.SetValue(entity, auditStatus);
                        break;
                    case "auditor":
                        item.SetValue(entity, userInfo.UserTrueName);
                        break;
                    case "auditdate":
                        item.SetValue(entity, DateTime.Now);
                        break;
                    case "auditreason":
                        item.SetValue(entity, auditReason);
                        break;
                    default:
                        isAudit = false;
                        break;
                }
                if (isAudit)
                {
                    auditFields.Add(item.Name);
                }
            }
            return auditFields;
        }
        /// <summary>
        /// 判断数据是否在审批中
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="auditProperty"></param>
        /// <returns></returns>
        public static bool CheckAudting<T>(List<T> list, PropertyInfo auditProperty)
        {
            foreach (var entity in list)
            {

                int value = auditProperty.GetValue(entity).GetInt();
                if (value != (int)AuditStatus.审核中 && value != (int)AuditStatus.待审核)
                {
                    return false;
                    // return Response.Error($"只能操作审批中的数据".Translator());
                }
            }
            return true;
        }


        /// <summary>
        /// 验证节节点是否满足当前用户
        /// </summary>
        /// <param name="stepType"></param>
        /// <param name="stepValue"></param>
        /// <returns></returns>
        public static bool CheckAuditUserValue(int? stepType, string stepValue = null)
        {
            switch (stepType)
            {
                case (int)AuditType.角色审批:
                    return UserContext.Current.RoleId==stepValue.GetInt();
                case (int)AuditType.部门审批:
                    return UserContext.Current.DeptIds.Contains((Guid)stepValue.GetGuid());
                default:
                    return UserContext.Current.UserId == stepValue.GetInt();
            }
        }
    }
}