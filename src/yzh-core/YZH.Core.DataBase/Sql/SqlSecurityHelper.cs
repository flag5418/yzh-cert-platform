using System.Text.RegularExpressions;

namespace YZH.Core.DataBase.Sql;

/// <summary>
///     SQL 片段安全校验器
///     防止 SQL 注入攻击
/// </summary>
public static class SqlSecurityHelper
{
    /// <summary>
    ///     验证 SQL 标识符（表名、字段名）
    ///     只允许：字母、数字、下划线、点（用于 schema.table）
    /// </summary>
    public static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        return Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.]*$");
    }

    /// <summary>
    ///     验证 SQL 片段（JOIN/GROUP BY/HAVING/ORDER BY 等）
    ///     只允许：字母、数字、空格、逗号、点、下划线、括号
    /// </summary>
    public static bool IsValidSqlFragment(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return true;

        return Regex.IsMatch(fragment, @"^[a-zA-Z0-9\s,_.\(\)\+\-\*\/<>=!]+$");
    }

    /// <summary>
    ///     安全转义 SQL 标识符（MySQL 风格）
    /// </summary>
    public static string EscapeIdentifier(string identifier)
    {
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException($"非法的 SQL 标识符: {identifier}");

        return $"`{identifier}`";
    }

    /// <summary>
    ///     验证并清理 SQL 片段
    /// </summary>
    public static string ValidateSqlFragment(string fragment, string name)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return fragment;

        if (!IsValidSqlFragment(fragment))
            throw new ArgumentException($"非法的 {name} 参数: {fragment}");

        return fragment;
    }
}
