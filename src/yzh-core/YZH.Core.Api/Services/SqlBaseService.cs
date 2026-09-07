using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Sql;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     轻量级 SQL 查询服务实现（Dapper + 多数据库方言）
///     自动根据连接字符串识别数据库类型，生成对应方言的 SQL
/// </summary>
public class SqlBaseService : ISqlBaseService
{
    private readonly string _connectionString;
    private readonly ILogger<SqlBaseService> _logger;

    public IDatabaseDialect Dialect { get; }

    public SqlBaseService(IConfiguration configuration, ILogger<SqlBaseService> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("缺少数据库连接字符串: DefaultConnection");

        // 根据连接字符串自动识别数据库类型，创建对应方言
        var dbType = DatabaseTypeDetector.Detect(_connectionString);
        Dialect = DatabaseDialectFactory.Create(dbType);
    }

    public SqlBaseService(string connectionString, ILogger<SqlBaseService> logger)
    {
        _logger = logger;
        _connectionString = connectionString;
        var dbType = DatabaseTypeDetector.Detect(connectionString);
        Dialect = DatabaseDialectFactory.Create(dbType);
    }

    // ==================== 分页查询 ====================

    public RepoResult<PagedResult<T>> GetPage<T>(SqlPageOptions options) where T : new()
    {
        try
        {
            ArgumentNullException.ThrowIfNull(options);

            using var connection = CreateConnection();

            // 构建基础 SQL
            var baseSql = BuildBaseSql(options);

            // 先查总数
            var countSql = Dialect.BuildCountSql(baseSql);
            var total = connection.ExecuteScalar<int>(countSql);

            // 构建带分页的 SQL
            var pageSql = BuildPagedSql(baseSql, options);

            var items = connection.Query<T>(pageSql).ToList();

            return RepoResult<PagedResult<T>>.Ok(new PagedResult<T>(items, total, options.PageNumber, options.PageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPage 查询失败: {Message}", ex.Message);
            return RepoResult<PagedResult<T>>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    public RepoResult<PagedResult<dynamic>> GetPageDynamic(SqlPageOptions options)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(options);

            using var connection = CreateConnection();

            var baseSql = BuildBaseSql(options);
            var countSql = Dialect.BuildCountSql(baseSql);
            var total = connection.ExecuteScalar<int>(countSql);
            var pageSql = BuildPagedSql(baseSql, options);

            var items = connection.Query(pageSql).ToList();

            return RepoResult<PagedResult<dynamic>>.Ok(new PagedResult<dynamic>(items, total, options.PageNumber, options.PageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPage(dynamic) 查询失败: {Message}", ex.Message);
            return RepoResult<PagedResult<dynamic>>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    // ==================== 列表查询 ====================

    public RepoResult<List<T>> GetList<T>(SqlQueryOptions? options = null) where T : new()
    {
        try
        {
            using var connection = CreateConnection();
            var sql = BuildQuerySql(options);
            var items = connection.Query<T>(sql).ToList();
            return RepoResult<List<T>>.Ok(items, items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetList<T> 查询失败: {Message}", ex.Message);
            return RepoResult<List<T>>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    public RepoResult<List<dynamic>> GetListDynamic(SqlQueryOptions options)
    {
        try
        {
            using var connection = CreateConnection();
            var sql = BuildQuerySql(options);
            var items = connection.Query(sql).ToList();
            return RepoResult<List<dynamic>>.Ok(items, items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetList(dynamic) 查询失败: {Message}", ex.Message);
            return RepoResult<List<dynamic>>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    // ==================== 单条查询 ====================

    public RepoResult<dynamic?> GetFirstOrDefault(SqlQueryOptions options)
    {
        try
        {
            using var connection = CreateConnection();
            var sql = BuildQuerySql(options);
            var item = connection.QueryFirstOrDefault(sql);
            return RepoResult<dynamic?>.Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFirstOrDefault(dynamic) 失败: {Message}", ex.Message);
            return RepoResult<dynamic?>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    public RepoResult<T?> GetFirstOrDefault<T>(SqlQueryOptions options) where T : new()
    {
        try
        {
            using var connection = CreateConnection();
            var sql = BuildQuerySql(options);
            var item = connection.QueryFirstOrDefault<T>(sql);
            return RepoResult<T?>.Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFirstOrDefault<T> 失败: {Message}", ex.Message);
            return RepoResult<T?>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    // ==================== 执行 SQL ====================

    public RepoResult<int> Execute(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            var affected = connection.Execute(sql, param);
            return RepoResult<int>.Ok(affected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execute 执行失败: {Message}", ex.Message);
            return RepoResult<int>.Fail($"执行失败: {ex.Message}", ex);
        }
    }

    public RepoResult<T?> ExecuteScalar<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            var result = connection.ExecuteScalar<T>(sql, param);
            return RepoResult<T?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteScalar 执行失败: {Message}", ex.Message);
            return RepoResult<T?>.Fail($"执行失败: {ex.Message}", ex);
        }
    }

    public RepoResult<DataTable> GetDataTable(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            var reader = connection.ExecuteReader(sql, param);
            var table = new DataTable();
            table.Load(reader);
            return RepoResult<DataTable>.Ok(table);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDataTable 执行失败: {Message}", ex.Message);
            return RepoResult<DataTable>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    // ==================== 聚合查询 ====================

    public RepoResult<int> Count(SqlQueryOptions options)
    {
        try
        {
            var countOptions = new SqlQueryOptions
            {
                TableName = options.TableName,
                SelectFields = "COUNT(*)",
                Conditions = options.Conditions,
                JoinClause = options.JoinClause,
                GroupBy = options.GroupBy
            };

            using var connection = CreateConnection();
            var sql = BuildQuerySql(countOptions);
            var count = connection.ExecuteScalar<int>(sql);
            return RepoResult<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Count 查询失败: {Message}", ex.Message);
            return RepoResult<int>.Fail($"查询失败: {ex.Message}", ex);
        }
    }

    public RepoResult<bool> Exists(SqlQueryOptions options)
    {
        var countResult = Count(options);
        return countResult.Success
            ? RepoResult<bool>.Ok(countResult.Data > 0)
            : RepoResult<bool>.Fail(countResult.ErrorMessage!, countResult.Exception);
    }

    // ==================== SQL 构建核心方法 ====================

    /// <summary>构建基础 SELECT SQL（不含分页）</summary>
    private string BuildBaseSql(SqlPageOptions options)
    {
        var fields = string.IsNullOrWhiteSpace(options.SelectFields) ? "*" : options.SelectFields;
        var sql = new StringBuilder();

        sql.Append($"SELECT {fields} FROM {options.TableName}");

        if (!string.IsNullOrWhiteSpace(options.JoinClause))
            sql.Append($" {options.JoinClause}");

        AppendWhereClause(sql, options.Conditions);

        if (!string.IsNullOrWhiteSpace(options.GroupBy))
            sql.Append($" GROUP BY {options.GroupBy}");

        if (!string.IsNullOrWhiteSpace(options.Having))
            sql.Append($" HAVING {options.Having}");

        // 排序（SQL Server 分页必须带 ORDER BY）
        if (!string.IsNullOrWhiteSpace(options.SortField))
            sql.Append($" ORDER BY {options.SortField} {options.SortDirection}");

        // 对于 SQL Server，如果分页没有排序，需要给一个默认排序以避免错误
        if (DatabaseTypeDetector.Detect(_connectionString) == DatabaseType.SqlServer &&
            string.IsNullOrWhiteSpace(options.SortField) && string.IsNullOrWhiteSpace(options.GroupBy))
        {
            sql.Append(" ORDER BY (SELECT NULL)");
        }

        return sql.ToString();
    }

    /// <summary>构建基础 SELECT SQL（无分页版本，用于普通查询）</summary>
    private string BuildQuerySql(SqlQueryOptions? options)
    {
        if (options == null)
            return "SELECT * FROM (SELECT 1 WHERE 1=0) AS __empty"; // 返回空的 SQL

        var fields = string.IsNullOrWhiteSpace(options.SelectFields) ? "*" : options.SelectFields;
        var sql = new StringBuilder();

        sql.Append($"SELECT {fields} FROM {options.TableName}");

        if (!string.IsNullOrWhiteSpace(options.JoinClause))
            sql.Append($" {options.JoinClause}");

        AppendWhereClause(sql, options.Conditions);

        if (!string.IsNullOrWhiteSpace(options.SortField))
            sql.Append($" ORDER BY {options.SortField} {options.SortDirection}");

        // 处理 Top N
        if (options.Top.HasValue && options.Top.Value > 0)
        {
            var topSql = Dialect.BuildTopSql(sql.ToString(), options.Top.Value);
            return topSql;
        }

        return sql.ToString();
    }

    /// <summary>构建分页 SQL</summary>
    private string BuildPagedSql(string baseSql, SqlPageOptions options)
    {
        var offset = (options.PageNumber - 1) * options.PageSize;
        return Dialect.BuildPaginationSql(baseSql, offset, options.PageSize);
    }

    /// <summary>追加 WHERE 子句（使用参数化，防止注入）</summary>
    private void AppendWhereClause(StringBuilder sql, SqlCondition[]? conditions)
    {
        if (conditions == null || conditions.Length == 0)
            return;

        sql.Append(" WHERE ");
        for (var i = 0; i < conditions.Length; i++)
        {
            var condition = conditions[i];

            if (i > 0)
                sql.Append($" {condition.Logic} ");

            // 安全验证：只允许合法标识符
            if (!IsValidIdentifier(condition.Field))
            {
                // 跳过非法条件，记录警告
                _logger.LogWarning("跳过非法字段名: {Field}", condition.Field);
                sql.Append("1=1"); // 恒真条件保持 SQL 语法正确
                continue;
            }

            var fieldName = Dialect.EscapeIdentifier(condition.Field);

            switch (condition.Operator)
            {
                case SqlOperator.Eq:
                    sql.Append($"{fieldName} = @{condition.Field}");
                    break;
                case SqlOperator.Neq:
                    sql.Append($"{fieldName} <> @{condition.Field}");
                    break;
                case SqlOperator.Gt:
                    sql.Append($"{fieldName} > @{condition.Field}");
                    break;
                case SqlOperator.Gte:
                    sql.Append($"{fieldName} >= @{condition.Field}");
                    break;
                case SqlOperator.Lt:
                    sql.Append($"{fieldName} < @{condition.Field}");
                    break;
                case SqlOperator.Lte:
                    sql.Append($"{fieldName} <= @{condition.Field}");
                    break;
                case SqlOperator.Like:
                    sql.Append($"{fieldName} LIKE @{condition.Field}");
                    break;
                case SqlOperator.StartsWith:
                    sql.Append($"{fieldName} LIKE @{condition.Field}");
                    break;
                case SqlOperator.EndsWith:
                    sql.Append($"{fieldName} LIKE @{condition.Field}");
                    break;
                case SqlOperator.In:
                    sql.Append($"{fieldName} IN @{condition.Field}");
                    break;
                case SqlOperator.NotIn:
                    sql.Append($"{fieldName} NOT IN @{condition.Field}");
                    break;
                case SqlOperator.IsNull:
                    sql.Append($"{fieldName} IS NULL");
                    break;
                case SqlOperator.IsNotNull:
                    sql.Append($"{fieldName} IS NOT NULL");
                    break;
                case SqlOperator.Between:
                    sql.Append($"{fieldName} BETWEEN @{condition.Field}_from AND @{condition.Field}_to");
                    break;
                default:
                    sql.Append($"{fieldName} = @{condition.Field}");
                    break;
            }
        }
    }

    /// <summary>验证标识符合法性，防止 SQL 注入</summary>
    private static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        // 允许：字母数字下划线.，不允许其他特殊字符
        return System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_.]*$");
    }

    /// <summary>创建数据库连接</summary>
    private IDbConnection CreateConnection()
    {
        var dbType = DatabaseTypeDetector.Detect(_connectionString);
        return dbType switch
        {
            DatabaseType.MySql => new MySqlConnection(_connectionString),
            // TODO: 按需扩展其他数据库连接
            _ => new MySqlConnection(_connectionString) // 默认 MySQL
        };
    }
}
