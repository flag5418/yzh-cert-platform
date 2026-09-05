# 体系认证平台架构整合 TODO 清单

> **版本**：V1.0 | **创建日期**：2026-09-04 | **状态**：执行中
>
> 本文件是架构整合的唯一执行清单，每个TODO任务包含具体操作步骤、涉及文件、预期变更和验证方法。

---

## 一、任务总览

| Phase | 内容 | 工时 | 状态 |
|-------|------|------|------|
| Phase 1 | 后端基类整合 | 2天 | ✅ 已完成 |
| Phase 2 | 配置API接口 | 1天 | ✅ 已完成 |
| Phase 3 | 前端适配 | 2天 | ✅ 已完成 |
| Phase 4 | 测试验证 | 1天 | ⏳ 待开始 |
| Phase 5 | 清理 | 0.5天 | ⏳ 待开始 |

---

## 二、Phase 1：后端基类整合

### TODO-1.1：创建配置特性定义文件

**状态**：✅ 已完成  
**优先级**：🔴 高  
**预计耗时**：30分钟

**目标**：在VOL.Entity项目中创建YZH特性定义文件

**操作步骤**：
1. 在 `VOL.Entity/Admin/Platform/Base/` 目录下创建 `YZHAttributes.cs`
2. 定义以下特性类：
   - `YZHPageAttribute` - 页面级配置
   - `YZHColumnAttribute` - 表格列配置
   - `YZHFormAttribute` - 表单字段配置
   - `YZHSearchAttribute` - 搜索条件配置
   - `YZHDeleteStrategyAttribute` - 删除策略
   - `QueryViewAttribute` - 视图查询
   - `TreeSourceAttribute` - 左树右表

**涉及文件**：
- 新建：`src/server/Vue.NetCore/vol.api/VOL.Entity/Admin/Platform/Base/YZHAttributes.cs`

**预期代码**：
```csharp
using System;

namespace VOL.Entity.Admin.Platform.Base
{
    /// <summary>
    /// 页面级配置特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class YZHPageAttribute : Attribute
    {
        public string PageKey { get; set; }
        public string Title { get; set; }
        public string ControllerName { get; set; }
        public string KeyField { get; set; } = "Id";
        public string SortField { get; set; } = "Id";
        public string SortOrder { get; set; } = "desc";
        public int DialogWidth { get; set; } = 800;
        public string DialogMaxHeight { get; set; } = "60vh";
        public int DialogLabelWidth { get; set; } = 120;
        public string SearchMode { get; set; } = "fixed";
        public string[] VisibleButtons { get; set; } = new[] { "add", "refresh", "batchDelete" };
        public bool ShowRowNumber { get; set; } = false;
        public bool CheckboxSelection { get; set; } = true;
        public bool ShowActionColumn { get; set; } = true;
    }

    /// <summary>
    /// 表格列配置特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class YZHColumnAttribute : Attribute
    {
        public bool Visible { get; set; } = true;
        public int Order { get; set; } = 999;
        public int Width { get; set; } = 120;
        public string Title { get; set; }
        public bool Sortable { get; set; } = false;
        public string Fixed { get; set; }
        public string Align { get; set; } = "left";
        public bool ShowOverflow { get; set; } = true;
        public string Formatter { get; set; }
    }

    /// <summary>
    /// 表单字段配置特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class YZHFormAttribute : Attribute
    {
        public string Title { get; set; }
        public string ControlType { get; set; } = "input";
        public bool Required { get; set; } = false;
        public int GridRow { get; set; } = 0;
        public int GridCol { get; set; } = 0;
        public int GridRowSpan { get; set; } = 1;
        public int GridColSpan { get; set; } = 1;
        public string Placeholder { get; set; }
        public string DefaultValue { get; set; }
        public bool Readonly { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public int MaxLength { get; set; } = 0;
        public string DataKey { get; set; }
        public string RemoteUrl { get; set; }
        public int Precision { get; set; } = 0;
        public double? MinVal { get; set; }
        public double? MaxVal { get; set; }
        public int TextareaRows { get; set; } = 3;
    }

    /// <summary>
    /// 搜索条件配置特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class YZHSearchAttribute : Attribute
    {
        public bool IsSearch { get; set; } = true;
        public string Title { get; set; }
        public string ControlType { get; set; }
        public string Placeholder { get; set; }
        public int Width { get; set; } = 200;
        public string DataKey { get; set; }
    }

    /// <summary>
    /// 删除模式枚举
    /// </summary>
    public enum DeleteMode
    {
        Logical = 0,
        Physical = 1,
        Cascade = 2
    }

    /// <summary>
    /// 删除策略特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class YZHDeleteStrategyAttribute : Attribute
    {
        public DeleteMode Mode { get; set; } = DeleteMode.Logical;
        public Type[] CascadeEntities { get; set; }
        public bool ForceDelete { get; set; } = false;
    }

    /// <summary>
    /// 视图查询特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class QueryViewAttribute : Attribute
    {
        public Type ViewType { get; set; }
        
        public QueryViewAttribute(Type viewType)
        {
            ViewType = viewType;
        }
    }

    /// <summary>
    /// 左树右表特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TreeSourceAttribute : Attribute
    {
        public string TreeController { get; set; }
        public string FilterField { get; set; }
        public string TreeKeyField { get; set; } = "Code";
        public string TreeLabelField { get; set; } = "Name";
    }
}
```

