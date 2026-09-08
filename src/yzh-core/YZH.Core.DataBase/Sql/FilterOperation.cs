using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase.Sql;

/// <summary>
///     过滤操作解析器
///     将前端传入的抽象 FilterItem 安全地转换为后端 SqlCondition
///     
///     核心职责：
///     1. 操作符映射：eq → =、neq → !=、gt → >、gte → >=、lt → <、lte → <=、like → LIKE、in → IN
///     2. 值处理：like 自动添加 % 通配符、in 拆分为数组
///     3. 安全防护：字段名白名单校验、SQL 注入防护
///     
///     安全策略：
///     - 字段名必须通过 SqlSecurityHelper.IsValidIdentifier 校验
///     - 值通过 Dapper 参数化传递，不直接拼接
///     - 不支持自定义 SQL 片段
/// </summary>
public static class FilterOperation
{
    /// <summary>支持的抽象操作符集合</summary>
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "neq", "gt", "gte", "lt", "lte", "like", "in", "isnull", "isnotnull"
    };

    /// <summary>
    ///     将 FilterItem 转换为 SqlCondition
    /// </summary>
    /// <param name="item">前端过滤项</param>
    /// <returns>SQL 条件</returns>
    /// <exception cref="ArgumentException">当字段名非法或操作符不支持时抛出</exception>
    public static SqlCondition Parse(FilterItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (string.IsNullOrWhiteSpace(item.Field))
            throw new ArgumentException("FilterItem.Field 不能为空");

        // 安全校验：字段名必须是合法标识符
        if (!SqlSecurityHelper.IsValidIdentifier(item.Field))
            throw new ArgumentException($"非法的字段名: {item.Field}");

        var op = item.Operator?.ToLowerInvariant() ?? "eq";

        if (!SupportedOperators.Contains(op))
            throw new ArgumentException($"不支持的操作符: {item.Operator}。支持的操作符: {string.Join(", ", SupportedOperators)}");

        return op switch
        {
            "eq" => new SqlCondition(item.Field, "=", item.Value),
            "neq" => new SqlCondition(item.Field, "!=", item.Value),
            "gt" => new SqlCondition(item.Field, ">", item.Value),
            "gte" => new SqlCondition(item.Field, ">=", item.Value),
            "lt" => new SqlCondition(item.Field, "<", item.Value),
            "lte" => new SqlCondition(item.Field, "<=", item.Value),
            "like" => ParseLike(item),
            "in" => ParseIn(item),
            "isnull" => new SqlCondition(item.Field, "IS NULL", null),
            "isnotnull" => new SqlCondition(item.Field, "IS NOT NULL", null),
            _ => throw new ArgumentException($"未处理的操作符: {op}")
        };
    }

    /// <summary>
    ///     批量转换 FilterItem 列表为 SqlCondition 列表
    /// </summary>
    public static List<SqlCondition> ParseList(IEnumerable<FilterItem>? items)
    {
        if (items == null)
            return new List<SqlCondition>();

        var result = new List<SqlCondition>();
        foreach (var item in items)
        {
            // 跳过空值（eq/neq 操作符时）
            if (item.Value == null && 
                (item.Operator?.Equals("eq", StringComparison.OrdinalIgnoreCase) == true ||
                 item.Operator?.Equals("neq", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            result.Add(Parse(item));
        }
        return result;
    }

    /// <summary>
    ///     解析 LIKE 操作符
    ///     自动在值前后添加 % 通配符（如果未包含）
    /// </summary>
    private static SqlCondition ParseLike(FilterItem item)
    {
        var value = item.Value?.ToString() ?? string.Empty;

        // 如果值不包含通配符，自动添加前后 %
        if (!value.Contains('%') && !value.Contains('_'))
        {
            value = $"%{value}%";
        }

        return new SqlCondition(item.Field, "LIKE", value);
    }

    /// <summary>
    ///     解析 IN 操作符
    ///     支持字符串（逗号分隔）、数组、List 等格式
    /// </summary>
    private static SqlCondition ParseIn(FilterItem item)
    {
        var value = item.Value;

        if (value == null)
            return new SqlCondition(item.Field, "IN", new List<object>());

        // 如果已经是 IEnumerable（非字符串），直接使用
        if (value is IEnumerable<object> objEnumerable && value is not string)
            return new SqlCondition(item.Field, "IN", objEnumerable.ToList());

        if (value is IEnumerable<string> strEnumerable)
            return new SqlCondition(item.Field, "IN", strEnumerable.Cast<object>().ToList());

        // 字符串类型：按逗号分隔
        var str = value.ToString() ?? string.Empty;
        var parts = str.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .Cast<object>()
                       .ToList();

        return new SqlCondition(item.Field, "IN", parts);
    }
}
