using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VOL.Core.BaseProvider;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Core.Utilities;
using VOL.Entity.SystemModels;
using VOL.Entity.CertPlatform;

namespace VOL.CERT.Extensions
{
    /// <summary>
    /// 通用唯一性校验扩展方法。
    /// 
    /// 设计原则：
    /// - 声明式：实体属性上标记 [UniqueField]，Service 中一行调用即可
    /// - 反射驱动：运行时扫描实体上的 [UniqueField] 特性，自动构建查询
    /// - 通用性：适用于所有继承 YZHBaseEntity 的实体，无需逐个 Service 手写
    /// 
    /// 工作流程：
    /// 1. 反射扫描 TEntity 上所有带 [UniqueField] 的属性
    /// 2. 对每个属性，取其当前值
    /// 3. 用 Vol 的 CreateExpression 构建动态 Lambda 查询
    /// 4. 如有 WithFields，追加联合唯一条件
    /// 5. Update 场景排除当前记录（Id != currentId）
    /// 6. 查询数据库，存在则返回友好错误
    /// 
    /// 使用示例（Service 中）：
    /// <code>
    /// // Add 场景
    /// var result = repository.ValidateUniqueFields(entity, isAdd: true);
    /// if (!result.Status) return result;
    /// 
    /// // Update 场景（排除自身）
    /// var result = repository.ValidateUniqueFields(entity, isAdd: false, excludeId: currentId);
    /// if (!result.Status) return result;
    /// </code>
    /// </summary>
    public static class UniqueValidationExtensions
    {
        /// <summary>
        /// 校验实体中所有标记了 [UniqueField] 的属性的唯一性。
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="repository">Vol 框架的 IRepository</param>
        /// <param name="entity">待校验的实体实例</param>
        /// <param name="isAdd">true=新增场景，false=更新场景</param>
        /// <param name="excludeId">更新时排除自身的 Id（仅 isAdd=false 时有效）</param>
        /// <returns>校验通过返回 OK，失败返回 Error（含友好消息）</returns>
        public static WebResponseContent ValidateUniqueFields<TEntity>(
            this IRepository<TEntity> repository,
            TEntity entity,
            bool isAdd,
            long? excludeId = null)
            where TEntity : BaseEntity
        {
            var response = new WebResponseContent();

            // 扫描实体上所有带 [UniqueField] 特性的属性
            var uniqueProps = typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<UniqueFieldAttribute>() != null)
                .ToList();

            if (uniqueProps.Count == 0)
                return response.OK();

            foreach (var prop in uniqueProps)
            {
                var attr = prop.GetCustomAttribute<UniqueFieldAttribute>();
                string fieldName = prop.Name;
                object fieldValue = prop.GetValue(entity);

                // 空值跳过（唯一性校验只针对有值的字段）
                if (fieldValue == null || (fieldValue is string s && string.IsNullOrWhiteSpace(s)))
                    continue;

                // 获取字段中文名：优先用 UniqueFieldAttribute.Description，其次用 [Display(Name=...)]
                string description = attr.Description;
                if (string.IsNullOrEmpty(description))
                {
                    var displayAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
                    description = displayAttr?.Name ?? fieldName;
                }

                // 构建查询条件：FieldName == fieldValue
                Expression<Func<TEntity, bool>> condition = fieldName
                    .CreateExpression<TEntity>(fieldValue.ToString(), LinqExpressionType.Equal);

                // 联合唯一：追加 WithFields 条件
                if (attr.WithFields != null && attr.WithFields.Length > 0)
                {
                    foreach (string withFieldName in attr.WithFields)
                    {
                        var withProp = typeof(TEntity).GetProperty(withFieldName);
                        if (withProp == null) continue;

                        object withValue = withProp.GetValue(entity);
                        if (withValue == null) continue;

                        var withCondition = withFieldName
                            .CreateExpression<TEntity>(withValue.ToString(), LinqExpressionType.Equal);

                        // 合并条件：condition = condition AND withCondition
                        var paramExpr = condition.Parameters[0];
                        var combinedBody = Expression.AndAlso(condition.Body, withCondition.Body);
                        condition = Expression.Lambda<Func<TEntity, bool>>(combinedBody, paramExpr);
                    }
                }

                // Update 场景：排除自身记录 Id != excludeId
                if (!isAdd && excludeId.HasValue)
                {
                    var keyProp = typeof(TEntity).GetKeyProperty();
                    if (keyProp != null)
                    {
                        var excludeCondition = keyProp.Name
                            .CreateExpression<TEntity>(excludeId.Value.ToString(), LinqExpressionType.NotEqual);

                        var paramExpr = condition.Parameters[0];
                        var combinedBody = Expression.AndAlso(condition.Body, excludeCondition.Body);
                        condition = Expression.Lambda<Func<TEntity, bool>>(combinedBody, paramExpr);
                    }
                }

                // 执行查询
                bool exists = repository.FindAsIQueryable(condition).Any();
                if (exists)
                {
                    // 构建联合字段的提示信息
                    string valueDisplay = fieldValue.ToString();
                    if (attr.WithFields != null && attr.WithFields.Length > 0)
                    {
                        // 联合唯一时，显示所有参与字段的值
                        var withValues = new List<string>();
                        foreach (string wf in attr.WithFields)
                        {
                            var wp = typeof(TEntity).GetProperty(wf);
                            if (wp != null)
                            {
                                var wv = wp.GetValue(entity);
                                if (wv != null) withValues.Add(wv.ToString());
                            }
                        }
                        if (withValues.Count > 0)
                            valueDisplay = $"{valueDisplay}（{string.Join(" / ", withValues)}）";
                    }

                    return response.Error($"{description}「{valueDisplay}」已存在，请使用其他值");
                }
            }