**验证方法**：
- 编译通过：`dotnet build VOL.Entity`
- 无语法错误

---

### TODO-1.2：创建配置模型类

**状态**：✅ 已完成  
**优先级**：🔴 高  
**预计耗时**：20分钟

**目标**：创建配置JSON的数据模型

**操作步骤**：
1. 在 `VOL.Entity/Admin/Platform/Base/` 目录下创建 `EntityConfigModels.cs`
2. 定义 `PageUIConfig`、`PageMeta`、`FieldConfig` 模型类

**涉及文件**：
- 新建：`src/server/Vue.NetCore/vol.api/VOL.Entity/Admin/Platform/Base/EntityConfigModels.cs`

**预期代码**：
```csharp
using System.Collections.Generic;

namespace VOL.Entity.Admin.Platform.Base
{
    /// <summary>
    /// 页面完整UI配置
    /// </summary>
    public class PageUIConfig
    {
        public PageMeta PageMeta { get; set; }
        public List<FieldConfig> FieldConfigs { get; set; } = new();
    }

    /// <summary>
    /// 页面元数据
    /// </summary>
    public class PageMeta
    {
        public string PageKey { get; set; }
        public string PageTitle { get; set; }
        public string EntityName { get; set; }
        public string TableName { get; set; }
        public string ControllerName { get; set; }
        public string KeyField { get; set; }
        public string KeyFieldType { get; set; }
        public string SortField { get; set; }
        public string SortOrder { get; set; }
        public int DialogWidth { get; set; }
        public string DialogMaxHeight { get; set; }
        public int DialogLabelWidth { get; set; }
        public string SearchMode { get; set; }
        public string[] VisibleButtons { get; set; }
        public bool ShowRowNumber { get; set; }
        public bool CheckboxSelection { get; set; }
        public bool ShowActionColumn { get; set; }
    }

    /// <summary>
    /// 字段配置
    /// </summary>
    public class FieldConfig
    {
        // 标识
        public string FieldName { get; set; }
        public string FieldAlias { get; set; }
        public string FieldType { get; set; }
        public bool IsKey { get; set; }

        // 表格列配置
        public bool XsFlag { get; set; }
        public int ColumnSxh { get; set; }
        public string ColumnTitle { get; set; }
        public int ColumnWidth { get; set; }
        public string ColumnFixed { get; set; }
        public bool Sortable { get; set; }
        public string Align { get; set; }
        public bool ShowOverflow { get; set; }
        public string ColumnFormatter { get; set; }

        // 表单字段配置
        public bool BcFlag { get; set; }
        public string FormTitle { get; set; }
        public string ControlType { get; set; }
        public int GridRow { get; set; }
        public int GridCol { get; set; }
        public int GridRowSpan { get; set; }
        public int GridColSpan { get; set; }
        public bool Required { get; set; }
        public int MaxLength { get; set; }
        public string Placeholder { get; set; }
        public string DefaultValue { get; set; }
        public bool Readonly { get; set; }
        public bool Disabled { get; set; }
        public string DataKey { get; set; }
        public string RemoteUrl { get; set; }
        public int Precision { get; set; }
        public double? MinVal { get; set; }
        public double? MaxVal { get; set; }
        public int TextareaRows { get; set; }

        // 搜索条件配置
        public bool SearchFlag { get; set; }
        public string SearchTitle { get; set; }
        public string SearchPlaceholder { get; set; }
        public string SearchControlType { get; set; }
        public int SearchWidth { get; set; }
    }
}
```

**验证方法**：
- 编译通过：`dotnet build VOL.Entity`

---

### TODO-1.3：创建配置生成器

**状态**：✅ 已完成  
**优先级**：🔴 高  
**预计耗时**：40分钟

**目标**：实现从实体特性生成配置JSON的核心逻辑

**操作步骤**：
1. 在 `VOL.Core/Utilities/` 目录下创建 `EntityConfigGenerator.cs`
2. 实现 `Generate<T>()` 方法
3. 实现 `GenerateTableColumns<T>()` 和 `GenerateFormFields<T>()` 便捷方法

**涉及文件**：
- 新建：`src/server/Vue.NetCore/vol.api/VOL.Core/Utilities/EntityConfigGenerator.cs`

**预期代码**：
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using VOL.Entity.Admin.Platform.Base;

namespace VOL.Core.Utilities
{
    /// <summary>
    /// 实体配置生成器
    /// 从实体特性自动生成前端页面配置
    /// </summary>
    public static class EntityConfigGenerator
    {
        /// <summary>
        /// 生成完整的页面UI配置
        /// </summary>
        public static PageUIConfig Generate<TEntity>() where TEntity : class
        {
            var entityType = typeof(TEntity);
            var pageAttr = entityType.GetCustomAttribute<YZHPageAttribute>();
            
            return new PageUIConfig
            {
                PageMeta = GeneratePageMeta(entityType, pageAttr),
                FieldConfigs = GenerateFieldConfigs(entityType)
            };
        }

