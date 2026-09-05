using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using YZH.Core.DBManager;
using YZH.Entity;
using YZH.Entity.Admin.Platform;

namespace YZH.Core.Validation;

/// <summary>
/// 实体校验处理器。只做校验（SELECT COUNT），不做数据持久化。
///
/// 使用方式：
///   var handler = new EntityValidationHandler&lt;CertificationBody&gt;();
///   var result = handler.Validate(entity, ValidationAction.Add);
///   if (!result.IsValid) return result.ToWebResponse();
/// </summary>
public class EntityValidationHandler<TEntity> where TEntity : class
{
    #region 核心入口：只校验，不保存

    public virtual EntityValidationResult Validate(
        TEntity entity,
        ValidationAction action,
        string? excludeCode = null)
    {
        return action switch
        {
            ValidationAction.Add => OnAdding(entity),
            ValidationAction.Update => OnUpdating(entity, excludeCode),
            ValidationAction.Delete => OnDeleting(entity),
            _ => new EntityValidationResult().OK()
        };
    }

    #endregion

    #region 虚方法：校验扩展点

    protected virtual EntityValidationResult OnAdding(TEntity entity)
    {
        var result = ValidateRequiredFields(entity);
        if (!result.IsValid) return result;
        return ValidateUniqueFields(entity, isAdd: true);
    }

    protected virtual EntityValidationResult OnUpdating(TEntity entity, string? excludeCode)
    {
        var result = ValidateRequiredFields(entity);
        if (!result.IsValid) return result;
        return ValidateUniqueFields(entity, isAdd: false, excludeCode);
    }

    protected virtual EntityValidationResult OnDeleting(TEntity entity)
        => new EntityValidationResult().OK();

    #endregion

    #region 默认实现：基于 [Required] / [StringLength] 的必填/长度校验

