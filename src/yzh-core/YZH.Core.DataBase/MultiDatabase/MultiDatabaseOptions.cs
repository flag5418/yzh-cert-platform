namespace YZH.Core.DataBase.MultiDatabase;

/// <summary>
///     多数据库配置根节点
///     支持通过别名切换不同数据库（MySQL/Oracle/MSSQL 等）
/// </summary>
public class MultiDatabaseOptions
{
    /// <summary>
    ///     默认数据库别名（不传别名时使用）
    /// </summary>
    public string Default { get; set; } = "DefaultDataBase";

    /// <summary>
    ///     数据库连接集合（Key = 别名，Value = 连接配置）
    /// </summary>
    public Dictionary<string, DatabaseConnection> Connections { get; set; } = new();
}

/// <summary>
///     单个数据库连接配置
/// </summary>
public class DatabaseConnection
{
    /// <summary>
    ///     数据库提供程序类型：MySql / Oracle / SqlServer / SQLite / PostgreSQL / Dameng / KingbaseES
    /// </summary>
    public string Provider { get; set; } = "MySql";

    /// <summary>
    ///     连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    ///     连接池最大连接数（默认 100）
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    ///     连接超时秒数（默认 30）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;
}
