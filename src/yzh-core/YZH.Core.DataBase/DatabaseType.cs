namespace YZH.Core.DataBase;

/// <summary>
///     支持的数据库类型枚举
///     用于动态 SQL 拼接时生成对应方言
/// </summary>
public enum DatabaseType
{
    /// <summary>MySQL 8.0+</summary>
    MySql,

    /// <summary>SQL Server 2012+</summary>
    SqlServer,

    /// <summary>Oracle 12c+</summary>
    Oracle,

    /// <summary>PostgreSQL 10+</summary>
    PostgreSQL,

    /// <summary>SQLite 3+</summary>
    SQLite
}

/// <summary>
///     数据库类型探测
///     根据连接字符串自动识别数据库类型
/// </summary>
public static class DatabaseTypeDetector
{
    public static DatabaseType Detect(string connectionString)
    {
        var cs = connectionString.ToLowerInvariant();
        return cs switch
        {
            _ when cs.Contains("host=") || cs.Contains("server=") && cs.Contains("port=") && !cs.Contains("oracle") => DatabaseType.MySql,
            _ when cs.Contains("data source=") && cs.Contains("oracle") => DatabaseType.Oracle,
            _ when cs.Contains("server=") || cs.Contains("data source=") => DatabaseType.SqlServer,
            _ when cs.Contains("host=") && cs.Contains("postgres") => DatabaseType.PostgreSQL,
            _ when cs.Contains("data source=") && (cs.Contains(".db") || cs.Contains(".sqlite")) => DatabaseType.SQLite,
            _ => DatabaseType.MySql // 默认 MySQL
        };
    }
}
