using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using YZH.Core.Stand.Models;

namespace YZH.Core.Stand.Helpers;

/// <summary>
///     实体结构反射助手
///     
///     职责：
///     通过反射扫描实体类型，自动生成"空实体模板"字典
///     前端直接用这个字典初始化表单/提交数据，不再手写 TS interface
///     
///     反射策略：
///     - 包含所有 public instance 属性
///     - 排除标记了 [NotMapped] 的属性（非持久化字段）
///     - 排除标记了 [JsonIgnore] 的属性（密码等敏感字段）
///     - 排除 CheckFlag/DeleteFlag 等纯前端状态字段
///     
///     输出格式（camelCase 字段名）：
///     {
///       "code": { "type": "string", "default": "", "optional": false },
///       "orgName": { "type": "string", "default": "", "optional": false },
///       "enable": { "type": "number", "default": 1, "optional": false }
///     }
///     
///     V1 实现：支持所有基元类型 + 可空类型 + 枚举 + Guid + DateTime
/// </summary>
public static class EntitySchemaHelper
{
    // 排除的属性名列表（纯前端状态字段）
    private static readonly HashSet<string> ExcludedProps = new(StringComparer.OrdinalIgnoreCase)
    {
        "CheckFlag",
        "DeleteFlag",
        "RowVersion"
    };

    /// <summary>
    ///     获取实体的字段结构字典（前端用）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>camelCase 字段名 → 字段描述</returns>
    public static Dictionary<string, EntityFieldSchema> GetSchema<T>() where T : class
    {
        return GetSchema(typeof(T));
    }

    /// <summary>
    ///     获取实体的字段结构字典（前端用）
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>camelCase 字段名 → 字段描述</returns>
    public static Dictionary<string, EntityFieldSchema> GetSchema(Type entityType)
    {
        var schema = new Dictionary<string, EntityFieldSchema>();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // 排除标记 [NotMapped] 和 [JsonIgnore] 的属性
            if (prop.GetCustomAttribute<NotMappedAttribute>() != null) continue;
            if (prop.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null) continue;

            // 排除纯前端状态字段
            if (ExcludedProps.Contains(prop.Name)) continue;

            var camelName = ToCamelCase(prop.Name);
            var (clrType, optional) = UnwrapNullable(prop.PropertyType);

            schema[camelName] = new EntityFieldSchema
            {
                Type = ClrTypeToSchemaType(clrType),
                Default = GetDefaultValue(clrType, prop),
                Optional = optional
            };
        }

        return schema;
    }

    /// <summary>
    ///     获取实体的空实体模板（直接用于前端初始化表单数据）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>camelCase 字段名 → 默认值</returns>
    public static Dictionary<string, object?> GetEmptyEntity<T>() where T : class
    {
        return GetEmptyEntity(typeof(T));
    }

    /// <summary>
    ///     获取实体的空实体模板
    /// </summary>
    public static Dictionary<string, object?> GetEmptyEntity(Type entityType)
    {
        var schema = GetSchema(entityType);
        return schema.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Default
        );
    }

    // ═════════════════════════════════════════════
    // 私有辅助
    // ═════════════════════════════════════════════

    /// <summary>解包可空类型，返回 (底层类型, 是否可选)</summary>
    private static (Type type, bool optional) UnwrapNullable(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return (underlying, true);
        return (type, false);
    }

    /// <summary>CLR 类型 → Schema 类型字符串</summary>
    private static string ClrTypeToSchemaType(Type type)
    {
        var typeCode = Type.GetTypeCode(type);
        return typeCode switch
        {
            TypeCode.String or TypeCode.Char => "string",
            TypeCode.Boolean => "boolean",
            TypeCode.Byte or TypeCode.SByte => "number",
            TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => "number",
            TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => "number",
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => "number",
            _ => type == typeof(Guid) || type == typeof(Guid?)
                ? "string"
                : type == typeof(DateTime) || type == typeof(DateTime?)
                ? "datetime"
                : type.IsEnum
                ? "number"
                : "string"
        };
    }

    /// <summary>获取属性的默认值</summary>
    private static object? GetDefaultValue(Type type, PropertyInfo prop)
    {
        // 通过创建实例获取属性默认值
        try
        {
            if (Activator.CreateInstance(prop.DeclaringType!) is { } instance)
            {
                return prop.GetValue(instance);
            }
        }
        catch
        {
            // 无法创建实例，返回类型默认值
        }

        // 兜底：类型默认值
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        return null;
    }

    /// <summary>PascalCase → camelCase</summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
