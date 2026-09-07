using System.Text;

namespace YZH.Core.DataBase.Sql;

/// <summary>
///     MySQL 方言实现（8.0+）
/// </summary>
public class MySqlDialect : IDatabaseDialect
{
    public DatabaseType DbType => DatabaseType.MySql;
    public string ParameterPrefix => "@";
    public (string Open, string Close) IdentifierQuotes => ("`", "`");
    public string StringQuote => "'";
    public string NowFunction => "NOW()";

    public string BuildPaginationSql(string baseSql, int offset, int limit)
    {
        // MySQL: LIMIT limit OFFSET offset
        return $"{baseSql} LIMIT {limit} OFFSET {offset}";
    }

    public string BuildCountSql(string baseSql)
    {
        // 提取 FROM 之后的内容，生成分页查询
        var fromIndex = baseSql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (fromIndex < 0) return $"SELECT COUNT(*) FROM ({baseSql}) AS __count";

        return $"SELECT COUNT(*) {baseSql[fromIndex..]}";
    }

    public string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空", nameof(identifier));

        // 防止注入：只允许字母数字下划线和点号
        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.]*$"))
            throw new ArgumentException($"非法标识符: {identifier}", nameof(identifier));

        return $"`{identifier}`";
    }

    public string EscapeString(string value)
    {
        // MySQL 转义：' " \ NUL 等
        return value.Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\0", "\\0");
    }

    public string ConcatFunction(IEnumerable<string> expressions)
    {
        return $"CONCAT({string.Join(", ", expressions)})";
    }

    public string DateFormatFunction(string column, string format)
    {
        // MySQL 日期格式：%Y-%m-%d %H:%i:%s
        return $"DATE_FORMAT({EscapeIdentifier(column)}, '{format}')";
    }

    public string ContainsFunction(string column, string value)
    {
        return $"{EscapeIdentifier(column)} LIKE CONCAT('%', {ParameterPrefix}value, '%')";
    }

    public string BuildTopSql(string baseSql, int topN)
    {
        // MySQL: 在末尾加 LIMIT
        // 注意：如果原 SQL 已有限定，需要特殊处理
        var trimmedSql = baseSql.TrimEnd(';');
        return $"{trimmedSql} LIMIT {topN}";
    }
}

/// <summary>
///     SQL Server 方言实现（2012+）
/// </summary>
public class SqlServerDialect : IDatabaseDialect
{
    public DatabaseType DbType => DatabaseType.SqlServer;
    public string ParameterPrefix => "@";
    public (string Open, string Close) IdentifierQuotes => ("[", "]");
    public string StringQuote => "'";
    public string NowFunction => "GETDATE()";

    public string BuildPaginationSql(string baseSql, int offset, int limit)
    {
        // SQL Server 2012+: OFFSET offset ROWS FETCH NEXT limit ROWS ONLY
        // 需要 ORDER BY 支持，因此在外部调用时确保已添加 ORDER BY
        return $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";
    }

    public string BuildCountSql(string baseSql)
    {
        var fromIndex = baseSql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (fromIndex < 0) return $"SELECT COUNT(*) FROM ({baseSql}) AS __count";

        return $"SELECT COUNT(*) {baseSql[fromIndex..]}";
    }