            return response.OK();
        }

        /// <summary>
        /// 便捷重载：从 SaveModel.MainData（Dictionary）中提取字段值进行唯一性校验。
        /// 
        /// 用于在 Service.Add/Update override 中调用 base.Add/Update 之前的前置校验，
        /// 此时实体还未被 Vol 框架转换，只有原始的 Dictionary 数据。
        /// </summary>
        public static WebResponseContent ValidateUniqueFieldsFromDict<TEntity>(
            this IRepository<TEntity> repository,
            Dictionary<string, object> mainData,
            bool isAdd,
            long? excludeId = null)
            where TEntity : BaseEntity
        {
            var response = new WebResponseContent();

            var uniqueProps = typeof(TEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<UniqueFieldAttribute>() != null)
                .ToList();

            if (uniqueProps.Count == 0)
                return response.OK();

            foreach (var prop in uniqueProps)
            {
                var attr = prop.GetCustomAttribute<UniqueFieldAttribute>();
                string fieldName = prop.Name;

                // 从 Dictionary 中取值（键名是属性名 PascalCase）
                if (!mainData.ContainsKey(fieldName)) continue;
                object fieldValue = mainData[fieldName];
                if (fieldValue == null || (fieldValue is string s && string.IsNullOrWhiteSpace(s)))
                    continue;

                // 获取字段中文名
                string description = attr.Description;
                if (string.IsNullOrEmpty(description))
                {
                    var displayAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
                    description = displayAttr?.Name ?? fieldName;
                }

                // 构建查询条件
                Expression<Func<TEntity, bool>> condition = fieldName
                    .CreateExpression<TEntity>(fieldValue.ToString(), LinqExpressionType.Equal);

                // 联合唯一
                if (attr.WithFields != null && attr.WithFields.Length > 0)
                {
                    foreach (string withFieldName in attr.WithFields)
                    {
                        if (!mainData.ContainsKey(withFieldName)) continue;
                        object withValue = mainData[withFieldName];
                        if (withValue == null) continue;

                        var withCondition = withFieldName
                            .CreateExpression<TEntity>(withValue.ToString(), LinqExpressionType.Equal);

                        var paramExpr = condition.Parameters[0];
                        var combinedBody = Expression.AndAlso(condition.Body, withCondition.Body);
                        condition = Expression.Lambda<Func<TEntity, bool>>(combinedBody, paramExpr);
                    }
                }

                // Update 排除自身
                if (!isAdd && excludeId.HasValue)
                {
                    var keyProp = typeof(TEntity).GetKeyProperty();
                    if (keyProp != null)
                    {
                        var excludeCondition = keyProp.Name
                            .CreateExpression<TEntity>(excludeId.Value.ToString(), LinqExpressionType.NotEqual);

                        var paramExpr = condition.Parameters[0];
                        var combinedBody = Expression.AndAlso(condition.Body, excludeCondition.Body);
                        condition = Expression.Lambda<Func<TEntity, bool>>(combinedBody, paramExpr);
                    }
                }

                bool exists = repository.FindAsIQueryable(condition).Any();
                if (exists)
                {
                    string valueDisplay = fieldValue.ToString();
                    if (attr.WithFields != null && attr.WithFields.Length > 0)
                    {
                        var withValues = new List<string>();
                        foreach (string wf in attr.WithFields)
                        {
                            if (mainData.ContainsKey(wf) && mainData[wf] != null)
                                withValues.Add(mainData[wf].ToString());
                        }
                        if (withValues.Count > 0)
                            valueDisplay = $"{valueDisplay}（{string.Join(" / ", withValues)}）";
                    }

                    return response.Error($"{description}「{valueDisplay}」已存在，请使用其他值");
                }
            }

            return response.OK();
        }
    }
}
