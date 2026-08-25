# EntityValidationHandler + YZHServiceBase 设计文档

> **版本**: V4.0  
> **位置**: `YZH-Framework/YZH.Core/Validation/` + `YZH-Framework/YZH.Core/YZHServiceBase.cs`  
> **性质**: YZH 架构核心 — 跨项目可复用  

---

## 一、设计目标

### 1.1 解决的问题

| 问题 | 说明 |
|------|------|
| **校验逻辑分散** | 每个 Service 各自手写校验代码，重复且不一致 |
| **LINQ 翻译 Bug** | 当前 `UniqueValidationExtensions` 的 `Expression.AndAlso` 合并导致参数不匹配 |
| **错误提示不友好** | 异常直接暴露给前端，用户看到技术栈信息 |
| **删除校验缺失** | 复杂关联关系下，删除前缺少统一的校验机制 |
| **代码重复** | 每个 Service 都有类似的 try-catch + 校板代码 |
| **无生命周期钩子** | Vol ServiceBase 只有 delegate 方式的钩子，没有 virtual 方法 |
| **查询视图与保存实体混用** | 查询需要 JOIN 视图，保存用实体，当前基类没有区分 |

### 1.2 设计原则

1. **与 Vol 架构一致**：单泛型 `YZHServiceBase<TEntity, TRepository>`，继承 Vol 的 `ServiceBase`
2. **声明式优先**：实体通过 `[UniqueField]`、`[QueryView]` 等特性声明行为，无需手写代码
3. **模板方法**：基类提供默认实现（校验、生命周期），子类按需覆写
4. **单一职责**：Validator 只负责校验（SELECT COUNT），**不做数据持久化**
5. **SQL 替代 LINQ**：用 Dapper 执行原生 SQL，避免 EF Core 表达式树翻译问题
6. **架构层归属**：放在 `YZH.Core`，可跨项目复用

### 1.3 核心理念

```
Validator 只校验，不保存：
  ✅ SELECT COUNT(1) FROM ... （判断是否重复）
  ✅ SELECT COUNT(1) FROM ... （检查关联关系）
  ❌ INSERT / UPDATE / DELETE （不做，由 Vol 框架负责）

YZHServiceBase 提供完整生命周期：
  OnAdding → Validate → base.Add → OnAdded
  OnUpdating → Validate → base.Update → OnUpdated
  OnDeleting → Validate → base.Del → OnDeleted
```

---

## 二、架构定位

### 2.1 整体架构

```
VOL.Core/ServiceBase<TEntity, TRepository>        ← Vol 框架基类（不动）
    ↑ 继承
YZH.Core/YZHServiceBase<TEntity, TRepository>     ← YZH 架构基类（新增）
    ↑ 继承
VOL.CERT/CertCertificationBodyService             ← 业务 Service（简化）
```

### 2.2 文件结构

```
YZH-Framework/YZH.Core/
├── Validation/
│   ├── EntityValidationHandler.cs    ← 校验处理器（只做校验）
│   ├── EntityValidationResult.cs     ← 校验结果
│   └── ValidationAction.cs           ← 枚举
├── Attributes/
│   └── QueryViewAttribute.cs         ← 查询视图声明特性
└── YZHServiceBase.cs                 ← 继承 Vol ServiceBase，加入生命周期 + 校验管道
```

---

## 三、特性设计

### 3.1 [QueryView] 查询视图声明特性

```csharp
namespace YZH.Core.Attributes
{
    /// <summary>
    /// 声明实体对应的查询视图类型。
    /// 
    /// 用于区分"查询用的视图"和"保存用的实体"。
    /// 当实体标记了此特性，YZHServiceBase.GetPageData 会自动查询视图类型。
    /// 
    /// 使用示例：
    /// <code>
    /// [Entity(TableCnName = "ISO标准管理", TableName = "cert_iso_standard")]
    /// [QueryView(typeof(ISOStandardView))]
    /// public class ISOStandard : YZHBaseEntity
    /// {
    ///     public string StandardCode { get; set; }
    ///     public string StandardName { get; set; }
    /// }
    /// 
    /// // 视图类：继承实体，添加 [NotMapped] 的查询字段
    /// [NotMapped]
    /// public class ISOStandardView : ISOStandard
    /// {
    ///     public string CategoryName { get; set; }  // 字典翻译
    ///     public string StatusName { get; set; }    // 字典翻译
    /// }
    /// </code>
    /// 
    /// 设计说明：
    /// - 保持单泛型（与 Vol 一致），通过特性而非泛型参数声明视图
    /// - 视图类通过继承实体类实现（TView : TEntity）
    /// - [NotMapped] 确保视图特有字段不会被 EF Core 映射到实体表
    /// - JSON 序列化时，运行时类型是 TView，所以视图字段会自动输出
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class QueryViewAttribute : Attribute
    {
        /// <summary>
        /// 查询视图类型（必须继承自实体类型）
        /// </summary>
        public Type ViewType { get; }

        public QueryViewAttribute(Type viewType)
        {
            ViewType = viewType;
        }
    }
}
```