    public string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空", nameof(identifier));

        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.]*$"))
            throw new ArgumentException($"非法标识符: {identifier}", nameof(identifier));

        return $"[{identifier}]";
    }

    public string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    public string ConcatFunction(IEnumerable<string> expressions)
    {
        return $"CONCAT({string.Join(", ", expressions)})";
    }

    public string DateFormatFunction(string column, string format)
    {
        // SQL Server: FORMAT(column, 'yyyy-MM-dd HH:mm:ss')
        return $"FORMAT({EscapeIdentifier(column)}, '{ConvertToSqlServerFormat(format)}')";
    }

    public string ContainsFunction(string column, string value)
    {
        return $"{EscapeIdentifier(column)} LIKE '%' + {ParameterPrefix}value + '%'";
    }

    public string BuildTopSql(string baseSql, int topN)
    {
        // SQL Server: SELECT TOP N ... (在 SELECT 后插入)
        if (baseSql.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
            return baseSql.Replace("SELECT ", $"SELECT TOP {topN} ", StringComparison.OrdinalIgnoreCase);

        return baseSql;
    }

    private static string ConvertToSqlServerFormat(string mysqlFormat)
    {
        return mysqlFormat.Replace("%Y", "yyyy")
                         .Replace("%m", "MM")
                         .Replace("%d", "dd")
                         .Replace("%H", "HH")
                         .Replace("%i", "mm")
                         .Replace("%s", "ss");
    }
}

/// <summary>
///     PostgreSQL 方言实现（10+）
/// </summary>
public class PostgreSqlDialect : IDatabaseDialect
{
    public DatabaseType DbType => DatabaseType.PostgreSQL;
    public string ParameterPrefix => "@";
    public (string Open, string Close) IdentifierQuotes => ("\"", "\"");
    public string StringQuote => "'";
    public string NowFunction => "NOW()";

    public string BuildPaginationSql(string baseSql, int offset, int limit)
    {
        // PostgreSQL: LIMIT limit OFFSET offset
        return $"{baseSql} LIMIT {limit} OFFSET {offset}";
    }

    public string BuildCountSql(string baseSql)
    {
        var fromIndex = baseSql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (fromIndex < 0) return $"SELECT COUNT(*) FROM ({baseSql}) AS __count";

        return $"SELECT COUNT(*) {baseSql[fromIndex..]}";
    }

    public string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空", nameof(identifier));

        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.]*$"))
            throw new ArgumentException($"非法标识符: {identifier}", nameof(identifier));

        return $"\"{identifier}\"";
    }

    public string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    public string ConcatFunction(IEnumerable<string> expressions)
    {
        return $"CONCAT({string.Join(", ", expressions)})";
    }

    public string DateFormatFunction(string column, string format)
    {
        return $"TO_CHAR({EscapeIdentifier(column)}, '{ConvertToPgFormat(format)}')";
    }

    public string ContainsFunction(string column, string value)
    {
        return $"{EscapeIdentifier(column)} LIKE '%' || {ParameterPrefix}value || '%'";
    }

    public string BuildTopSql(string baseSql, int topN)
    {
        return $"{baseSql.TrimEnd(';')} LIMIT {topN}";
    }

    private static string ConvertToPgFormat(string mysqlFormat)
    {
        return mysqlFormat.Replace("%Y", "YYYY")
                         .Replace("%m", "MM")
                         .Replace("%d", "DD")
                         .Replace("%H", "HH24")
                         .Replace("%i", "MI")
                         .Replace("%s", "SS");
    }
}

/// <summary>
///     Oracle 方言实现（12c+）
/// </summary>
public class OracleDialect : IDatabaseDialect
{
    public DatabaseType DbType => DatabaseType.Oracle;
    public string ParameterPrefix => ":";
    public (string Open, string Close) IdentifierQuotes => ("\"", "\"");
    public string StringQuote => "'";
    public string NowFunction => "SYSDATE";

    public string BuildPaginationSql(string baseSql, int offset, int limit)
    {
        // Oracle 12c+: OFFSET offset ROWS FETCH NEXT limit ROWS ONLY
        return $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";
    }

    public string BuildCountSql(string baseSql)
    {
        var fromIndex = baseSql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (fromIndex < 0) return $"SELECT COUNT(*) FROM ({baseSql}) AS __count";

        return $"SELECT COUNT(*) {baseSql[fromIndex..]}";
    }