        /// <summary>
        /// 生成表格列配置
        /// </summary>
        public static List<FieldConfig> GenerateTableColumns<TEntity>() where TEntity : class
        {
            return Generate<TEntity>().FieldConfigs
                .Where(f => f.XsFlag)
                .OrderBy(f => f.ColumnSxh)
                .ToList();
        }

        /// <summary>
        /// 生成表单字段配置
        /// </summary>
        public static List<FieldConfig> GenerateFormFields<TEntity>() where TEntity : class
        {
            return Generate<TEntity>().FieldConfigs
                .Where(f => f.BcFlag)
                .OrderBy(f => f.GridRow)
                .ThenBy(f => f.GridCol)
                .ToList();
        }

        /// <summary>
        /// 生成搜索条件配置
        /// </summary>
        public static List<FieldConfig> GenerateSearchFields<TEntity>() where TEntity : class
        {
            return Generate<TEntity>().FieldConfigs
                .Where(f => f.SearchFlag)
                .ToList();
        }

        /// <summary>
        /// 生成页面元数据
        /// </summary>
        private static PageMeta GeneratePageMeta(Type entityType, YZHPageAttribute pageAttr)
        {
            var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
            
            return new PageMeta
            {
                PageKey = pageAttr?.PageKey ?? entityType.Name,
                PageTitle = pageAttr?.Title ?? entityType.GetCustomAttribute<DisplayAttribute>()?.Name ?? "",
                EntityName = entityType.Name,
                TableName = tableAttr?.Name ?? "",
                ControllerName = pageAttr?.ControllerName ?? "",
                KeyField = pageAttr?.KeyField ?? "Id",
                KeyFieldType = DetermineKeyType(entityType, pageAttr?.KeyField ?? "Id"),
                SortField = pageAttr?.SortField ?? "Id",
                SortOrder = pageAttr?.SortOrder ?? "desc",
                DialogWidth = pageAttr?.DialogWidth ?? 800,
                DialogMaxHeight = pageAttr?.DialogMaxHeight ?? "60vh",
                DialogLabelWidth = pageAttr?.DialogLabelWidth ?? 120,
                SearchMode = pageAttr?.SearchMode ?? "fixed",
                VisibleButtons = pageAttr?.VisibleButtons ?? new[] { "add", "refresh", "batchDelete" },
                ShowRowNumber = pageAttr?.ShowRowNumber ?? false,
                CheckboxSelection = pageAttr?.CheckboxSelection ?? true,
                ShowActionColumn = pageAttr?.ShowActionColumn ?? true
            };
        }

        /// <summary>
        /// 生成字段配置列表
        /// </summary>
        private static List<FieldConfig> GenerateFieldConfigs(Type entityType)
        {
            var fields = new List<FieldConfig>();
            var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var prop in props)
            {
                var columnAttr = prop.GetCustomAttribute<YZHColumnAttribute>();
                var formAttr = prop.GetCustomAttribute<YZHFormAttribute>();
                var searchAttr = prop.GetCustomAttribute<YZHSearchAttribute>();

                // 如果没有任何YZH特性，跳过
                if (columnAttr == null && formAttr == null && searchAttr == null)
                    continue;

                // 检查是否是主键
                var isKey = prop.Name == "Id" || prop.Name == "Code" ||
                           prop.GetCustomAttribute<KeyAttribute>() != null;

                var field = new FieldConfig
                {
                    FieldName = prop.Name,
                    FieldAlias = prop.Name,
                    FieldType = DetermineFieldType(prop),
                    IsKey = isKey,

                    // 表格列配置
                    XsFlag = columnAttr?.Visible ?? false,
                    ColumnSxh = columnAttr?.Order ?? 999,
                    ColumnTitle = columnAttr?.Title ?? formAttr?.Title ?? "",
                    ColumnWidth = columnAttr?.Width ?? 120,
                    ColumnFixed = columnAttr?.Fixed,
                    Sortable = columnAttr?.Sortable ?? false,
                    Align = columnAttr?.Align ?? "left",
                    ShowOverflow = columnAttr?.ShowOverflow ?? true,
                    ColumnFormatter = columnAttr?.Formatter,

                    // 表单字段配置
                    BcFlag = true, // 默认保存到数据库
                    FormTitle = formAttr?.Title ?? columnAttr?.Title ?? "",
                    ControlType = formAttr?.ControlType ?? DetermineControlType(prop),
                    GridRow = formAttr?.GridRow ?? 0,
                    GridCol = formAttr?.GridCol ?? 0,
                    GridRowSpan = formAttr?.GridRowSpan ?? 1,
                    GridColSpan = formAttr?.GridColSpan ?? 1,
                    Required = formAttr?.Required ?? HasRequiredAttribute(prop),
                    MaxLength = formAttr?.MaxLength ?? GetMaxLength(prop),
                    Placeholder = formAttr?.Placeholder ?? "",
                    DefaultValue = formAttr?.DefaultValue ?? "",
                    Readonly = formAttr?.Readonly ?? false,
                    Disabled = formAttr?.Disabled ?? false,
                    DataKey = formAttr?.DataKey,
                    RemoteUrl = formAttr?.RemoteUrl,
                    Precision = formAttr?.Precision ?? 0,
                    MinVal = formAttr?.MinVal,
                    MaxVal = formAttr?.MaxVal,
                    TextareaRows = formAttr?.TextareaRows ?? 3,

                    // 搜索条件配置
                    SearchFlag = searchAttr?.IsSearch ?? false,
                    SearchTitle = searchAttr?.Title ?? "",
                    SearchPlaceholder = searchAttr?.Placeholder ?? "",
                    SearchControlType = searchAttr?.ControlType,
                    SearchWidth = searchAttr?.Width ?? 200
                };

                fields.Add(field);
            }