### 3.2 当前项目中可用的特性汇总

| 特性 | 用途 | 位置 |
|------|------|------|
| `[UniqueField("描述")]` | 单字段唯一性校验 | `VOL.Entity.CertPlatform` |
| `[UniqueField("描述", WithFields = new[] { "字段" })]` | 联合唯一性校验 | `VOL.Entity.CertPlatform` |
| `[QueryView(typeof(TView))]` | 声明查询视图类型 | `YZH.Core.Attributes`（新增） |
| `[Column("列名")]` | 数据库列名映射 | `System.ComponentModel.DataAnnotations.Schema` |
| `[NotMapped]` | 排除 EF Core 映射 | `System.ComponentModel.DataAnnotations.Schema` |
| `[Entity(TableName = "表名")]` | 声明数据库表名 | `VOL.Entity.AttributeManager` |
| `[Key]` + `[DatabaseGenerated(Identity)]` | 自增主键 | `System.ComponentModel.DataAnnotations` |
| `[Required]` | 非空校验 | `System.ComponentModel.DataAnnotations` |
| `[StringLength(n)]` | 长度校验 | `System.ComponentModel.DataAnnotations` |

### 3.3 [UniqueField] 使用统计

| 类型 | 数量 | 示例 |
|------|------|------|
| **单字段唯一** | 25+ | `CertificationBody.CbCode`、`CertStage.StageCode` |
| **联合唯一（2字段）** | 8+ | `ISOStandard.StandardCode + VersionYear` |
| **联合唯一（3字段）** | 2+ | `YzhFieldConfig.PageKey + FieldName + OrgCode` |

### 3.4 Code 作为业务主键

所有体系认证表用 `Code` 作为业务主键（而非数据库自增 `Id`）：

```csharp
// YZHBaseEntity 中定义
[MaxLength(100)]
[Column("code")]
public string Code { get; set; } = Guid.NewGuid().ToString("N");
```

唯一性校验 SQL 模式：

```sql
-- 新增校验
SELECT COUNT(1) FROM cert_certification_body WHERE name = @name

-- 修改校验（排除自身，用 Code）
SELECT COUNT(1) FROM cert_certification_body WHERE name = @name AND code <> @excludeCode

-- 联合唯一校验
SELECT COUNT(1) FROM cert_standard_phase_config 
WHERE standard_code = @standardCode AND phase_code = @phaseCode AND code <> @excludeCode
```

---

## 四、类定义

### 4.1 ValidationAction 枚举

```csharp
namespace YZH.Core.Validation
{
    public enum ValidationAction
    {
        Add,
        Update,
        Delete
    }
}
```

### 4.2 EntityValidationResult 校验结果

```csharp
namespace YZH.Core.Validation
{
    /// <summary>
    /// 校验结果。只承载校验状态和错误信息，不涉及数据持久化。
    /// </summary>
    public class EntityValidationResult
    {
        public bool IsValid { get; set; } = true;
        public string ErrorMessage { get; set; }

        public EntityValidationResult OK()
        {
            IsValid = true;
            ErrorMessage = null;
            return this;
        }

        public EntityValidationResult Error(string message)
        {
            IsValid = false;
            ErrorMessage = message;
            return this;
        }

        /// <summary>
        /// 转换为 Vol 的 WebResponseContent，可直接返回给 Controller
        /// </summary>
        public VOL.Core.Utilities.WebResponseContent ToWebResponse()
        {
            if (IsValid)
                return VOL.Core.Utilities.WebResponseContent.Instance.OK();
            return VOL.Core.Utilities.WebResponseContent.Instance.Error(ErrorMessage);
        }
    }
}
```

### 4.3 EntityValidationHandler 校验处理器

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using VOL.Core.DBManager;
using VOL.Core.Extensions;
using VOL.Entity.CertPlatform;

