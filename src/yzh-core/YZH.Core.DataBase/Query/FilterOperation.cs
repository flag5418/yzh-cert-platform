using System.Text.Json;
using YZH.Core.Stand.Models;
using YZH.Core.DataBase.Models;

namespace YZH.Core.DataBase.Query;

/// <summary>
///     过滤器操作辅助类
///     将前端传入的 FilterItem 转换为安全的 SqlCondition
/// </summary>
public static class FilterOperation
{
    /// <summary>
    ///     解析过滤器列表，将 FilterItem 转换为 SqlCondition
    /// </summary>
    public static List<SqlCondition> ParseList(List<FilterItem>? filters)
    {
        var result = new List<SqlCondition>();
        if (filters == null) return result;

        foreach (var filter in filters)
        {
            var condition = Parse(filter);
            if (condition != null)
                result.Add(condition);
        }
        return result;
    }

    /// <summary>
    ///     解析单个 FilterItem 为 SqlCondition
    /// </summary>
    public static SqlCondition? Parse(FilterItem? filter)
    {
        if (filter == null || string.IsNullOrWhiteSpace(filter.Field))
            return null;

        // 字段名安全检查：只允许字母、数字、下划线
        if (!System.Text.RegularExpressions.Regex.IsMatch(filter.Field, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"非法字段名: {filter.Field}");

        // 将 JsonElement 转换为实际 .NET 类型（JSON 反序列化后 object 可能是 JsonElement）
        var value = NormalizeJsonValue(filter.Value);

        return filter.Operator?.ToLower() switch
        {
            "eq" or "=" or "equal" => new SqlCondition(filter.Field, "=", value),
            "neq" or "!=" or "notEqual" => new SqlCondition(filter.Field, "!=", value),
            "gt" or ">" => new SqlCondition(filter.Field, ">", value),
            "gte" or ">=" => new SqlCondition(filter.Field, ">=", value),
            "lt" or "<" => new SqlCondition(filter.Field, "<", value),
            "lte" or "<=" => new SqlCondition(filter.Field, "<=", value),
            "contains" or "like" => new SqlCondition(filter.Field, "LIKE", $"%{value}%"),
            "startswith" => new SqlCondition(filter.Field, "LIKE", $"{value}%"),
            "endswith" => new SqlCondition(filter.Field, "LIKE", $"%{value}"),
            "in" => new SqlCondition(filter.Field, "IN", value),
            _ => new SqlCondition(filter.Field, "=", value)
        };
    }

    /// <summary>
    ///     将 JsonElement 转换为实际 .NET 类型
    ///     JSON 反序列化为 object 时，数字会变成 JsonElement，需转换
    /// </summary>
    private static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.TryGetInt64(out var l) ? l : jsonElement.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => jsonElement.GetRawText()
            };
        }
        return value;
    }
}
