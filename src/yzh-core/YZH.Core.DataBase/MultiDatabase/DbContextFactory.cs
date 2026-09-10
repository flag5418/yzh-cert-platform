using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Implementations;

namespace YZH.Core.DataBase.MultiDatabase;

/// <summary>
///     数据库上下文工厂（多数据库支持）
///     内部维护多个 SqlSugarClient 实例，通过别名切换
/// </summary>
public class DbContextFactory : IDbContextFactory
{
    private readonly MultiDatabaseOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, SqlSugarClient> _clients = new();
    private readonly object _lock = new();

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
        var client = GetOrCreateClient(alias);
        return new SqlSugarDbOrm(client, _loggerFactory.CreateLogger<SqlSugarDbOrm>());
    }

    /// <summary>
    ///     获取或创建 SqlSugarClient（线程安全，每个别名一个实例）
    /// </summary>
    private SqlSugarClient GetOrCreateClient(string alias)
    {
        if (_clients.TryGetValue(alias, out var existing))
            return existing;

        lock (_lock)
        {
            if (_clients.TryGetValue(alias, out existing))
                return existing;

                if (!_options.Connections.TryGetValue(alias, out var conn))
                    throw new ArgumentException($"数据库别名未配置: {alias}");

                var dbType = ParseDbType(conn.Provider);
                
                var client = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = conn.ConnectionString,
                    DbType = dbType,
                    IsAutoCloseConnection = true,
                    ConfigureExternalServices = new ConfigureExternalServices
                    {
                        EntityService = (property, columnInfo) =>
                        {
                            // 忽略标记了 [NotMapped] 的属性（如 IsLeaf 等计算属性）
                            if (property.GetCustomAttributes(typeof(NotMappedAttribute), false).Any())
                            {
                                columnInfo.IsIgnore = true;
                            }
                        }
                    }
                });

                _clients[alias] = client;
                return client;
        }
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