namespace YZH.Core.Validation
{
    /// <summary>
    /// 实体校验处理器。只做校验（SELECT COUNT），不做数据持久化。
    /// 
    /// 使用方式：
    /// <code>
    /// var handler = new EntityValidationHandler&lt;CertificationBody&gt;();
    /// var result = handler.Validate(entity, ValidationAction.Add);
    /// if (!result.IsValid) return result.ToWebResponse();
    /// </code>
    /// </summary>
    public class EntityValidationHandler<TEntity> where TEntity : class
    {
        #region 核心入口：只校验，不保存

        public virtual EntityValidationResult Validate(
            TEntity entity,
            ValidationAction action,
            string excludeCode = null)
        {
            return action switch
            {
                ValidationAction.Add    => OnAdding(entity),
                ValidationAction.Update => OnUpdating(entity, excludeCode),
                ValidationAction.Delete => OnDeleting(entity),
                _ => new EntityValidationResult().OK()
            };
        }

        #endregion

        #region 虚方法：校验扩展点

        protected virtual EntityValidationResult OnAdding(TEntity entity)
        {
            // 1. 必填/长度校验
            var result = ValidateRequiredFields(entity);
            if (!result.IsValid) return result;
            // 2. 唯一性校验
            return ValidateUniqueFields(entity, isAdd: true);
        }