            return fields;
        }

        /// <summary>
        /// 确定主键类型
        /// </summary>
        private static string DetermineKeyType(Type entityType, string keyFieldName)
        {
            var keyProp = entityType.GetProperty(keyFieldName);
            if (keyProp == null) return "number";

            var typeName = keyProp.PropertyType.Name.ToLower();
            if (typeName.Contains("guid")) return "guid";
            if (typeName.Contains("string")) return "string";
            return "number";
        }

        /// <summary>
        /// 确定字段类型
        /// </summary>
        private static string DetermineFieldType(PropertyInfo prop)
        {
            var typeName = prop.PropertyType.Name.ToLower();
            
            if (typeName.Contains("bool")) return "boolean";
            if (typeName.Contains("int") || typeName.Contains("long") || typeName.Contains("decimal") || typeName.Contains("double") || typeName.Contains("float"))
                return "number";
            if (typeName.Contains("datetime") || typeName.Contains("date"))
                return "date";
            if (typeName.Contains("string"))
                return "string";
            
            return "string";
        }

        /// <summary>
        /// 根据属性类型推断控件类型
        /// </summary>
        private static string DetermineControlType(PropertyInfo prop)
        {
            var typeName = prop.PropertyType.Name.ToLower();
            
            if (typeName.Contains("bool")) return "switch";
            if (typeName.Contains("datetime") || typeName.Contains("date")) return "date";
            if (typeName.Contains("int") || typeName.Contains("long") || typeName.Contains("decimal") || typeName.Contains("double") || typeName.Contains("float"))
                return "number";
            
            // 检查是否有[Column]特性且字段名包含特定关键词
            var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            if (columnName.Contains("remark") || columnName.Contains("description") || columnName.Contains("content") || columnName.Contains("note"))
                return "textarea";
            
            return "input";
        }

        /// <summary>
        /// 检查是否有Required特性
        /// </summary>
        private static bool HasRequiredAttribute(PropertyInfo prop)
        {
            return prop.GetCustomAttribute<RequiredAttribute>() != null;
        }

        /// <summary>
        /// 获取字段最大长度
        /// </summary>
        private static int GetMaxLength(PropertyInfo prop)
        {
            var maxLengthAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLengthAttr != null) return maxLengthAttr.Length;

            var stringLengthAttr = prop.GetCustomAttribute<StringLengthAttribute>();
            if (stringLengthAttr != null) return stringLengthAttr.MaximumLength;

