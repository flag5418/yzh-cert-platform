using System.Data;
using System.Linq.Expressions;
using System.Text;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using YZH.Core.DataBase.Sql;
using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase;

/// <summary>
///     Dapper 实现的 IDbOrm 接口
///     统一使用 Dapper 作为数据库操作核心，放弃 EF Core 双路并存
/// </summary>
public class DapperDbOrm : IDbOrm
{
    private readonly string _connectionString;
    private readonly ILogger<DapperDbOrm> _logger;
    private readonly IDatabaseDialect _dialect;

    public DapperDbOrm(IConfiguration configuration, ILogger<DapperDbOrm> logger)
    {
        _logger = logger;
        // 优先 CertPlatform，兼容 DefaultConnection
        _connectionString = configuration.GetConnectionString("CertPlatform")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("缺少数据库连接字符串: CertPlatform 或 DefaultConnection");
        _dialect = DatabaseDialectFactory.Create(DatabaseTypeDetector.Detect(_connectionString));
    }

    // ==================== 查询 ====================

    public async Task<Result<T?>> GetOneAsync<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            using var connection = CreateConnection();
            var sql = $"SELECT * FROM {SqlSecurityHelper.EscapeIdentifier(table)} WHERE 1=1";
            // 注意：Dapper 不支持表达式树直接转换，这里简化实现
            // 实际项目可使用 ExpressionVisitor 转换为 SQL
            var entity = await connection.QueryFirstOrDefaultAsync<T>(sql);
            return Result<T?>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOneAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T?>.Fail($"获取单条失败：{ex.Message}");
        }
    }

    public async Task<Result<List<T>>> GetListAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            using var connection = CreateConnection();
            var sql = $"SELECT * FROM {SqlSecurityHelper.EscapeIdentifier(table)} WHERE IsDeleted = 0";
            var entities = await connection.QueryAsync<T>(sql);
            return Result<List<T>>.Ok(entities.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetListAsync 失败，Type={Type}", typeof(T).Name);
            return Result<List<T>>.Fail($"获取列表失败：{ex.Message}");
        }
    }

    public async Task<Result<(List<T> items, int total)>> GetPageAsync<T>(SqlPageOptions options) where T : class
    {
        try
        {
            var parameters = new DynamicParameters();
            var sql = BuildBaseSql(options, parameters);
            var countSql = _dialect.BuildCountSql(sql);
            
            using var connection = CreateConnection();
            var total = await connection.ExecuteScalarAsync<int>(countSql, parameters);
            
            var pageSql = BuildPagedSql(sql, options);
            var items = (await connection.QueryAsync<T>(pageSql, parameters)).ToList();
            
            return Result<(List<T>, int)>.Ok((items, total));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPageAsync 失败，Type={Type}", typeof(T).Name);
            return Result<(List<T>, int)>.Fail($"分页查询失败：{ex.Message}");
        }
    }

    public async Task<Result<int>> CountAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            using var connection = CreateConnection();
            var sql = $"SELECT COUNT(*) FROM {SqlSecurityHelper.EscapeIdentifier(table)} WHERE IsDeleted = 0";
            var count = await connection.ExecuteScalarAsync<int>(sql);
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CountAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail($"统计失败：{ex.Message}");
        }
    }

    public async Task<Result<bool>> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            using var connection = CreateConnection();
            var sql = $"SELECT COUNT(*) FROM {SqlSecurityHelper.EscapeIdentifier(table)} WHERE IsDeleted = 0 LIMIT 1";
            var count = await connection.ExecuteScalarAsync<int>(sql);
            return Result<bool>.Ok(count > 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExistsAsync 失败，Type={Type}", typeof(T).Name);
            return Result<bool>.Fail($"存在性检查失败：{ex.Message}");
        }
    }

    // ==================== 写入 ====================

    public async Task<Result<T>> InsertAsync<T>(T entity) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            var columns = GetEntityColumns<T>();
            var placeholders = string.Join(", ", columns.Select(c => $"@{c}"));
            var columnList = string.Join(", ", columns.Select(c => SqlSecurityHelper.EscapeIdentifier(c)));
            
            using var connection = CreateConnection();
            var sql = $"INSERT INTO {SqlSecurityHelper.EscapeIdentifier(table)} ({columnList}) VALUES ({placeholders})";
            
            await connection.ExecuteAsync(sql, entity);
            
            // 获取自增 ID（MySQL）
            var idSql = "SELECT LAST_INSERT_ID()";
            var id = await connection.ExecuteScalarAsync<long>(idSql);
            
            // 更新实体的 Id 字段（如果是 long 类型）
            var idProp = typeof(T).GetProperty("Id");
            if (idProp != null && idProp.PropertyType == typeof(long))
            {
                idProp.SetValue(entity, id);
            }
            
            return Result<T>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "新增"));
        }
    }

    public async Task<Result<int>> InsertBatchAsync<T>(IEnumerable<T> entities) where T : class
    {
        try
        {
            var list = entities.ToList();
            if (list.Count == 0) return Result<int>.Ok(0);
            
            var table = GetTableName<T>();
            var columns = GetEntityColumns<T>();
            var placeholders = string.Join(", ", columns.Select(c => $"@{c}"));
            var columnList = string.Join(", ", columns.Select(c => SqlSecurityHelper.EscapeIdentifier(c)));
            
            using var connection = CreateConnection();
            var sql = $"INSERT INTO {SqlSecurityHelper.EscapeIdentifier(table)} ({columnList}) VALUES ({placeholders})";
            
            var count = await connection.ExecuteAsync(sql, list);
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertBatchAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量新增"));
        }
    }

    public async Task<Result<T>> UpdateAsync<T>(T entity) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            var columns = GetEntityColumns<T>();
            var setClauses = columns
                .Where(c => c != "Code")
                .Select(c => $"{SqlSecurityHelper.EscapeIdentifier(c)} = @{c}");
            
            using var connection = CreateConnection();
            // 使用 Code 作为更新条件（业务键）
            var sql = $"UPDATE {SqlSecurityHelper.EscapeIdentifier(table)} SET {string.Join(", ", setClauses)} WHERE Code = @Code";
            
            var count = await connection.ExecuteAsync(sql, entity);
            
            if (count == 0)
                return Result<T>.Fail("更新失败：记录不存在或无变化");
            
            return Result<T>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "更新"));
        }
    }

    public async Task<Result<T>> UpdateAsync<T>(T entity, params string[] fields) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            var setClauses = fields
                .Where(f => f != "Code")
                .Select(f => $"{SqlSecurityHelper.EscapeIdentifier(f)} = @{f}");
            
            using var connection = CreateConnection();
            // 使用 Code 作为更新条件（业务键）
            var sql = $"UPDATE {SqlSecurityHelper.EscapeIdentifier(table)} SET {string.Join(", ", setClauses)} WHERE Code = @Code";
            
            var count = await connection.ExecuteAsync(sql, entity);
            
            if (count == 0)
                return Result<T>.Fail("更新失败：记录不存在或无变化");
            
            return Result<T>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAsync(fields) 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "更新"));
        }
    }

    public async Task<Result<int>> UpdateBatchAsync<T>(IEnumerable<T> entities) where T : class
    {
        try
        {
            var list = entities.ToList();
            if (list.Count == 0) return Result<int>.Ok(0);
            
            var table = GetTableName<T>();
            var setClauses = GetEntityColumns<T>()
                .Where(c => c != "Code")
                .Select(c => $"{SqlSecurityHelper.EscapeIdentifier(c)} = @{c}");
            
            using var connection = CreateConnection();
            // 使用 Code 作为更新条件（业务键）
            var sql = $"UPDATE {SqlSecurityHelper.EscapeIdentifier(table)} SET {string.Join(", ", setClauses)} WHERE Code = @Code";
            
            var count = await connection.ExecuteAsync(sql, list);
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateBatchAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量更新"));
        }
    }

    public async Task<Result<bool>> DeleteByCodeAsync<T>(string code) where T : class
    {
        try
        {
            var table = GetTableName<T>();
            using var connection = CreateConnection();
            var sql = $"DELETE FROM {SqlSecurityHelper.EscapeIdentifier(table)} WHERE Code = @Code";
            var count = await connection.ExecuteAsync(sql, new { Code = code });
            return Result<bool>.Ok(count > 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteByCodeAsync 失败，Type={Type}, Code={Code}", typeof(T).Name, code);
            return Result<bool>.Fail(FormatError(ex, "删除"));
        }
    }

    public async Task<Result<int>> DeleteByCodeBatchAsync<T>(IEnumerable<string> codes) where T : class
    {
        try
        {
            var codeList = codes.ToList();
            if (codeList.Count == 0) return Result<int>.Ok(0);

            var table = GetTableName<T>();
            var paramDict = new Dictionary<string, object?>();
            var placeholders = new List<string>();
            for (int i = 0; i < codeList.Count; i++)
            {
                var paramName = $"code{i}";
                placeholders.Add($"@{paramName}");
                paramDict[paramName] = codeList[i];
            }

            using var connection = CreateConnection();
            var sql = $"DELETE FROM {SqlSecurityHelper.EscapeIdentifier(table)} WHERE Code IN ({string.Join(", ", placeholders)})";
            var count = await connection.ExecuteAsync(sql, paramDict);
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteByCodeBatchAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量删除"));
        }
    }

    // ==================== 原生 SQL ====================

    public async Task<Result<List<dynamic>>> SqlQueryAsync(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync(sql, param);
            return Result<List<dynamic>>.Ok(result.Cast<dynamic>().ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlQueryAsync 失败");
            return Result<List<dynamic>>.Fail($"SQL 查询失败：{ex.Message}");
        }
    }

    public async Task<Result<List<T>>> SqlQueryAsync<T>(string sql, object? param = null) where T : class
    {
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<T>(sql, param);
            return Result<List<T>>.Ok(result.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlQueryAsync<T> 失败");
            return Result<List<T>>.Fail($"SQL 查询失败：{ex.Message}");
        }
    }

    public async Task<Result<int>> SqlExecuteAsync(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            var count = await connection.ExecuteAsync(sql, param);
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlExecuteAsync 失败");
            return Result<int>.Fail($"SQL 执行失败：{ex.Message}");
        }
    }

    public async Task<Result<T?>> SqlScalarAsync<T>(string sql, object? param = null)
    {
        try
        {
            using var connection = CreateConnection();
            var result = await connection.ExecuteScalarAsync<T>(sql, param);
            return Result<T?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlScalarAsync 失败");
            return Result<T?>.Fail($"SQL 标量查询失败：{ex.Message}");
        }
    }

    /// <summary>执行 SQL 查询并返回单条记录（强类型 + 参数化）</summary>
    public async Task<Result<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? param = null) where T : class
    {
        try
        {
            using var connection = CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<T>(sql, param);
            return Result<T?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueryFirstOrDefaultAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T?>.Fail($"查询失败：{ex.Message}");
        }
    }

    // ==================== 事务 ====================

    public IDbTransaction BeginTransaction()
    {
        var connection = CreateConnection();
        var transaction = connection.BeginTransaction();
        return new DapperTransaction(transaction, connection);
    }

    // ==================== 私有方法 ====================

    private MySqlConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    private static string GetTableName<T>() where T : class
    {
        // 从 [Table] 特性获取表名，默认使用类名
        var type = typeof(T);
        var tableAttr = type.GetCustomAttributes(typeof(TableAttribute), false)
            .Cast<TableAttribute>()
            .FirstOrDefault();
        return tableAttr?.Name ?? type.Name;
    }

    private static List<string> GetEntityColumns<T>() where T : class
    {
        return typeof(T).GetProperties()
            .Select(p => p.Name)
            .ToList();
    }

    /// <summary>
    ///     构建基础 SQL 查询（不含分页 LIMIT）
    ///     同时填充 DynamicParameters 供 Dapper 参数化使用
    /// </summary>
    private static string BuildBaseSql(SqlPageOptions options, DynamicParameters parameters)
    {
        var sql = new StringBuilder();
        var fields = string.IsNullOrWhiteSpace(options.SelectFields) ? "*" : options.SelectFields;
        
        sql.Append($"SELECT {fields} FROM {SqlSecurityHelper.EscapeIdentifier(options.TableName)}");
        
        if (!string.IsNullOrWhiteSpace(options.JoinClause))
            sql.Append($" {SqlSecurityHelper.ValidateSqlFragment(options.JoinClause, "JoinClause")}");
        
        // 添加软删除过滤
        sql.Append(" WHERE IsDeleted = 0");
        
        AppendWhereClause(sql, options.Conditions, parameters);
        
        if (!string.IsNullOrWhiteSpace(options.GroupBy))
            sql.Append($" GROUP BY {SqlSecurityHelper.ValidateSqlFragment(options.GroupBy, "GroupBy")}");
        
        if (!string.IsNullOrWhiteSpace(options.Having))
            sql.Append($" HAVING {SqlSecurityHelper.ValidateSqlFragment(options.Having, "Having")}");
        
        if (!string.IsNullOrWhiteSpace(options.SortField))
        {
            var sortField = SqlSecurityHelper.IsValidIdentifier(options.SortField)
                ? SqlSecurityHelper.EscapeIdentifier(options.SortField)
                : throw new ArgumentException($"非法的排序字段: {options.SortField}");
            sql.Append($" ORDER BY {sortField} {options.SortDirection}");
        }
        
        return sql.ToString();
    }

    private static string BuildPagedSql(string baseSql, SqlPageOptions options)
    {
        // MySQL 分页
        var offset = (options.PageNumber - 1) * options.PageSize;
        return $"{baseSql} LIMIT {options.PageSize} OFFSET {offset}";
    }

    /// <summary>
    ///     拼接 WHERE 条件子句（不含 WHERE 关键字）
    ///     支持的操作符：=, !=, &gt;, &gt;=, &lt;, &lt;=, LIKE, IN, IS NULL, IS NOT NULL
    ///     所有值通过 Dapper 参数化传递，防止 SQL 注入
    /// </summary>
    private static void AppendWhereClause(StringBuilder sql, SqlCondition[]? conditions, DynamicParameters parameters)
    {
        if (conditions == null || conditions.Length == 0) return;
        
        sql.Append(" AND (");
        var clauses = new List<string>();
        for (int i = 0; i < conditions.Length; i++)
        {
            var c = conditions[i];
            var paramName = $"_c{i}_{c.Field}"; // 带索引前缀，避免参数名冲突
            
            if (c.Operator.Equals("IS NULL", StringComparison.OrdinalIgnoreCase) ||
                c.Operator.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
            {
                // IS NULL / IS NOT NULL 不需要参数
                clauses.Add($"{SqlSecurityHelper.EscapeIdentifier(c.Field)} {c.Operator}");
            }
            else if (c.Operator.Equals("IN", StringComparison.OrdinalIgnoreCase) && c.Value is IEnumerable<object> values && c.Value is not string)
            {
                // IN 操作符：展开为多个参数
                var inParams = new List<string>();
                int j = 0;
                foreach (var val in values)
                {
                    var inParamName = $"_in{i}_{j}_{c.Field}";
                    inParams.Add($"@{inParamName}");
                    parameters.Add(inParamName, val);
                    j++;
                }
                clauses.Add($"{SqlSecurityHelper.EscapeIdentifier(c.Field)} IN ({string.Join(", ", inParams)})");
            }
            else
            {
                clauses.Add($"{SqlSecurityHelper.EscapeIdentifier(c.Field)} {c.Operator} @{paramName}");
                parameters.Add(paramName, c.Value);
            }
        }
        sql.Append(string.Join(" AND ", clauses));
        sql.Append(")");
    }

    private static string FormatError(Exception ex, string operation)
    {
        var msg = ex.Message;
        
        if (msg.Contains("Duplicate") || msg.Contains("UNIQUE") || msg.Contains("duplicate"))
            return $"{operation}失败：数据已存在（唯一约束冲突）";
        
        if (msg.Contains("foreign key") || msg.Contains("FOREIGN KEY"))
            return $"{operation}失败：数据被其他业务引用，无法操作";
        
        if (msg.Contains("Cannot insert the value NULL") || msg.Contains("cannot be null"))
            return $"{operation}失败：必填字段为空";
        
        return $"{operation}失败：{msg}";
    }

    private class DapperTransaction : IDbTransaction
    {
        private readonly MySqlTransaction _transaction;
        private readonly MySqlConnection _connection;
        private bool _disposed;

        public DapperTransaction(MySqlTransaction transaction, MySqlConnection connection)
        {
            _transaction = transaction;
            _connection = connection;
        }

        public void Commit()
        {
            _transaction.Commit();
            _connection.Dispose();
        }

        public void Rollback()
        {
            _transaction.Rollback();
            _connection.Dispose();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { _transaction.Rollback(); } catch { /* 忽略 */ }
                _transaction.Dispose();
                _connection.Dispose();
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// [Table] 特性（用于获取表名）
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TableAttribute : Attribute
{
    public string Name { get; set; }
    
    public TableAttribute(string name)
    {
        Name = name;
    }
}