        protected virtual EntityValidationResult OnUpdating(TEntity entity, string excludeCode)
        {
            // 1. 必填/长度校验
            var result = ValidateRequiredFields(entity);
            if (!result.IsValid) return result;
            // 2. 唯一性校验
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

        /// <summary>获取字段中文名（优先 Display → UniqueField → 属性名）</summary>
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
        /// 新增：SELECT COUNT(1) FROM {table} WHERE {col} = @val
        /// 修改：SELECT COUNT(1) FROM {table} WHERE {col} = @val AND code <> @excludeCode
        /// 联合：SELECT COUNT(1) FROM {table} WHERE {col1} = @val1 AND {col2} = @val2 AND code <> @excludeCode
        /// </summary>
        protected virtual EntityValidationResult ValidateUniqueFields(
            TEntity entity, bool isAdd, string excludeCode = null)
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
                var attr = prop.GetCustomAttribute<UniqueFieldAttribute>();
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
                    string valueDisplay = value.ToString();
                    if (attr.WithFields != null && attr.WithFields.Length > 0)
                    {
                        var withValues = new List<string>();
                        foreach (var wf in attr.WithFields)
                        {
                            var wp = entityType.GetProperty(wf);
                            var wv = wp?.GetValue(entity);
                            if (wv != null) withValues.Add(wv.ToString());
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

        protected string GetCodeValue(TEntity entity)
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

        protected string GetFriendlyErrorMessage(Exception ex)
        {
            var message = ex.Message;
            if (message.Contains("duplicate entry")) return "数据重复，请检查唯一性字段";
            if (message.Contains("foreign key constraint")) return "存在关联数据，无法操作";
            return message.Length > 200 ? message.Substring(0, 200) + "..." : message;
        }

        #endregion
    }
}
```

### 4.4 YZHServiceBase 继承基类

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using VOL.Core.BaseProvider;
using VOL.Core.DBManager;
using VOL.Core.Extensions;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;
using YZH.Core.Attributes;
using YZH.Core.Validation;

namespace YZH.Core
{
    /// <summary>
    /// YZH 业务 Service 基类，继承 Vol 的 ServiceBase。
    /// 
    /// 新增能力：
    /// 1. 完整生命周期钩子（OnAdding/OnAdded/OnUpdating/OnUpdated/OnDeleting/OnDeleted）
    /// 2. 自动校验管道（EntityValidationHandler）
    /// 3. 查询视图声明（[QueryView] 特性）
    /// 4. 统一异常兜底
    /// 
    /// 使用方式：
    /// <code>
    /// // 最简使用：什么都不用写，基类自动处理
    /// public class CertStageService 
    ///     : YZHServiceBase&lt;CertStage, ICertStageRepository&gt;
    ///     , ICertStageService, IDependency
    /// { }
    /// 
    /// // 需要自定义校验：覆写 CreateValidator
    /// public class CertificationBodyService 
    ///     : YZHServiceBase&lt;CertificationBody, ICertCertificationBodyRepository&gt;
    ///     , ICertCertificationBodyService, IDependency
    /// {
    ///     protected override EntityValidationHandler&lt;CertificationBody&gt; CreateValidator()
    ///         =&gt; new CertificationBodyValidator();
    /// }
    /// 
    /// // 需要删除校验：覆写 OnDeleting
    /// public class ISOStandardService 
    ///     : YZHServiceBase&lt;ISOStandard, IISOStandardRepository&gt;
    ///     , IISOStandardService, IDependency
    /// {
    ///     protected override EntityValidationResult OnDeleting(
    ///         object[] keys, List&lt;ISOStandard&gt; entities)
    ///     {
    ///         var entity = entities[0];
    ///         var count = GetRelatedCount("cert_iso_clause", "standard_code", entity.StandardCode);
    ///         if (count &gt; 0)
    ///             return new EntityValidationResult().Error($"该标准下有 {count} 个条款，请先删除");
    ///         return new EntityValidationResult().OK();
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class YZHServiceBase<TEntity, TRepository>
        : ServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        public YZHServiceBase() { }
        public YZHServiceBase(TRepository repository) : base(repository) { }

        #region 1. 校验器创建（子类可覆写）

        /// <summary>
        /// 创建校验器。默认使用 EntityValidationHandler。
        /// 子类可覆写，返回自定义的 Validator。
        /// </summary>
        protected virtual EntityValidationHandler<TEntity> CreateValidator()
            => new EntityValidationHandler<TEntity>();

        #endregion

        #region 2. 生命周期虚方法（子类按需覆写）

        // ====== 新增 ======

        /// <summary>新增前校验。返回 Error 则阻止保存。</summary>
        protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;

        /// <summary>新增后处理。此时数据已保存到数据库。</summary>
        protected virtual void OnAdded(TEntity entity) { }

        // ====== 修改 ======

        /// <summary>修改前校验。返回 Error 则阻止保存。</summary>
        protected virtual EntityValidationResult OnUpdating(TEntity entity, string excludeCode) => OK;

        /// <summary>修改后处理。此时数据已保存到数据库。</summary>
        protected virtual void OnUpdated(TEntity entity) { }

        // ====== 删除 ======

        /// <summary>
        /// 删除前校验。返回 Error 则阻止删除。
        /// keys: 前端传来的主键数组
        /// entities: 查询到的待删除实体列表（用于关联关系校验）
        /// </summary>
        protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;

        /// <summary>删除后处理。此时数据已从数据库删除。</summary>
        protected virtual void OnDeleted(object[] keys) { }

        // ====== 辅助常量 ======
        private static readonly EntityValidationResult OK = new EntityValidationResult().OK();

        #endregion

        #region 3. 查询视图支持

        /// <summary>
        /// 获取实体声明的查询视图类型。
        /// 如果实体标记了 [QueryView(typeof(TView))]，返回 TView；否则返回 null。
        /// </summary>
        protected Type GetQueryViewType()
        {
            var attr = typeof(TEntity).GetCustomAttribute<QueryViewAttribute>();
            return attr?.ViewType;
        }

        /// <summary>
        /// 重写 GetPageData：如果实体声明了 [QueryView]，自动查询视图。
        /// 
        /// 原理：
        /// 1. 通过 Dapper 查询视图（返回 List&lt;TView&gt;）
        /// 2. Cast 为 List&lt;TEntity&gt;（TView : TEntity，安全）
        /// 3. JSON 序列化时，运行时类型是 TView，视图字段自动输出
        /// </summary>
        public override PageGridData<TEntity> GetPageData(PageDataOptions options)
        {
            var viewType = GetQueryViewType();
            if (viewType != null && viewType != typeof(TEntity))
            {
                return GetPageDataFromView(options, viewType);
            }
            return base.GetPageData(options);
        }

        /// <summary>
        /// 从视图查询分页数据。
        /// 子类可覆写此方法自定义视图查询逻辑。
        /// </summary>
        protected virtual PageGridData<TEntity> GetPageDataFromView(
            PageDataOptions options, Type viewType)
        {
            // 默认实现：用 Dapper 查询视图
            var tableName = viewType.GetCustomAttribute<EntityAttribute>()?.TableName
                ?? viewType.Name;

            // 构建分页查询（简化版，实际应复用 Vol 的查询构建逻辑）
            var parameters = new Dictionary<string, object>();
            var whereClause = "1=1"; // 默认无条件

            var countSql = $"SELECT COUNT(1) FROM {tableName} WHERE {whereClause} AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            var total = Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(countSql, parameters));

            var dataSql = $"SELECT * FROM {tableName} WHERE {whereClause} AND (IsDeleted = 0 OR IsDeleted IS NULL) " +
                          $"ORDER BY Id DESC LIMIT @offset, @rows";
            parameters["offset"] = (options.Page - 1) * options.Rows;
            parameters["rows"] = options.Rows;

            // 用 Dapper 查询视图类型
            var viewData = DBServerProvider.SqlDapper.QueryList<dynamic>(dataSql, parameters);

            // 转换为 TEntity 列表（运行时类型是 TView，JSON 序列化会包含视图字段）
            var rows = viewData.Select(d => (TEntity)d).ToList();

            return new PageGridData<TEntity>
            {
                rows = rows,
                total = total
            };
        }

        #endregion

        #region 4. 重写 Add：生命周期 + 校验

        public override WebResponseContent Add(SaveModel saveDataModel)
        {
            // 1. 转换实体
            var entity = saveDataModel.MainData.DicToEntity<TEntity>();

            // 2. 新增前校验（自动唯一性校验）
            var validator = CreateValidator();
            var validateResult = validator.Validate(entity, ValidationAction.Add);
            if (!validateResult.IsValid)
                return validateResult.ToWebResponse();

            // 3. 自定义新增前校验
            var customResult = OnAdding(entity);
            if (!customResult.IsValid)
                return customResult.ToWebResponse();

            // 4. 执行保存（Vol 框架标准流程）
            try
            {
                var result = base.Add(saveDataModel);

                // 5. 新增后处理
                if (result.Status)
                    OnAdded(entity);

                return result;
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"保存失败：{GetFriendlyErrorMessage(ex)}");
            }
        }

        #endregion

        #region 5. 重写 Update：生命周期 + 校验

        public override WebResponseContent Update(SaveModel saveDataModel)
        {
            // 1. 转换实体
            var entity = saveDataModel.MainData.DicToEntity<TEntity>();

            // 2. 获取 Code（业务主键，用于排除自身）
            var excludeCode = typeof(TEntity).GetProperty("Code")?.GetValue(entity)?.ToString();

            // 3. 修改前校验（自动唯一性校验）
            var validator = CreateValidator();
            var validateResult = validator.Validate(entity, ValidationAction.Update, excludeCode);
            if (!validateResult.IsValid)
                return validateResult.ToWebResponse();

            // 4. 自定义修改前校验
            var customResult = OnUpdating(entity, excludeCode);
            if (!customResult.IsValid)
                return customResult.ToWebResponse();

            // 5. 执行保存
            try
            {
                var result = base.Update(saveDataModel);

                // 6. 修改后处理
                if (result.Status)
                    OnUpdated(entity);

                return result;
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"保存失败：{GetFriendlyErrorMessage(ex)}");
            }
        }

        #endregion

        #region 6. 重写 Del：生命周期 + 校验

        public override WebResponseContent Del(object[] keys, bool delList = true)
        {
            // 1. 查询待删除实体（用于关联关系校验）
            var keyName = typeof(TEntity).GetKeyName();
            var keyExpression = keyName.CreateExpression<TEntity>(keys[0].ToString(), LinqExpressionType.Equal);
            var entityList = repository.FindAsIQueryable(keyExpression).ToList();

            if (entityList == null || entityList.Count == 0)
                return new WebResponseContent().Error("未找到要删除的数据");

            // 2. 删除前校验
            var customResult = OnDeleting(keys, entityList);
            if (!customResult.IsValid)
                return customResult.ToWebResponse();

            // 3. 执行删除
            try
            {
                var result = base.Del(keys, delList);

                // 4. 删除后处理
                if (result.Status)
                    OnDeleted(keys);

                return result;
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"删除失败：{GetFriendlyErrorMessage(ex)}");
            }
        }

        #endregion

        #region 7. 辅助方法

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

        protected string GetFriendlyErrorMessage(Exception ex)
        {
            var message = ex.Message;
            if (message.Contains("duplicate entry")) return "数据重复，请检查唯一性字段";
            if (message.Contains("foreign key constraint")) return "存在关联数据，无法操作";
            return message.Length > 200 ? message.Substring(0, 200) + "..." : message;
        }

        #endregion
    }
}
```

---

## 五、使用指南

### 5.1 场景一：最简使用（零配置）

```csharp
// 之前：每个 Service 都要手写校验 + try-catch
public class CertStageService 
    : ServiceBase<CertStage, ICertStageRepository>
{
    public override WebResponseContent Add(SaveModel saveDataModel)
    {
        // 手动校验...
        // 手动 try-catch...
        return base.Add(saveDataModel);
    }
}

// 之后：什么都不用写
public class CertStageService 
    : YZHServiceBase<CertStage, ICertStageRepository>
    , ICertStageService, IDependency
{
    // 基类自动处理：唯一性校验 + 异常兜底
}
```

### 5.2 场景二：声明查询视图

```csharp
// 实体：声明对应的查询视图
[Entity(TableCnName = "ISO标准管理", TableName = "cert_iso_standard")]
[QueryView(typeof(ISOStandardView))]  // ← 声明查询视图
public class ISOStandard : YZHBaseEntity
{
    [UniqueField("标准编号", WithFields = new[] { "VersionYear" })]
    [Column("standard_code")]
    public string StandardCode { get; set; }

    [Column("standard_name")]
    public string StandardName { get; set; }

    [Column("version_year")]
    public int VersionYear { get; set; }
}

// 视图：继承实体，添加查询特有字段
[NotMapped]
public class ISOStandardView : ISOStandard
{
    public string CategoryName { get; set; }  // 字典翻译
    public string StatusName { get; set; }    // 字典翻译
}

// Service：自动使用视图查询
public class ISOStandardService 
    : YZHServiceBase<ISOStandard, IISOStandardRepository>
    , IISOStandardService, IDependency
{
    // GetPageData 自动查询 ISOStandardView
    // Add/Update/Del 使用 ISOStandard
}
```

### 5.3 场景三：自定义新增校验

```csharp
// 校验器
public class CertificationBodyValidator 
    : EntityValidationHandler<CertificationBody>
{
    protected override EntityValidationResult OnAdding(CertificationBody entity)
    {
        var result = base.OnAdding(entity);  // 默认唯一性校验
        if (!result.IsValid) return result;

        // 自定义业务校验
        if (string.IsNullOrEmpty(entity.CbCode))
            return result.Error("机构编号不能为空");

        return result;
    }
}

// Service：指定自定义校验器
public class CertCertificationBodyService
    : YZHServiceBase<CertificationBody, ICertCertificationBodyRepository>
    , ICertCertificationBodyService, IDependency
{
    protected override EntityValidationHandler<CertificationBody> CreateValidator()
        => new CertificationBodyValidator();
}
```

### 5.4 场景四：删除校验（核心场景）

```csharp
// ISOStandard 删除校验：检查关联关系
public class ISOStandardService
    : YZHServiceBase<ISOStandard, IISOStandardRepository>
    , IISOStandardService, IDependency
{
    protected override EntityValidationResult OnDeleting(
        object[] keys, List<ISOStandard> entities)
    {
        var entity = entities[0];
        var standardCode = entity.StandardCode;

        // 检查是否有下属条款
        var clauseCount = GetRelatedCount("cert_iso_clause", "standard_code", standardCode);
        if (clauseCount > 0)
            return new EntityValidationResult().Error(
                $"该标准下有 {clauseCount} 个条款，请先删除条款");

        // 检查是否有目录配置
        var dirCount = GetRelatedCount(
            "cert_standard_directory_config", "StandardCode", standardCode);
        if (dirCount > 0)
            return new EntityValidationResult().Error(
                $"该标准已配置目录结构，请先删除目录配置");

        // 检查是否有企业正在使用
        var enterpriseCount = GetRelatedCount(
            "cert_enterprise_standard",
            new Dictionary<string, object>
            {
                ["standard_code"] = standardCode,
                ["enable"] = true
            });
        if (enterpriseCount > 0)
            return new EntityValidationResult().Error(
                $"有 {enterpriseCount} 家企业正在使用该标准，无法删除");

        return new EntityValidationResult().OK();
    }
}
```

### 5.5 场景五：删除校验 — 认证机构

```csharp
public class CertCertificationBodyService
    : YZHServiceBase<CertificationBody, ICertCertificationBodyRepository>
    , ICertCertificationBodyService, IDependency
{
    protected override EntityValidationResult OnDeleting(
        object[] keys, List<CertificationBody> entities)
    {
        var entity = entities[0];
        var cbCode = entity.CbCode;

        var enterpriseCount = GetRelatedCount("cert_enterprise", "cb_code", cbCode);
        if (enterpriseCount > 0)
            return new EntityValidationResult().Error(
                $"该机构下有 {enterpriseCount} 家关联企业，请先解绑");

        var auditorCount = GetRelatedCount("cert_auditor_profile", "cb_code", cbCode);
        if (auditorCount > 0)
            return new EntityValidationResult().Error(
                $"该机构下有 {auditorCount} 名审核员，请先移除");

        var taskCount = GetRelatedCount(
            "cert_audit_task",
            new Dictionary<string, object>
            {
                ["cb_code"] = cbCode,
                ["status"] = "in_progress"
            });
        if (taskCount > 0)
            return new EntityValidationResult().Error(
                $"该机构有 {taskCount} 个进行中的认证任务，无法删除");

        return new EntityValidationResult().OK();
    }
}
```

### 5.6 场景六：修改校验 — 字段变更限制

```csharp
public class CertCertificationBodyService
    : YZHServiceBase<CertificationBody, ICertCertificationBodyRepository>
    , ICertCertificationBodyService, IDependency
{
    protected override EntityValidationResult OnUpdating(
        CertificationBody entity, string excludeCode)
    {
        // 查询数据库中的原始数据
        var dbEntity = DBServerProvider.SqlDapper.QueryFirst<CertificationBody>(
            "SELECT * FROM cert_certification_body WHERE code = @code",
            new { code = excludeCode });

        // 某些字段不允许修改
        if (dbEntity != null && dbEntity.CbCode != entity.CbCode)
            return new EntityValidationResult().Error("机构编号不允许修改");

        return new EntityValidationResult().OK();
    }
}
```

---

## 六、完整调用流程

### 6.1 新增流程

```
前端 POST /api/CertCertificationBody/add
  → ApiBaseController.Add(SaveModel)
    → CertCertificationBodyService.Add(SaveModel)  // 继承自 YZHServiceBase
      → 1. saveModel.MainData.DicToEntity<CertificationBody>()
      → 2. CreateValidator().Validate(entity, ValidationAction.Add)
        → ValidateUniqueFields: SELECT COUNT(1) FROM cert_certification_body WHERE name = @name
      → 3. OnAdding(entity)  // 自定义校验
      → 4. base.Add(saveModel)  // Vol 框架标准流程
      → 5. OnAdded(entity)  // 保存后处理
```

### 6.2 删除流程

```
前端 POST /api/ISOStandard/Del
  → ApiBaseController.Del(keys)
    → ISOStandardService.Del(keys)  // 继承自 YZHServiceBase
      → 1. 查询待删除实体
      → 2. OnDeleting(keys, entities)  // 关联关系校验
        → GetRelatedCount("cert_iso_clause", "standard_code", code)
        → GetRelatedCount("cert_standard_directory_config", "StandardCode", code)
      → 3. base.Del(keys)  // Vol 框架标准流程
      → 4. OnDeleted(keys)  // 删除后处理
```

### 6.3 查询流程（带视图）

```
前端 POST /api/ISOStandard/getPageData
  → ApiBaseController.GetPageData(options)
    → ISOStandardService.GetPageData(options)  // 继承自 YZHServiceBase
      → 1. 检查 [QueryView] 特性 → 发现 ISOStandardView
      → 2. GetPageDataFromView(options, typeof(ISOStandardView))
        → Dapper 查询 v_iso_standard 视图
        → 返回 List<ISOStandardView>（运行时类型）
      → 3. Cast 为 List<ISOStandard>（TView : TEntity，安全）
      → 4. JSON 序列化时，CategoryName/StatusName 自动输出
```

---

## 七、迁移指南

### 7.1 可删除的代码

| 文件 | 说明 |
|------|------|
| `VOL.CERT/Extensions/UniqueValidationExtensions.cs` | 被 `EntityValidationHandler.ValidateUniqueFields` 替代 |
| `CertCertificationBodyService` 中的手动 try-catch | 被 `YZHServiceBase` 统一处理 |
| `CertCertificationBodyService` 中的手动 `ValidateUniqueFieldsFromDict` 调用 | 被 `YZHServiceBase.Add()` 自动调用 |

### 7.2 迁移步骤

**第一步**：在 `YZH.Core` 中创建核心类
- `Validation/EntityValidationHandler.cs`
- `Validation/EntityValidationResult.cs`
- `Validation/ValidationAction.cs`
- `Attributes/QueryViewAttribute.cs`
- `YZHServiceBase.cs`

**第二步**：修改业务 Service 的继承关系
```csharp
// 之前
public class CertCertificationBodyService 
    : ServiceBase<CertificationBody, ICertCertificationBodyRepository>

// 之后
public class CertCertificationBodyService 
    : YZHServiceBase<CertificationBody, ICertCertificationBodyRepository>
```

**第三步**：删除 Service 中的手动校验代码

**第四步**：为需要自定义校验的 Service 覆写虚方法

### 7.3 迁移优先级

| 优先级 | Service | 说明 |
|--------|---------|------|
| P0 | `CertCertificationBodyService` | 当前有 Bug，优先修复 |
| P1 | `ISOStandardService` | 有 [UniqueField] + 联合唯一 + 视图 |
| P1 | `CertStageService` | 有 [UniqueField] + 视图 |
| P2 | `StandardDirectoryConfigService` | 有 [UniqueField] + 联合唯一 |
| P3 | 其他 Service | 逐步迁移 |

---

## 八、注意事项

### 8.1 Validator 只校验，不保存

```
Validator 职责：
  ✅ SELECT COUNT(1) FROM ... （判断是否重复）
  ✅ SELECT COUNT(1) FROM ... （检查关联关系）
  ❌ INSERT / UPDATE / DELETE （不做）
```

### 8.2 Code 作为业务主键

所有唯一性校验的排除条件使用 `code <> @excludeCode`。

### 8.3 生命周期执行顺序

```
Add:
  OnAdding(entity)
    → ValidateRequiredFields   // [Required] / [StringLength] 校验
    → ValidateUniqueFields    // [UniqueField] 唯一性校验
  base.Add(saveModel)         // Vol 框架标准流程
  OnAdded(entity)             // 保存后处理

Update:
  OnUpdating(entity, excludeCode)
    → ValidateRequiredFields   // [Required] / [StringLength] 校验
    → ValidateUniqueFields    // [UniqueField] 唯一性校验（排除自身 Code）
  base.Update(saveModel)      // Vol 框架标准流程
  OnUpdated(entity)           // 保存后处理

Delete:
  OnDeleting(keys, entities)  // 关联关系校验（GetRelatedCount）
  base.Del(keys)              // Vol 框架标准流程
  OnDeleted(keys)             // 删除后处理
```

### 8.4 [QueryView] 与 [NotMapped] 的关系

- `[QueryView(typeof(TView))]` 标记在**实体**上，声明查询视图类型
- `[NotMapped]` 标记在**视图**上，阻止 EF Core 映射视图特有字段
- 视图类必须**继承实体类**（`TView : TEntity`），确保 Cast 安全

### 8.5 自动必填/长度校验

`EntityValidationHandler` 在唯一性校验之前，自动检查 `[Required]` 和 `[StringLength]` 特性：

```csharp
// EntityValidationHandler.ValidateRequiredFields 方法
protected virtual EntityValidationResult ValidateRequiredFields(TEntity entity)
{
    var result = new EntityValidationResult();
    var props = typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    foreach (var prop in props)
    {
        // 跳过 NotMapped 字段
        if (prop.GetCustomAttribute<NotMappedAttribute>() != null) continue;

        var value = prop.GetValue(entity);
        var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
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
```

调用顺序：`ValidateRequiredFields` → `ValidateUniqueFields`

### 8.6 防重复提交机制

**当前状态**：后端无防重复机制，前端仅靠 `saving` 状态防按钮连点

**建议方案（前后端配合）**：

#### 前端：按钮 loading 状态（已有）
```typescript
// YzhCrudTable.vue — 已有
saving.value = true
try {
    const res = await api.add(editForm)
} finally {
    saving.value = false  // 请求完成后才释放按钮
}
```

#### 后端：ActionFilter 幂等性拦截（待实现）

在 `YZH.Core` 中新增 `YZHIdempotentActionFilter`，基于 Redis 实现：

```csharp
// YZH.Core/Idempotent/YZHIdempotentActionFilter.cs
public class YZHIdempotentActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var userId = UserContext.Current?.UserId ?? 0;
        var actionName = context.ActionDescriptor.RouteValues["action"]?.ToString();
        var tableName = GetTableName(context);

        // 生成幂等键：用户 + 操作 + 表名 + 请求体哈希
        var bodyHash = await GetBodyHash(httpContext);
        var idempotentKey = $"idempotent:{userId}:{actionName}:{tableName}:{bodyHash}";

        // Redis SET NX（不存在才设置，有效期 10 秒）
        var redis = httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var acquired = await db.StringSetAsync(idempotentKey, "1", TimeSpan.FromSeconds(10));

        if (!acquired)
        {
            context.Result = new ObjectResult(
                new WebResponseContent().Error("请勿重复提交"));
            return;
        }

        await next();
    }
}
```

#### 注册方式

```csharp
// YZHModule.cs 中注册
options.Filters.Add<YZHIdempotentActionFilter>(int.MinValue + 100);
```

#### 拦截范围

| 操作 | 拦截 | 说明 |
|------|------|------|
| Add | ✅ | 新增防重复（10秒内同用户同表同数据） |
| Update | ✅ | 修改防重复 |
| Del | ❌ | 删除不拦截（用户可能需要重试） |
| GetPageData | ❌ | 查询不拦截 |

### 8.7 后续演进

- 支持异步版本 `AddAsync` / `UpdateAsync` / `DelAsync`
- 幂等性过滤器接入 Redis（需项目部署 Redis）
- 可将 `YZHServiceBase` 的校验管道扩展为 pre/post hook 机制
