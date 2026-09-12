using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Implementations;

namespace YZH.Core.DataBase.MultiDatabase;

/// <summary>
///     数据库上下文工厂（多数据库支持）
///     注意：SqlSugarClient 不是线程安全的，每次调用 GetByAlias 创建新实例
/// </summary>
public class DbContextFactory : IDbContextFactory
{
    private readonly MultiDatabaseOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public DbContextFactory(IOptions<MultiDatabaseOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
    }

    public IDbOrm GetDefault()
    {
        return GetByAlias(_options.Default);
    }

    public IDbOrm GetByAlias(string alias)
    {
        var client = CreateClient(alias);
        return new SqlSugarDbOrm(client, _loggerFactory.CreateLogger<SqlSugarDbOrm>());
    }

    /// <summary>
    ///     每次创建新的 SqlSugarClient（线程安全：每次请求独立实例）
    /// </summary>
    private SqlSugarClient CreateClient(string alias)
    {
        if (!_options.Connections.TryGetValue(alias, out var conn))
            throw new ArgumentException($"数据库别名未配置: {alias}");

        var dbType = ParseDbType(conn.Provider);
        
        SqlSugar.StaticConfig.Check_StringIdentity = false;

        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = conn.ConnectionString,
            DbType = dbType,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, columnInfo) =>
                {
                    if (property.GetCustomAttributes(typeof(NotMappedAttribute), false).Any())
                    {
                        columnInfo.IsIgnore = true;
                    }
                }
            }
        });
    }

    /// <summary>
    ///     将字符串 Provider 转换为 SqlSugar.DbType 枚举
    /// </summary>
    private static DbType ParseDbType(string provider)
    {
        return provider.ToUpperInvariant() switch
        {
            "MYSQL" => DbType.MySql,
            "ORACLE" => DbType.Oracle,
            "MSSQL" or "SQLSERVER" => DbType.SqlServer,
            "SQLITE" => DbType.Sqlite,
            "POSTGRESQL" or "PG" => DbType.PostgreSQL,
            // 国产数据库暂不支持，预留扩展
            // "DAMENG" => DbType.Dameng,
            // "KINGBASEES" => DbType.KingbaseES,
            _ => throw new NotSupportedException($"不支持的数据库提供程序: {provider}")
        };
    }
}