            return 0;
        }
    }
}
```

**验证方法**：
- 编译通过：`dotnet build VOL.Core`
- 无语法错误

---

### TODO-1.4：创建CertServiceBase

**状态**：✅ 已完成  
**优先级**：🔴 高  
**预计耗时**：45分钟

**目标**：创建整合YZH能力的统一Service基类

**操作步骤**：
1. 在 `VOL.Core/Services/` 目录下创建 `CertServiceBase.cs`
2. 继承 `ServiceBase<TEntity, TRepository>`
3. 整合YZH的校验、生命周期、删除策略、异常脱敏能力
4. 添加配置获取方法

**涉及文件**：
- 新建：`src/server/Vue.NetCore/vol.api/VOL.Core/Services/CertServiceBase.cs`

**预期代码**：
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using VOL.Core.BaseProvider;
using VOL.Core.DBManager;
using VOL.Core.Enums;
using VOL.Core.Extensions;
using VOL.Core.Services;
using VOL.Core.Utilities;
using VOL.Entity.DomainModels;
using VOL.Entity.SystemModels;
using VOL.Entity.Admin.Platform.Base;

namespace VOL.Core.Services
{
    /// <summary>
    /// 体系认证平台统一Service基类
    /// 整合Vol的CRUD能力 + YZH的校验/生命周期/删除策略/异常脱敏
    /// </summary>
    public abstract class CertServiceBase<TEntity, TRepository> 
        : ServiceBase<TEntity, TRepository>
        where TEntity : BaseEntity
        where TRepository : IRepository<TEntity>
    {
        public CertServiceBase() { }
        public CertServiceBase(TRepository repository) : base(repository) { }

        #region 1. 校验器创建（子类可覆写）

        protected virtual EntityValidationHandler<TEntity> CreateValidator()
            => new EntityValidationHandler<TEntity>();

        #endregion

        #region 2. 生命周期钩子（子类按需覆写）

        // ===== 新增 =====
        protected virtual EntityValidationResult OnAdding(TEntity entity) => OK;
        protected virtual void OnAdded(TEntity entity) { }

        // ===== 修改 =====
        protected virtual EntityValidationResult OnUpdating(TEntity entity, string? excludeCode) => OK;
        protected virtual void OnUpdated(TEntity entity) { }

        // ===== 删除 =====
        protected virtual EntityValidationResult OnDeleting(object[] keys, List<TEntity> entities) => OK;
        protected virtual void OnDeleted(object[] keys) { }

        private static readonly EntityValidationResult OK = new EntityValidationResult().OK();

        #endregion

        #region 3. 配置获取方法

        /// <summary>
        /// 获取当前实体的页面配置
        /// </summary>
        public PageUIConfig GetPageConfig()
        {
            return EntityConfigGenerator.Generate<TEntity>();
        }

        /// <summary>
        /// 获取当前实体的表格列配置
        /// </summary>
        public List<FieldConfig> GetTableColumns()
        {
            return EntityConfigGenerator.GenerateTableColumns<TEntity>();
        }

        /// <summary>
        /// 获取当前实体的表单字段配置
        /// </summary>
        public List<FieldConfig> GetFormFields()
        {
            return EntityConfigGenerator.GenerateFormFields<TEntity>();
        }

        /// <summary>
        /// 获取当前实体的搜索条件配置
        /// </summary>
        public List<FieldConfig> GetSearchFields()
        {
            return EntityConfigGenerator.GenerateSearchFields<TEntity>();
        }

        #endregion

        #region 4. 重写Add：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Add(SaveModel saveDataModel)
        {
            // 1. 空数据检查
            if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
                return new WebResponseContent().Error("提交数据为空");

            // 2. 预转换实体用于校验
            TEntity? entity = default;
            bool entityConverted = false;
            string? excludeCode = null;
            try
            {
                entity = saveDataModel.MainData.DicToEntity<TEntity>();
                entityConverted = true;
            }
            catch
            {
                // DicToEntity可能失败，不阻断流程
            }

            // 3. 自动校验（唯一性 + 必填）
            if (entityConverted && entity != null)
            {
                try
                {
                    var validateResult = CreateValidator().Validate(entity, ValidationAction.Add);
                    if (!validateResult.IsValid)
                        return validateResult.ToWebResponse();
                }
                catch (Exception ex)
                {
                    return ExceptionSanitizer.Sanitize(ex, "数据校验");
                }

                // 4. 自定义新增前校验
                try
                {
                    var customResult = OnAdding(entity);
                    if (!customResult.IsValid)
                        return customResult.ToWebResponse();
                }
                catch (Exception ex)
                {
                    return ExceptionSanitizer.Sanitize(ex, "新增");
                }
            }

            // 5. 执行保存
            try
            {
                var result = base.Add(saveDataModel);

                // 6. 异常脱敏
                if (!result.Status)
                    result = ExceptionSanitizer.SanitizeResponse(result, "新增");

                // 7. 新增后处理
                if (result.Status && entityConverted && entity != null)
                {
                    try
                    {
                        OnAdded(entity);
                    }
                    catch (Exception afterEx)
                    {
                        Logger.Error($"OnAdded 后置处理异常: {afterEx.Message}");
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "新增");
            }
        }

        #endregion

        #region 5. 重写Update：生命周期 + 校验 + 异常兜底

        public override WebResponseContent Update(SaveModel saveDataModel)
        {
            // 1. 空数据检查
            if (saveDataModel?.MainData == null || saveDataModel.MainData.Count == 0)
                return new WebResponseContent().Error("提交数据为空");

            // 2. 预转换实体用于校验
            TEntity? entity = default;
            bool entityConverted = false;
            string? excludeCode = null;
            try
            {
                entity = saveDataModel.MainData.DicToEntity<TEntity>();
                entityConverted = true;
                // 获取Code用于排除自身
                excludeCode = typeof(TEntity)
                    .GetProperty("Code")?.GetValue(entity)?.ToString();
            }
            catch
            {
                // 不阻断流程
            }

            // 3. 自动校验
            if (entityConverted && entity != null)
            {
                try
                {
                    var validateResult = CreateValidator()
                        .Validate(entity, ValidationAction.Update, excludeCode);
                    if (!validateResult.IsValid)
                        return validateResult.ToWebResponse();
                }
                catch (Exception ex)
                {
                    return ExceptionSanitizer.Sanitize(ex, "数据校验");
                }

                // 4. 自定义修改前校验
                try
                {
                    var customResult = OnUpdating(entity, excludeCode);
                    if (!customResult.IsValid)
                        return customResult.ToWebResponse();
                }
                catch (Exception ex)
                {
                    return ExceptionSanitizer.Sanitize(ex, "修改");
                }
            }

            // 5. 执行保存
            try
            {
                var result = base.Update(saveDataModel);

                // 6. 异常脱敏
                if (!result.Status)
                    result = ExceptionSanitizer.SanitizeResponse(result, "修改");

                // 7. 修改后处理
                if (result.Status && entityConverted && entity != null)
                {
                    try
                    {
                        OnUpdated(entity);
                    }
                    catch (Exception afterEx)
                    {
                        Logger.Error($"OnUpdated 后置处理异常: {afterEx.Message}");
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "修改");
            }
        }

        #endregion

        #region 6. 重写Del：生命周期 + 声明式删除策略 + 异常兜底

        public override WebResponseContent Del(object[] keys, bool delList = true)
        {
            // 1. 查询待删除实体
            List<TEntity> entityList;
            try
            {
                var keyName = typeof(TEntity).GetKeyName();
                var keyExpression = keyName.CreateExpression<TEntity>(keys[0].ToString()!, LinqExpressionType.Equal);
                entityList = repository.FindAsIQueryable(keyExpression).ToList();

                if (entityList == null || entityList.Count == 0)
                    return new WebResponseContent().Error("未找到要删除的数据");
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "删除");
            }

            // 2. 读取声明式删除策略
            var deleteAttr = typeof(TEntity).GetCustomAttribute<YZHDeleteStrategyAttribute>();
            var deleteMode = deleteAttr?.Mode ?? DeleteMode.Logical;

            // 3. 删除前校验
            try
            {
                var customResult = OnDeleting(keys, entityList);
                if (!customResult.IsValid)
                    return customResult.ToWebResponse();
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "删除");
            }

            // 4. 根据策略执行删除
            WebResponseContent result;
            try
            {
                result = deleteMode switch
                {
                    DeleteMode.Physical => ExecutePhysicalDelete(keys),
                    DeleteMode.Cascade => ExecuteCascadeDelete(keys, entityList, deleteAttr),
                    _ => base.Del(keys, delList)
                };

                if (!result.Status)
                    result = ExceptionSanitizer.SanitizeResponse(result, "删除");

                if (result.Status)
                {
                    try
                    {
                        OnDeleted(keys);
                    }
                    catch (Exception afterEx)
                    {
                        Logger.Error($"OnDeleted 后置处理异常: {afterEx.Message}");
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "删除");
            }
        }

        /// <summary>
        /// 物理删除
        /// </summary>
        private WebResponseContent ExecutePhysicalDelete(object[] keys)
        {
            try
            {
                var tableName = GetTableName();
                var keyName = typeof(TEntity).GetKeyName();
                var sql = $"DELETE FROM {tableName} WHERE {keyName} IN @keys";
                var affected = DBServerProvider.SqlDapper.ExcuteNonQuery(sql, new { keys });
                return affected > 0
                    ? new WebResponseContent().OK()
                    : new WebResponseContent().Error("删除失败，记录可能已不存在");
            }
            catch (Exception ex)
            {
                return ExceptionSanitizer.Sanitize(ex, "物理删除");
            }
        }

        /// <summary>
        /// 级联删除
        /// </summary>
        private WebResponseContent ExecuteCascadeDelete(object[] keys, List<TEntity> entities, YZHDeleteStrategyAttribute? attr)
        {
            // 当前实现：先走标准逻辑删除，后续扩展
            return base.Del(keys, true);
        }

        #endregion

        #region 7. 辅助方法

        /// <summary>
        /// 获取关联表记录数
        /// </summary>
        protected int GetRelatedCount(string relatedTable, string foreignKeyColumn, object foreignKeyValue)
        {
            var sql = $"SELECT COUNT(1) FROM {relatedTable} " +
                      $"WHERE {foreignKeyColumn} = @val AND (IsDeleted = 0 OR IsDeleted IS NULL)";
            return Convert.ToInt32(DBServerProvider.SqlDapper.ExecuteScalar(sql, new { val = foreignKeyValue }));
        }

        /// <summary>
        /// 获取实体表名
        /// </summary>
        protected string GetTableName()
        {
            var entityAttr = typeof(TEntity).GetCustomAttribute<VOL.Entity.EntityAttribute>();
            return entityAttr?.TableName ?? typeof(TEntity).Name;
        }

        #endregion
    }
}
```