    public string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空", nameof(identifier));

        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.$]*$"))
            throw new ArgumentException($"非法标识符: {identifier}", nameof(identifier));

        return $"\"{identifier}\"";
    }

    public string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    public string ConcatFunction(IEnumerable<string> expressions)
    {
        // Oracle: expr1 || expr2
        return string.Join(" || ", expressions);
    }

    public string DateFormatFunction(string column, string format)
    {
        return $"TO_CHAR({EscapeIdentifier(column)}, '{ConvertToOracleFormat(format)}')";
    }

    public string ContainsFunction(string column, string value)
    {
        return $"{EscapeIdentifier(column)} LIKE '%' || {ParameterPrefix}value || '%'";
    }

    public string BuildTopSql(string baseSql, int topN)
    {
        // Oracle: 使用 FETCH FIRST n ROWS ONLY (12c+)
        return $"{baseSql.TrimEnd(';')} FETCH FIRST {topN} ROWS ONLY";
    }

    private static string ConvertToOracleFormat(string mysqlFormat)
    {
        return mysqlFormat.Replace("%Y", "YYYY")
                         .Replace("%m", "MM")
                         .Replace("%d", "DD")
                         .Replace("%H", "HH24")
                         .Replace("%i", "MI")
                         .Replace("%s", "SS");
    }
}

/// <summary>
///     SQLite 方言实现
/// </summary>
public class SQLiteDialect : IDatabaseDialect
{
    public DatabaseType DbType => DatabaseType.SQLite;
    public string ParameterPrefix => "@";
    public (string Open, string Close) IdentifierQuotes => ("\"", "\"");
    public string StringQuote => "'";
    public string NowFunction => "datetime('now')";

    public string BuildPaginationSql(string baseSql, int offset, int limit)
    {
        // SQLite: LIMIT limit OFFSET offset
        return $"{baseSql} LIMIT {limit} OFFSET {offset}";
    }

    public string BuildCountSql(string baseSql)
    {
        var fromIndex = baseSql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        if (fromIndex < 0) return $"SELECT COUNT(*) FROM ({baseSql}) AS __count";

        return $"SELECT COUNT(*) {baseSql[fromIndex..]}";
    }

    public string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("标识符不能为空", nameof(identifier));

        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.]*$"))
            throw new ArgumentException($"非法标识符: {identifier}", nameof(identifier));

        return $"\"{identifier}\"";
    }

    public string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }

    public string ConcatFunction(IEnumerable<string> expressions)
    {
        // SQLite: expr1 || expr2
        return string.Join(" || ", expressions);
    }

    public string DateFormatFunction(string column, string format)
    {
        return $"strftime('{ConvertToSQLiteFormat(format)}', {EscapeIdentifier(column)})";
    }

    public string ContainsFunction(string column, string value)
    {
        return $"{EscapeIdentifier(column)} LIKE '%' || {ParameterPrefix}value || '%'";
    }

    public string BuildTopSql(string baseSql, int topN)
    {
        return $"{baseSql.TrimEnd(';')} LIMIT {topN}";
    }

    private static string ConvertToSQLiteFormat(string mysqlFormat)
    {
        return mysqlFormat.Replace("%Y", "%Y")
                         .Replace("%m", "%m")
                         .Replace("%d", "%d")
                         .Replace("%H", "%H")
                         .Replace("%i", "%M")
                         .Replace("%s", "%S");
    }
}

/// <summary>
///     方言工厂：根据数据库类型创建对应方言实现
/// </summary>
public static class DatabaseDialectFactory
{
    public static IDatabaseDialect Create(DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.MySql => new MySqlDialect(),
            DatabaseType.SqlServer => new SqlServerDialect(),
            DatabaseType.Oracle => new OracleDialect(),
            DatabaseType.PostgreSQL => new PostgreSqlDialect(),
            DatabaseType.SQLite => new SQLiteDialect(),
            _ => new MySqlDialect()
        };
    }

    public static IDatabaseDialect Create(string connectionString)
    {
        return Create(DatabaseTypeDetector.Detect(connectionString));
    }
}
