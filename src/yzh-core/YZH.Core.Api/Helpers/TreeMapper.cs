using System.Reflection;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Api.Helpers;

/// <summary>
///     树节点映射工具（通用，不限于特定 Controller 继承链）
///     
///     使用场景：
///     1. TreeTableControllerBase / TreeControllerBase 的 MapToTreeItem
///     2. 非树控制器但需要构建树形结构（如复杂表单中的树选择器）
///     3. 任何需要将实体映射为 TreeItemDto 的场景
/// </summary>
public static class TreeMapper
{
    /// <summary>将实体映射为 TreeItemDto（最简版，使用默认字段名）</summary>
    public static TreeItemDto MapToTreeItem<T>(T entity, int level) where T : class
    {
        return MapToTreeItem(entity, level, new TreeFieldMapping());
    }

    /// <summary>将实体映射为 TreeItemDto（支持自定义字段映射）</summary>
    public static TreeItemDto MapToTreeItem<T>(T entity, int level, TreeFieldMapping? mapping) where T : class
    {
        mapping ??= new TreeFieldMapping();

        var code = GetFieldValue(entity, mapping.CodeField)
            ?? GetFieldValue(entity, "Code")
            ?? string.Empty;

        var name = GetFieldValue(entity, mapping.NameField)
            ?? GetFieldValue(entity, "Name")
            ?? code;

        var parentCode = GetFieldValue(entity, mapping.ParentCodeField)
            ?? GetFieldValue(entity, "ParentCode");

        var dto = new TreeItemDto
        {
            Code = code,
            Name = name,
            ParentCode = parentCode,
            NodeType = mapping.NodeType,
            IsLeaf = GetBoolFieldValue(entity, mapping.IsLeafField) ?? false,
            Level = level,
            Sort = GetIntFieldValue(entity, mapping.SortField),
            Extra = new Dictionary<string, object>()
        };

        // 自动提取常见扩展字段
        TryFillExtra(dto.Extra, entity, "Enable", "enable");
        TryFillExtra(dto.Extra, entity, "Remark", "remark");
        TryFillExtra(dto.Extra, entity, "Status", "status");
        TryFillExtra(dto.Extra, entity, "OrderNo", "orderNo");
        TryFillExtra(dto.Extra, entity, "Sort", "sort");
        TryFillExtra(dto.Extra, entity, "Level", "level");
        TryFillExtra(dto.Extra, entity, "OrgType", "orgType");
        TryFillExtra(dto.Extra, entity, "LeaderName", "leaderName");
        TryFillExtra(dto.Extra, entity, "LeaderPhone", "leaderPhone");

        return dto;
    }

    /// <summary>批量映射</summary>
    public static List<TreeItemDto> MapToTreeItems<T>(IEnumerable<T> entities, int level) where T : class
    {
        return entities.Select(e => MapToTreeItem(e, level)).ToList();
    }

    /// <summary>批量映射（支持自定义字段映射）</summary>
    public static List<TreeItemDto> MapToTreeItems<T>(IEnumerable<T> entities, int level, TreeFieldMapping? mapping) where T : class
    {
        return entities.Select(e => MapToTreeItem(e, level, mapping)).ToList();
    }

    // ========================================================
    // 私有辅助方法
    // ========================================================

    private static string? GetFieldValue<T>(T entity, string fieldName) where T : class
    {
        if (string.IsNullOrEmpty(fieldName)) return null;

        var prop = typeof(T).GetProperty(fieldName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (prop == null) return null;

        var value = prop.GetValue(entity);
        return value?.ToString();
    }

    private static bool? GetBoolFieldValue<T>(T entity, string fieldName) where T : class
    {
        if (string.IsNullOrEmpty(fieldName)) return null;

        var prop = typeof(T).GetProperty(fieldName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (prop == null) return null;

        var value = prop.GetValue(entity);
        if (value is bool b) return b;
        if (value is sbyte sb) return sb != 0;
        if (value is int i) return i != 0;
        if (value is long l) return l != 0;
        if (value is string s) return s == "1" || s.ToLower() == "true";

        return null;
    }

    private static int? GetIntFieldValue<T>(T entity, string fieldName) where T : class
    {
        if (string.IsNullOrEmpty(fieldName)) return null;

        var prop = typeof(T).GetProperty(fieldName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (prop == null) return null;

        var value = prop.GetValue(entity);
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is sbyte sb) return sb;
        if (value is decimal d) return (int)d;
        if (value is string s && int.TryParse(s, out var parsed)) return parsed;

        return null;
    }

    private static void TryFillExtra<T>(Dictionary<string, object> extra, T entity, string propertyName, string extraKey) where T : class
    {
        var prop = typeof(T).GetProperty(propertyName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (prop != null)
        {
            var value = prop.GetValue(entity);
            if (value != null)
            {
                extra[extraKey] = value;
            }
        }
    }
}

/// <summary>
///     树字段映射配置
/// </summary>
public class TreeFieldMapping
{
    /// <summary>Code 字段名（默认 "Code"）</summary>
    public string CodeField { get; set; } = "Code";

    /// <summary>Name 字段名（默认 "Name"）</summary>
    public string NameField { get; set; } = "Name";

    /// <summary>ParentCode 字段名（默认 "ParentCode"）</summary>
    public string ParentCodeField { get; set; } = "ParentCode";

    /// <summary>IsLeaf 字段名（默认 "IsLeaf"）</summary>
    public string IsLeafField { get; set; } = "IsLeaf";

    /// <summary>Sort 字段名（默认 "Sort"）</summary>
    public string SortField { get; set; } = "Sort";

    /// <summary>节点类型标识（可选）</summary>
    public string? NodeType { get; set; }

    /// <summary>从 TreeConfig 创建映射</summary>
    public static TreeFieldMapping FromTreeConfig(TreeConfig config)
    {
        return new TreeFieldMapping
        {
            CodeField = config.CodeField,
            NameField = config.NameField,
            ParentCodeField = config.ParentCodeField,
            NodeType = null
        };
    }
}