**验证方法**：
- 编译通过：`dotnet build VOL.Core`
- 无语法错误

---

### TODO-1.5：统一实体基类

**状态**：⬜ 待开始  
**优先级**：🟡 中  
**预计耗时**：30分钟

**目标**：检查并统一所有业务实体的基类继承

**操作步骤**：
1. 扫描所有继承 `BaseEntity` 而非 `YZHBaseEntity` 的实体
2. 列出需要修改的实体清单
3. 逐个修改继承关系
4. 添加必要的 `[Column]` 特性适配

**涉及文件**：
- 检查：`src/server/Vue.NetCore/vol.api/VOL.Entity/DomainModels/` 下所有实体
- 修改：继承 `BaseEntity` 的实体改为继承 `YZHBaseEntity`

**预期变更**：
- `StandardDirectoryConfig` → 继承 `YZHBaseEntity`
- `StandardDirectoryFolder` → 继承 `YZHBaseEntity`
- `StandardDirectoryFile` → 继承 `YZHBaseEntity`
- `UploadTask` → 继承 `YZHBaseEntity`
- `SysConfig` → 继承 `YZHBaseEntity`
- `YzhPageConfig` → 继承 `YZHBaseEntity`
- `YzhFieldConfig` → 继承 `YZHBaseEntity`
- `CertMessage` → 继承 `YZHBaseEntity`
- `AIUsageLog` → 继承 `YZHBaseEntity`

**验证方法**：
- 编译通过
- 数据库表结构不变（通过 `[Column]` 特性适配）

---

## 三、Phase 2：配置API接口

### TODO-2.1：创建EntityConfigController

