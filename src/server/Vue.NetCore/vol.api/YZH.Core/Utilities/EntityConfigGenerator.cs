using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using YZH.Entity.Admin.Platform.Base;

namespace YZH.Core.Utilities
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
            var pageAttr = entityType.GetCustomAttribute<PageAttribute>();

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
        private static PageMeta GeneratePageMeta(Type entityType, PageAttribute pageAttr)
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
                var columnAttr = prop.GetCustomAttribute<EntityColumnAttribute>();
                var formAttr = prop.GetCustomAttribute<FormAttribute>();
                var searchAttr = prop.GetCustomAttribute<SearchAttribute>();

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
                    BcFlag = formAttr?.Visible ?? true, // Visible=false时BcFlag=false
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
            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = columnAttr?.Name ?? prop.Name;
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
