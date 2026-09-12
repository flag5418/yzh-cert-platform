using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.Core.DataBase.Implementations;

/// <summary>
///     SqlSugar 实现的 IDbOrm 接口
///     替代 DapperDbOrm，提供真正的 LINQ 查询能力
///     支持多数据库（通过 DbContextFactory 传入不同 SqlSugarClient）
/// </summary>
public class SqlSugarDbOrm : IDbOrm
{
    private readonly SqlSugarClient _client;
    private readonly ILogger<SqlSugarDbOrm> _logger;

    public SqlSugarDbOrm(SqlSugarClient client, ILogger<SqlSugarDbOrm> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ==================== 查询 ====================

    public async Task<Result<T?>> GetOneAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        try
        {
            var entity = await _client.Queryable<T>()
                .Where(predicate)
                .Where(IsDeletedCondition<T>())
                .Where(IsValidCondition<T>())
                .FirstAsync();

            return Result<T?>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOneAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T?>.Fail($"获取单条失败：{ex.Message}");
        }
    }

    public async Task<Result<List<T>>> GetListAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class, new()
    {
        try
        {
            var query = _client.Queryable<T>().Where(IsDeletedCondition<T>()).Where(IsValidCondition<T>());
            if (predicate != null)
                query = query.Where(predicate);
            var list = await query.ToListAsync();
            return Result<List<T>>.Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetListAsync 失败，Type={Type}", typeof(T).Name);
            return Result<List<T>>.Fail($"获取列表失败：{ex.Message}");
        }
    }

    public async Task<Result<(List<T> items, int total)>> GetPageAsync<T>(SqlPageOptions options) where T : class, new()
    {
        try
        {
            // 基础查询（优先使用 options.TableName，支持 ViewName 视图路由）
            var tableName = !string.IsNullOrEmpty(options.TableName)
                ? options.TableName
                : GetTableName<T>();
            var query = _client.Queryable<T>(tableName);

            // 软删除过滤（反引号包裹列名，避免 SQL 解析问题）
            var hasIsDeleted = HasProperty<T>("IsDeleted");
            if (hasIsDeleted)
                query = query.Where("`IsDeleted` = 0");

            // 有效标志过滤（反引号包裹列名，避免 SQL 解析问题）
            var hasIsValid = HasProperty<T>("IsValid");
            if (hasIsValid)
                query = query.Where("`IsValid` = 1");

            // 解析 Conditions
            if (options.Conditions?.Length > 0)
            {
                var (sql, parameters) = BuildConditionsSql(options.Conditions);
                if (!string.IsNullOrEmpty(sql))
                    query = query.Where(sql, parameters);
            }

            // 获取总数
            var total = await query.CountAsync();

            // 排序
            if (!string.IsNullOrWhiteSpace(options.SortField))
            {
                query = options.SortDirection?.ToUpper() == "DESC"
                    ? query.OrderBy($"{options.SortField} DESC")
                    : query.OrderBy($"{options.SortField} ASC");
            }

            // 分页
            var items = await query
                .Skip((options.PageNumber - 1) * options.PageSize)
                .Take(options.PageSize)
                .ToListAsync();

            return Result<(List<T>, int)>.Ok((items, total));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPageAsync 失败，Type={Type}", typeof(T).Name);
            return Result<(List<T>, int)>.Fail($"分页查询失败：{ex.Message}");
        }
    }

    public async Task<Result<int>> CountAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : class, new()
    {
        try
        {
            var query = _client.Queryable<T>().Where(IsDeletedCondition<T>()).Where(IsValidCondition<T>());
            if (predicate != null)
                query = query.Where(predicate);
            var count = await query.CountAsync();
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CountAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail($"统计失败：{ex.Message}");
        }
    }