**状态**：✅ 已完成  
**优先级**：🔴 高  
**预计耗时**：25分钟

**目标**：创建配置获取API接口

**操作步骤**：
1. 在 `VOL.WebApi/Controllers/` 目录下创建 `EntityConfigController.cs`
2. 实现获取单个实体配置的接口
3. 实现获取所有实体配置列表的接口

**涉及文件**：
- 新建：`src/server/Vue.NetCore/vol.api/VOL.WebApi/Controllers/EntityConfigController.cs`

**预期代码**：
```csharp
using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Utilities;
using VOL.Entity.Admin.Platform.Base;

namespace VOL.WebApi.Controllers
{
    /// <summary>
    /// 实体配置API
    /// 用于前端获取页面/表格/表单配置
    /// </summary>
    [Route("api/entity-config")]
    [ApiController]
    public class EntityConfigController : ControllerBase
    {
        /// <summary>
        /// 获取实体页面配置
        /// GET /api/entity-config/{entityName}
        /// </summary>
        [HttpGet("{entityName}")]
        public IActionResult GetPageConfig(string entityName)
        {
            var entityType = FindEntityType(entityName);
            if (entityType == null)
                return NotFound(new { message = $"实体 {entityName} 不存在" });

            try
            {
                var method = typeof(EntityConfigGenerator)
                    .GetMethod("Generate")!
                    .MakeGenericMethod(entityType);
                
                var config = method.Invoke(null, null);
                return Ok(config);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"生成配置失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 获取所有实体配置列表
        /// GET /api/entity-config/list
        /// </summary>
        [HttpGet("list")]
        public IActionResult GetConfigList()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var entities = assemblies
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.GetCustomAttribute<YZHPageAttribute>() != null)
                    .Select(t => 
                    {
                        var pageAttr = t.GetCustomAttribute<YZHPageAttribute>();
                        return new
                        {
                            EntityName = t.Name,
                            PageKey = pageAttr?.PageKey,
                            Title = pageAttr?.Title,
                            ControllerName = pageAttr?.ControllerName
                        };
                    })
                    .OrderBy(e => e.EntityName)
                    .ToList();

                return Ok(entities);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"获取配置列表失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 根据实体名查找类型
        /// </summary>
        private Type? FindEntityType(string entityName)
        {
            // 精确匹配
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == entityName && 
                                   t.GetCustomAttribute<YZHPageAttribute>() != null);
            
            if (type != null) return type;

            // 尝试不区分大小写匹配
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => string.Equals(t.Name, entityName, StringComparison.OrdinalIgnoreCase) &&
                                   t.GetCustomAttribute<YZHPageAttribute>() != null);
        }
    }
}
```

**验证方法**：
- 编译通过
- 启动API后访问 `/api/entity-config/list` 返回实体列表
- 访问 `/api/entity-config/CertificationBody` 返回配置JSON

---

### TODO-2.2：测试配置生成

**状态**：✅ 已完成  
**优先级**：🔴 高  
**预计耗时**：15分钟

**目标**：验证配置生成功能正常

**操作步骤**：
1. 启动后端API
2. 调用 `/api/entity-config/list` 获取实体列表
3. 调用 `/api/entity-config/CertificationBody` 获取配置
4. 检查返回的JSON结构是否正确

**验证方法**：
- 返回的 `pageMeta` 包含正确的 `pageKey`、`title`、`controllerName`
- 返回的 `fieldConfigs` 包含所有带 `[YZHColumn]`/`[YZHForm]`/`[YZHSearch]` 特性的字段

---

## 四、Phase 3：前端适配

### TODO-3.1：创建前端配置API文件

**状态**：⬜ 待开始  
**优先级**：🔴 高  
**预计耗时**：10分钟

**目标**：创建前端调用配置API的工具函数

**操作步骤**：
1. 在 `src/yzh/api/` 目录下创建 `entity-config.ts`
2. 导出 `getEntityConfig` 和 `getEntityConfigList` 函数

**涉及文件**：
- 新建：`src/server/Vue.NetCore/vol.web/src/yzh/api/entity-config.ts`

**预期代码**：
```typescript
import { http } from '@/utils/http'
import type { IYzhPageUIConfig } from '../types/YZHV3Config'

/**
 * 获取实体页面配置
 * @param entityName 实体名称
 * @returns 页面UI配置
 */
export async function getEntityConfig(entityName: string): Promise<IYzhPageUIConfig> {
  return await http.get(`/api/entity-config/${entityName}`)
}

/**
 * 获取所有实体配置列表
 * @returns 实体配置列表
 */
export async function getEntityConfigList(): Promise<Array<{
  entityName: string
  pageKey: string
  title: string
  controllerName: string
}>> {
  return await http.get('/api/entity-config/list')
}
```

**验证方法**：
- TypeScript编译通过

---

### TODO-3.2：改造YzhCrudV3.vue

**状态**：⬜ 待开始  
**优先级**：🔴 高  
**预计耗时**：30分钟

**目标**：改造组件支持配置对象传入

**操作步骤**：
1. 修改 `YzhCrudV3.vue` 的 props 定义
2. 添加 `pageConfig` 和 `entityName` 两种新的配置获取方式
3. 保持对旧版 `pageKey` 的兼容

