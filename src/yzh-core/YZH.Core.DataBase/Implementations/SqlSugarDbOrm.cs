using System.Linq.Expressions;
using System.Reflection;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
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

    /// <summary>获取底层 SqlSugar 客户端（仅限 Deleteable/Insertable/Queryable 等原生 API）</summary>
    public SqlSugarClient Client => _client;

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

    /// <inheritdoc />
    public async Task<Result<T?>> GetOneIgnoreValidAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new()
    {
        try
        {
            // 仅过滤软删除，不过滤 IsValid —— 上传/转换状态机需读取 pending/replacing 中间态
            var entity = await _client.Queryable<T>()
                .Where(predicate)
                .Where(IsDeletedCondition<T>())
                .FirstAsync();

            return Result<T?>.Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOneIgnoreValidAsync 失败，Type={Type}", typeof(T).Name);
            return Result<T?>.Fail($"获取单条失败：{ex.Message}");
        }
    }

    public async Task<Result<List<T>>> GetListAsync<T>(Expression<Func<T, bool>>? predicate = null, bool includeDisabled = false) where T : class, new()
    {
        try
        {
            var query = _client.Queryable<T>().Where(IsDeletedCondition<T>());
            if (!includeDisabled)
                query = query.Where(IsValidCondition<T>());
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
            // 分页上限钳制（防止单请求拉全表）
            if (options.PageSize > PagerOptions.MaxPageSize)
                options.PageSize = PagerOptions.MaxPageSize;
            if (options.PageSize < 1)
                options.PageSize = 20;
            if (options.PageNumber < 1)
                options.PageNumber = 1;

            // 基础查询（优先使用 options.TableName，支持 ViewName 视图路由）
            var tableName = !string.IsNullOrEmpty(options.TableName)
                ? options.TableName
                : GetTableName<T>();
            var query = _client.Queryable<T>(tableName);

            // 软删除过滤（反引号包裹列名，避免 SQL 解析问题）
            var isDeletedCol = GetColumnName<T>("IsDeleted");
            if (isDeletedCol != null)
                query = query.Where($"`{isDeletedCol}` = 0");

            // 有效标志过滤（反引号包裹列名，避免 SQL 解析问题）
            // ShowDisabled=true 时由调用方设置 IncludeDisabled 跳过硬过滤
            var isValidCol = GetColumnName<T>("IsValid");
            if (isValidCol != null && !options.IncludeDisabled)
                query = query.Where($"`{isValidCol}` = 1");

            // 解析 Conditions
            if (options.Conditions?.Length > 0)
            {
                var (sql, parameters) = BuildConditionsSql<T>(options.Conditions);
                if (!string.IsNullOrEmpty(sql))
                    query = query.Where(sql, parameters);
            }

            // 获取总数
            var total = await query.CountAsync();

            // 排序（白名单校验，防止 SQL 注入）
            if (!string.IsNullOrWhiteSpace(options.SortField))
            {
                ValidateSortField<T>(options.SortField);
                var safeField = QuoteIdentifier(options.SortField);
                query = options.SortDirection?.ToUpper() == "DESC"
                    ? query.OrderBy($"{safeField} DESC")
                    : query.OrderBy($"{safeField} ASC");
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
            var result = await _client.Insertable(entity).ExecuteReturnEntityAsync();
            return Result<T>.Ok(result);
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
            var locator = BuildUpdateLocator(entity);
            if (locator == null)
                return Result<T>.Fail("更新失败：缺少业务键 Code");

            var count = await _client.Updateable(entity)
                .IgnoreColumns(GetIgnoreColumnsForUpdate())
                .Where(locator.Value.Sql, locator.Value.Params)
                .ExecuteCommandAsync();

            return await ResolveUpdateResultAsync(entity, count, locator.Value);
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
            var locator = BuildUpdateLocator(entity);
            if (locator == null)
                return Result<T>.Fail("更新失败：缺少业务键 Code");

            var count = await _client.Updateable(entity)
                .UpdateColumns(fields)
                .Where(locator.Value.Sql, locator.Value.Params)
                .ExecuteCommandAsync();

            return await ResolveUpdateResultAsync(entity, count, locator.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAsync(fields) 失败，Type={Type}", typeof(T).Name);
            return Result<T>.Fail(FormatError(ex, "更新"));
        }
    }

    /// <summary>
    ///     构建更新定位条件：**仅** Code（准则 A）。
    ///     Code 为空 → 返回 null → 调用方响亮失败「缺少业务键 Code」。
    ///     ⛔ 禁止回退 Id（Id 永不作 WHERE 定位）。
    /// </summary>
    private static (string Sql, SugarParameter[] Params)? BuildUpdateLocator<T>(T entity)
    {
        if (GetPropertyValue(entity, "Code") is string code && !string.IsNullOrWhiteSpace(code))
            return ("Code = @__locator", new[] { new SugarParameter("@__locator", code) });
        return null;
    }

    /// <summary>
    ///     归纳更新结果：MySQL 默认返回「实际变更行数」——记录存在但字段无变化时为 0。
    ///     无变化视为成功；仅在记录确实不存在时才失败。
    /// </summary>
    private async Task<Result<T>> ResolveUpdateResultAsync<T>(
        T entity, int affected, (string Sql, SugarParameter[] Params) locator) where T : class, new()
    {
        if (affected > 0)
            return Result<T>.Ok(entity);

        var exists = await _client.Queryable<T>()
            .Where(locator.Sql, locator.Params)
            .AnyAsync();

        return exists
            ? Result<T>.Ok(entity)
            : Result<T>.Fail("更新失败：记录不存在");
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

    // ==================== 物理删除与批量更新（绕过软删除过滤） ====================

    public async Task<Result<int>> PhysicalDeleteByConditionAsync<T>(Expression<Func<T, bool>> filter) where T : class, new()
    {
        try
        {
            var count = await _client.Deleteable<T>()
                .Where(filter)
                .ExecuteCommandAsync();
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PhysicalDeleteByConditionAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail($"物理删除失败：{ex.Message}");
        }
    }

    public async Task<Result<int>> BulkUpdateByConditionAsync<T>(
        Expression<Func<T, bool>> filter,
        Action<T> updater,
        params string[] fields) where T : class, new()
    {
        try
        {
            // 获取符合条件的记录
            var entities = await _client.Queryable<T>()
                .Where(filter)
                .ToListAsync();
            
            if (entities.Count == 0)
                return Result<int>.Ok(0);

            // 应用更新器
            foreach (var entity in entities)
            {
                updater(entity);
            }

            // 批量更新指定字段（按 fields 白名单）
            var count = await _client.Updateable(entities)
                .WhereColumns(fields.Length > 0 ? fields : null)
                .ExecuteCommandAsync();
            
            return Result<int>.Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BulkUpdateByConditionAsync 失败，Type={Type}", typeof(T).Name);
            return Result<int>.Fail($"批量更新失败：{ex.Message}");
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
    ///     检查属性是否在当前类型自身声明（排除 BaseEntity 继承的）
    ///     用于区分：实体自身声明了 IsDeleted → 需要过滤 vs 仅从 BaseEntity 继承 → 跳过
    /// </summary>
    private static bool IsDeclaredOnType<T>(string propertyName) where T : class, new()
    {
        var prop = typeof(T).GetProperty(propertyName);
        if (prop == null) return false;

        // [SugarColumn(IsIgnore = true)] → 数据库无此列，跳过
        var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
        if (sugarColumn != null && sugarColumn.IsIgnore) return false;

        // 检查 DeclaringType：是否在当前类型自身声明（而非 BaseEntity）
        return prop.DeclaringType == typeof(T);
    }

    /// <summary>
    ///     获取软删除过滤条件：仅当实体自身声明了 IsDeleted 属性时才过滤
    /// </summary>
    private static Expression<Func<T, bool>> IsDeletedCondition<T>() where T : class, new()
    {
        if (!IsDeclaredOnType<T>(nameof(ISoftDelete.IsDeleted)))
            return _ => true;

        var param = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(param, nameof(ISoftDelete.IsDeleted));
        var constant = Expression.Constant(false);
        var body = Expression.Equal(member, constant);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    /// <summary>
    ///     获取有效标志过滤条件：仅当实体自身声明了 IsValid 属性时才过滤
    /// </summary>
    private static Expression<Func<T, bool>> IsValidCondition<T>() where T : class, new()
    {
        if (!IsDeclaredOnType<T>(nameof(IIsValid.IsValid)))
            return _ => true;

        var param = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(param, nameof(IIsValid.IsValid));
        var constant = Expression.Constant(1);
        var body = Expression.Equal(member, constant);
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    /// <summary>
    ///     将 SqlCondition 数组构建为参数化 SQL 字符串
    ///     自动将 C# 属性名映射到 DB 列名（尊重 [SugarColumn(ColumnName)]）
    /// </summary>
    private static (string Sql, SugarParameter[] Parameters) BuildConditionsSql<T>(SqlCondition[] conditions) where T : class, new()
    {
        var parameters = new List<SugarParameter>();
        var clauses = new List<string>();

        for (int i = 0; i < conditions.Length; i++)
        {
            var c = conditions[i];
            // 关键：将前端传入的 C# 属性名映射到实际 DB 列名
            var dbColumn = GetColumnName<T>(c.Field) ?? c.Field;
            var paramName = $"_c{i}_{c.Field}";

            if (c.Operator.Equals("IS NULL", StringComparison.OrdinalIgnoreCase) ||
                c.Operator.Equals("IS NOT NULL", StringComparison.OrdinalIgnoreCase))
            {
                clauses.Add($"`{dbColumn}` {c.Operator}");
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
                clauses.Add($"`{dbColumn}` IN ({string.Join(", ", inParams)})");
            }
            else
            {
                clauses.Add($"`{dbColumn}` {c.Operator} @{paramName}");
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
    ///     获取属性对应的数据库列名
    ///     优先使用 [SugarColumn(ColumnName)] 映射，否则回退到属性名
    ///     如果属性不存在或标记了 IsIgnore，返回 null
    /// </summary>
    private static string? GetColumnName<T>(string propertyName)
    {
        var prop = typeof(T).GetProperty(propertyName);
        if (prop == null) return null;

        var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
        if (sugarColumn != null)
        {
            if (sugarColumn.IsIgnore) return null;
            if (!string.IsNullOrEmpty(sugarColumn.ColumnName)) return sugarColumn.ColumnName;
        }

        return propertyName;
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

        // 动态参数（ExpandoObject / Dictionary<string, object>）：
        // 这类对象没有可供反射的属性，必须走 IDictionary 接口，否则参数会全部丢失，
        // 生成“有占位符、无参数”的 SQL 并静默失败（如 IN (@c0) 的批量删除）。
        if (param is IDictionary<string, object> dynamicParams)
        {
            return dynamicParams
                .Select(kv => new SugarParameter($"@{kv.Key}", kv.Value))
                .ToArray();
        }

        var properties = param.GetType().GetProperties();
        return properties.Select(p => new SugarParameter($"@{p.Name}", p.GetValue(param))).ToArray();
    }

    /// <summary>
    ///     排序字段白名单校验（防止 SQL 注入）
    ///     只允许字母、数字、下划线，且必须以字母开头
    /// </summary>
    private static void ValidateSortField<T>(string sortField)
    {
        // 正则：只允许合法属性名
        if (!System.Text.RegularExpressions.Regex.IsMatch(sortField, @"^[a-zA-Z_][a-zA-Z0-9_.]*$"))
        {
            throw new InvalidOperationException($"排序字段 '{sortField}' 包含非法字符");
        }

        // 验证字段是否存在于实体属性中（支持 "A.B" 格式的点号路径，取最后一段验证）
        var fieldParts = sortField.Split('.');
        var leafField = fieldParts[^1];
        var prop = typeof(T).GetProperty(leafField);
        if (prop == null)
        {
            throw new InvalidOperationException($"排序字段 '{leafField}' 在实体 {typeof(T).Name} 中不存在");
        }
    }

    /// <summary>
    ///     标识符安全引用（防止 SQL 注入）
    ///     将排序字段包裹为安全的 SQL 标识符
    /// </summary>
    private static string QuoteIdentifier(string field)
    {
        // 如果包含点号（如 "A.B"），分段引用
        if (field.Contains('.'))
        {
            return string.Join(".", field.Split('.').Select(part => $"`{part}`"));
        }
        return $"`{field}`";
    }

    /// <summary>
    ///     将原始 SQL 参数安全化（防止注入）
    ///     校验参数值不包含 SQL 注入特征
    /// </summary>
    private static void ValidateSqlParam(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value)) return;

        // 简单检测 SQL 注入特征
        var dangerous = new[] { "DROP", "ALTER", "EXEC", "TRUNCATE", ";", "--", "UNION", "DELETE FROM" };
        var upper = value.ToUpper();
        foreach (var keyword in dangerous)
        {
            if (upper.Contains(keyword, StringComparison.Ordinal))
            {
                throw new SecurityException($"参数 '{paramName}' 包含疑似 SQL 注入内容");
            }
        }
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