    public async Task<Result<bool>> ExistsAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        try
        {
            var exists = await _client.Queryable<T>()
                .Where(IsDeletedCondition<T>())
                .Where(IsValidCondition<T>())
                .Where(predicate)
                .AnyAsync();
            return Result<bool>.Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExistsAsync 失败，Type={Type}", typeof(T).Name);
            return Result<bool>.Fail($"存在性检查失败：{ex.Message}");
        }
    }

    // ==================== 写入 ====================

    public async Task<Result<T>> InsertAsync<T>(T entity) where T : class, new()
    {
        try
        {
            await _client.Insertable(entity).ExecuteCommandAsync();
            return Result<T>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "新增"));
        }
    }

    public async Task<Result<int>> InsertBatchAsync<T>(IEnumerable<T> entities) where T : class, new()
    {
        try
        {
            var list = entities.ToList();
            if (list.Count == 0) return Result<int>.Ok(0);
            var count = await _client.Insertable(list).ExecuteCommandAsync();
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertBatchAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量新增"));
        }
    }

    public async Task<Result<T>> UpdateAsync<T>(T entity) where T : class, new()
    {
        try
        {
            // 使用 Code 作为更新条件（业务键）
            var codeValue = GetPropertyValue(entity, "Code");
            var count = await _client.Updateable(entity)
                .IgnoreColumns(GetIgnoreColumnsForUpdate())
                .Where("Code = @Code", new SugarParameter[] { new SugarParameter("@Code", codeValue) })
                .ExecuteCommandAsync();

            return count > 0
                ? Result<T>.Ok(entity)
                : Result<T>.Fail("更新失败：记录不存在或无变化");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "更新"));
        }
    }

    public async Task<Result<T>> UpdateAsync<T>(T entity, params string[] fields) where T : class, new()
    {
        try
        {
            var codeValue = GetPropertyValue(entity, "Code");
            var count = await _client.Updateable(entity)
                .UpdateColumns(fields)
                .Where("Code = @Code", new SugarParameter[] { new SugarParameter("@Code", codeValue) })
                .ExecuteCommandAsync();

            return count > 0
                ? Result<T>.Ok(entity)
                : Result<T>.Fail("更新失败：记录不存在或无变化");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAsync(fields) 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "更新"));
        }
    }

    public async Task<Result<int>> UpdateBatchAsync<T>(IEnumerable<T> entities) where T : class, new()
    {
        try
        {
            var list = entities.ToList();
            if (list.Count == 0) return Result<int>.Ok(0);
            var count = await _client.Updateable(list).ExecuteCommandAsync();
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateBatchAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量更新"));
        }
    }

    public async Task<Result<bool>> DeleteByCodeAsync<T>(string code) where T : class, new()
    {
        try
        {
            var count = await _client.Deleteable<T>()
                .Where("Code = @Code", new SugarParameter[] { new SugarParameter("@Code", code) })
                .ExecuteCommandAsync();
            return Result<bool>.Ok(count > 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteByCodeAsync 失败，Type={Type}, Code={Code}", typeof(T).Name, code);
            return Result<bool>.Fail(FormatError(ex, "删除"));
        }
    }

    public async Task<Result<int>> DeleteByCodeBatchAsync<T>(IEnumerable<string> codes) where T : class, new()
    {
        try
        {
            var codeList = codes.ToList();
            if (codeList.Count == 0) return Result<int>.Ok(0);
            var count = await _client.Deleteable<T>()
                .In("Code", codeList)
                .ExecuteCommandAsync();
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteByCodeBatchAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail(FormatError(ex, "批量删除"));
        }
    }

    // ==================== 原生 SQL ====================

    public Task<Result<List<dynamic>>> SqlQueryAsync(string sql, object? param = null)
    {
        try
        {
            var list = _client.Ado.SqlQuery<dynamic>(sql, ToSugarParameters(param));
            return Task.FromResult(Result<List<dynamic>>.Ok(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlQueryAsync 失败");
            return Task.FromResult(Result<List<dynamic>>.Fail($"SQL 查询失败：{ex.Message}"));
        }
    }

    public Task<Result<List<T>>> SqlQueryAsync<T>(string sql, object? param = null) where T : class, new()
    {
        try
        {
            var list = _client.Ado.SqlQuery<T>(sql, ToSugarParameters(param));
            return Task.FromResult(Result<List<T>>.Ok(list));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlQueryAsync<T> 失败");
            return Task.FromResult(Result<List<T>>.Fail($"SQL 查询失败：{ex.Message}"));
        }
    }

    public async Task<Result<int>> SqlExecuteAsync(string sql, object? param = null)
    {
        try
        {
            var count = await _client.Ado.ExecuteCommandAsync(sql, ToSugarParameters(param));
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlExecuteAsync 失败");
            return Result<int>.Fail($"SQL 执行失败：{ex.Message}");
        }
    }

    public Task<Result<T?>> SqlScalarAsync<T>(string sql, object? param = null)
    {
        try
        {
            var result = _client.Ado.GetScalar(sql, ToSugarParameters(param));
            if (result == null || result == DBNull.Value)
                return Task.FromResult(Result<T?>.Ok(default));
            return Task.FromResult(Result<T?>.Ok((T)Convert.ChangeType(result, typeof(T))));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SqlScalarAsync 失败");
            return Task.FromResult(Result<T?>.Fail($"SQL 标量查询失败：{ex.Message}"));
        }
    }

    public Task<Result<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? param = null) where T : class, new()
    {
        try
        {
            var list = _client.Ado.SqlQuery<T>(sql, ToSugarParameters(param));
            return Task.FromResult(Result<T?>.Ok(list.FirstOrDefault()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QueryFirstOrDefaultAsync 失败，Type={Type}", typeof(T).Name);
            return Task.FromResult(Result<T?>.Fail($"查询失败：{ex.Message}"));
        }
    }

    // ==================== 事务 ====================

    public IDbTransaction BeginTransaction()
    {
        var ado = _client.Ado;
        ado.BeginTran();
        return new SqlSugarTransaction(ado);
    }

    // ==================== 私有方法 ====================

    /// <summary>
    ///     获取软删除过滤条件（如果实体有 IsDeleted 属性）
    /// </summary>
    private static Expression<Func<T, bool>> IsDeletedCondition<T>() where T : class, new()
    {
        var prop = typeof(T).GetProperty("IsDeleted");
        if (prop == null)
            return _ => true; // 无 IsDeleted 字段，不过滤

        // 检查 [SugarColumn(IsIgnore = true)]，如果标记了则跳过过滤
        var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
        if (sugarColumn != null && sugarColumn.IsIgnore)
            return _ => true;

        var param = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(param, prop);
        var constant = Expression.Constant(false);
        var body = Expression.Equal(member, constant);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    /// <summary>
    ///     获取有效标志过滤条件（如果实体有 IsValid 属性）
    ///     1=有效（默认），0=无效
    ///     注意：如果属性标记了 [SugarColumn(IsIgnore = true)]，则跳过过滤
    /// </summary>
    private static Expression<Func<T, bool>> IsValidCondition<T>() where T : class, new()
    {
        var prop = typeof(T).GetProperty("IsValid");
        if (prop == null)
            return _ => true; // 无 IsValid 字段，不过滤

        // 检查 [SugarColumn(IsIgnore = true)]，如果标记了则跳过过滤
        var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
        if (sugarColumn != null && sugarColumn.IsIgnore)
            return _ => true;

        var param = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(param, prop);
        var constant = Expression.Constant(1);
        var body = Expression.Equal(member, constant);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    /// <summary>
    ///     将 SqlCondition 数组构建为参数化 SQL 字符串
    /// </summary>
    private static (string Sql, SugarParameter[] Parameters) BuildConditionsSql(SqlCondition[] conditions)
    {
        var parameters = new List<SugarParameter>();
        var clauses = new List<string>();

        for (int i = 0; i < conditions.Length; i++)
        {
            var c = conditions[i];
            var paramName = $"_c{i}_{c.Field}";

            if (c.Operator.Equals("IS NULL", StringComparison.OrdinalIgnoreCase) ||
                c.Operator.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
            {
                clauses.Add($"`{c.Field}` {c.Operator}");
            }
            else if (c.Operator.Equals("IN", StringComparison.OrdinalIgnoreCase) && c.Value is IEnumerable<object> values && c.Value is not string)
            {
                var inParams = new List<string>();
                int j = 0;
                foreach (var val in values)
                {
                    var inParamName = $"_in{i}_{j}_{c.Field}";
                    inParams.Add($"@{inParamName}");
                    parameters.Add(new SugarParameter($"@{inParamName}", val));
                    j++;
                }
                clauses.Add($"`{c.Field}` IN ({string.Join(", ", inParams)})");
            }
            else
            {
                clauses.Add($"`{c.Field}` {c.Operator} @{paramName}");
                parameters.Add(new SugarParameter($"@{paramName}", c.Value));
            }
        }

        return (string.Join(" AND ", clauses), parameters.ToArray());
    }

    /// <summary>
    ///     获取表名（从 [SugarTable] / [Table] 特性或类名）
    /// </summary>
    private static string GetTableName<T>()
    {
        var type = typeof(T);
        // 优先使用 SqlSugar 的 SugarTable 特性
        var sugarTableAttr = type.GetCustomAttributes(typeof(SugarTable), false)
            .Cast<SugarTable>()
            .FirstOrDefault();
        if (sugarTableAttr != null) return sugarTableAttr.TableName;

        // 其次使用 System.ComponentModel.DataAnnotations.Schema.Table 特性
        var tableAttr = type.GetCustomAttributes(typeof(TableAttribute), false)
            .Cast<TableAttribute>()
            .FirstOrDefault();
        return tableAttr?.Name ?? type.Name;
    }

    /// <summary>
    ///     判断类型是否有指定属性（排除 [SugarColumn(IsIgnore = true)] 标记的属性）
    /// </summary>
    private static bool HasProperty<T>(string propertyName)
    {
        var prop = typeof(T).GetProperty(propertyName);
        if (prop == null) return false;

        // 如果标记了 [SugarColumn(IsIgnore = true)]，视为不存在（数据库无此列）
        var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
        if (sugarColumn != null && sugarColumn.IsIgnore) return false;

        return true;
    }

    /// <summary>
    ///     获取属性值
    /// </summary>
    private static object? GetPropertyValue(object entity, string propertyName)
    {
        return entity.GetType().GetProperty(propertyName)?.GetValue(entity);
    }

    /// <summary>
    ///     更新时需要忽略的字段
    /// </summary>
    private static string[] GetIgnoreColumnsForUpdate()
    {
        return new[] { "Code", "Id", "CreateTime", "CreateBy" };
    }

    /// <summary>
    ///     将匿名对象参数转换为 SqlSugar 参数数组
    /// </summary>
    private static SugarParameter[]? ToSugarParameters(object? param)
    {
        if (param == null) return null;

        var properties = param.GetType().GetProperties();
        return properties.Select(p => new SugarParameter($"@{p.Name}", p.GetValue(param))).ToArray();
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
}