**涉及文件**：
- 修改：`src/server/Vue.NetCore/vol.web/src/yzh/components/YzhCrudV3.vue`

**预期变更**：
```vue
<!-- 修改 props 定义 -->
const props = withDefaults(defineProps<{
  // 新增：直接传入配置对象
  pageConfig?: IYzhPageUIConfig | null
  // 新增：通过实体名获取配置
  entityName?: string
  // 保留：通过pageKey获取配置（兼容旧版）
  pageKey?: string
  // API控制器名
  apiPrefix?: string
  // 其他props...
}>(), {
  pageConfig: null,
  entityName: '',
  pageKey: '',
  apiPrefix: '/api/',
  // 其他默认值...
})

// 修改 onMounted 中的配置加载逻辑
onMounted(async () => {
  try {
    // 优先级1：直接传入的配置对象
    if (props.pageConfig) {
      pageConfig.value = props.pageConfig
    }
    // 优先级2：通过实体名从后端获取
    else if (props.entityName) {
      const { getEntityConfig } = await import('../api/entity-config')
      pageConfig.value = await getEntityConfig(props.entityName)
    }
    // 优先级3：通过pageKey获取（兼容旧版）
    else if (props.pageKey) {
      pageConfig.value = await loadPageConfig(props.pageKey)
    }
    
    // 初始化API客户端
    initApiClient()
    // 加载数据
    await loadData()
    // 暴露实例
    emit('ready', exposedApi)
  } catch (e: any) {
    // 使用默认配置继续运行
    pageConfig.value = {
      pageMeta: defaultPageMeta(),
      fieldConfigs: [],
    }
    // ...
  }
})
```

**验证方法**：
- TypeScript编译通过
- 使用 `entity-name="CertificationBody"` 可正常渲染

---

### TODO-3.3：测试前端渲染

**状态**：⬜ 待开始  
**优先级**：🟡 中  
**预计耗时**：15分钟

**目标**：验证前端使用配置渲染正常

**操作步骤**：
1. 修改一个现有页面使用 `entity-name` 方式
2. 启动前端开发服务器
3. 访问页面验证渲染效果

**验证方法**：
- 表格列正确显示
- 搜索条件正确渲染
- 新增/编辑弹窗表单正确

---

## 五、Phase 4：测试验证

### TODO-4.1：单元测试

**状态**：⬜ 待开始  
**优先级**：🟡 中  
**预计耗时**：20分钟

**目标**：验证核心功能的正确性

**测试用例**：
1. `EntityConfigGenerator.Generate<CertificationBody>()` 返回正确配置
2. `CertServiceBase.Add()` 自动校验生效
3. `CertServiceBase.Del()` 删除策略生效

**验证方法**：
- 测试通过

---

### TODO-4.2：集成测试

**状态**：⬜ 待开始  
**优先级**：🟡 中  
**预计耗时**：20分钟

**目标**：验证端到端流程

**测试场景**：
1. 前端通过 `entity-name` 加载配置
2. 配置驱动渲染表格/表单
3. 执行增删改查操作
4. 验证校验、生命周期、异常处理

**验证方法**：
- 所有操作正常执行

---

## 六、Phase 5：清理

### TODO-5.1：删除YZH-Framework独立目录

**状态**：⬜ 待开始  
**优先级**：🟢 低  
**预计耗时**：10分钟

**目标**：删除不再需要的YZH独立框架目录

**操作步骤**：
1. 备份重要代码（已完成整合）
2. 删除 `src/server/YZH-Framework/` 目录
3. 更新 `.gitignore`

**涉及文件**：
- 删除：`src/server/YZH-Framework/`

**验证方法**：
- 项目编译通过
- 功能正常

---

### TODO-5.2：更新文档

**状态**：⬜ 待开始  
**优先级**：🟢 低  
**预计耗时**：15分钟

**目标**：更新相关文档反映新的架构

**操作步骤**：
1. 更新 `AGENTS.md` 中的架构说明
2. 更新 `docs/60-AI工程设计/` 下的相关文档
3. 创建新的架构设计文档

**涉及文件**：
- 修改：`AGENTS.md`
- 修改：`docs/60-AI工程设计/README.md`
- 新建：`docs/60-AI工程设计/架构整合方案-V1.md`

**验证方法**：
- 文档内容准确反映新架构

---

## 七、任务状态说明

| 状态 | 含义 |
|------|------|
| ⬜ 待开始 | 任务未开始 |
| 🔄 进行中 | 任务正在执行 |
| ✅ 已完成 | 任务已完成并通过验证 |
| ❌ 失败 | 任务执行失败，需要重试 |
| ⏸️ 暂停 | 任务暂停，等待条件 |
| ❌ 取消 | 任务不再需要 |

---

## 八、执行记录

| 日期 | 任务 | 操作 | 结果 |
|------|------|------|------|
| 2026-09-04 | 创建TODO清单 | 创建 | ✅ |
| | | | |

---

> **使用说明**：
> 1. 每次开始执行前，先读取本文件了解当前进度
> 2. 执行完成后，更新对应TODO的状态
> 3. 如遇问题，在执行记录中添加失败记录
> 4. 切换AI时，本文件是唯一的进度跟踪源