    /// <summary>
    /// 自动校验 [Required] 和 [StringLength] 特性。
    /// 在唯一性校验之前执行。
    /// </summary>
    protected virtual EntityValidationResult ValidateRequiredFields(TEntity entity)
    {
        var result = new EntityValidationResult();
        var props = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            if (prop.GetCustomAttribute<NotMappedAttribute>() != null) continue;
            if (prop.GetCustomAttribute<KeyAttribute>() != null) continue;
            if (prop.GetCustomAttribute<DatabaseGeneratedAttribute>() != null) continue;

            var value = prop.GetValue(entity);
            string description = GetFieldDescription(prop);

            // [Required] 非空校验
            var requiredAttr = prop.GetCustomAttribute<RequiredAttribute>();
            if (requiredAttr != null)
            {
                if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                    return result.Error($"{description}不能为空");
            }

            // [StringLength(n)] 长度校验
            var lengthAttr = prop.GetCustomAttribute<StringLengthAttribute>();
            if (lengthAttr != null && value is string strVal)
            {
                if (lengthAttr.MaximumLength > 0 && strVal.Length > lengthAttr.MaximumLength)
                    return result.Error($"{description}不能超过{lengthAttr.MaximumLength}个字符");
            }
        }
        return result.OK();
    }

    protected string GetFieldDescription(PropertyInfo prop)
    {
        var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
        if (displayAttr?.Name != null) return displayAttr.Name;
        var uniqueAttr = prop.GetCustomAttribute<UniqueFieldAttribute>();
        if (uniqueAttr?.Description != null) return uniqueAttr.Description;
        return prop.Name;
    }

    #endregion

    #region 默认实现：基于 [UniqueField] + SQL 的唯一性校验

    /// <summary>
    /// 通用唯一性校验：反射扫描 [UniqueField] 特性，拼接 SELECT COUNT SQL。
    ///
    /// 新增：SELECT COUNT(1) FROM table WHERE col = @val
    /// 修改：SELECT COUNT(1) FROM table WHERE col = @val AND code &lt;&gt; @excludeCode
    /// 联合：SELECT COUNT(1) FROM table WHERE col1 = @val1 AND col2 = @val2 AND code &lt;&gt; @excludeCode
    /// </summary>
    protected virtual EntityValidationResult ValidateUniqueFields(
        TEntity entity, bool isAdd, string? excludeCode = null)
    {
        var result = new EntityValidationResult();
        var entityType = typeof(TEntity);
        var tableName = GetTableName();
        var codeColumn = GetCodeColumnName();

        var uniqueProps = entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<UniqueFieldAttribute>() != null)
            .ToList();

        if (uniqueProps.Count == 0)
            return result.OK();

        foreach (var prop in uniqueProps)
        {
            var attr = prop.GetCustomAttribute<UniqueFieldAttribute>()!;
            var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            var value = prop.GetValue(entity);

            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                continue;

            var conditions = new List<string> { $"{colName} = @{colName}" };
            var parameters = new Dictionary<string, object> { [colName] = value };

            // 联合唯一
            if (attr.WithFields != null && attr.WithFields.Length > 0)
            {
                foreach (var withFieldName in attr.WithFields)
                {
                    var withProp = entityType.GetProperty(withFieldName);
                    if (withProp == null) continue;
                    var withColName = withProp.GetCustomAttribute<ColumnAttribute>()?.Name ?? withFieldName;
                    var withValue = withProp.GetValue(entity);
                    if (withValue == null) continue;
                    conditions.Add($"{withColName} = @{withColName}");
                    parameters[withColName] = withValue;
                }
            }

            // 修改/删除：排除自身（用 Code）
            if (!isAdd && !string.IsNullOrEmpty(excludeCode))
            {
                conditions.Add($"{codeColumn} <> @{codeColumn}_exclude");
                parameters[$"{codeColumn}_exclude"] = excludeCode;
            }

            var sql = $"SELECT COUNT(1) FROM {tableName} WHERE {string.Join(" AND ", conditions)}";
            var count = Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, parameters));

            if (count > 0)
            {
                string valueDisplay = value.ToString()!;
                if (attr.WithFields != null && attr.WithFields.Length > 0)
                {
                    var withValues = new List<string>();
                    foreach (var wf in attr.WithFields)
                    {
                        var wp = entityType.GetProperty(wf);
                        var wv = wp?.GetValue(entity);
                        if (wv != null) withValues.Add(wv.ToString()!);
                    }
                    if (withValues.Count > 0)
                        valueDisplay = $"{valueDisplay}（{string.Join(" / ", withValues)}）";
                }

                string description = attr.Description ?? prop.Name;
                return result.Error($"{description}「{valueDisplay}」已存在，请使用其他值");
            }
        }

        return result.OK();
    }

    #endregion

    #region 辅助方法

    protected string GetTableName()
    {
        var entityAttr = typeof(TEntity).GetCustomAttribute<EntityAttribute>();
        return entityAttr?.TableName ?? typeof(TEntity).Name;
    }

    protected string GetCodeColumnName() => "code";

    protected string? GetCodeValue(TEntity entity)
        => typeof(TEntity).GetProperty("Code")?.GetValue(entity)?.ToString();

    /// <summary>获取关联表记录数（用于删除校验）</summary>
    protected int GetRelatedCount(string relatedTable, string foreignKeyColumn, object foreignKeyValue)
    {
        var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                  $"WHERE {foreignKeyColumn} = @val AND (IsDeleted = 0 OR IsDeleted IS NULL)";
        return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, new { val = foreignKeyValue }));
    }

    /// <summary>获取关联表记录数（多条件版本）</summary>
    protected int GetRelatedCount(string relatedTable, Dictionary<string, object> conditions)
    {
        var whereClauses = conditions.Select(kv => $"{kv.Key} = @{kv.Key}").ToList();
        var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                  $"WHERE {string.Join(" AND ", whereClauses)} AND (IsDeleted = 0 OR IsDeleted IS NULL)";
        return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, conditions));
    }

    #endregion
}
