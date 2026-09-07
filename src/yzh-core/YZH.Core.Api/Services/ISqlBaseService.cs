using System.Data;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Sql;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     轻量级 SQL 查询服务接口（Dapper 实现）
///     提供动态 SQL 拼接、分页、排序等能力
///     适用于复杂查询、跨表查询、性能敏感场景
///     
///     核心特性：
///     1. 数据库方言感知（MySQL/SqlServer/PostgreSQL/Oracle/SQLite）
///     2. 自动生成分页 SQL（不同数据库语法适配）
///     3. 安全的参数化查询（Dapper DynamicParameters）
///     4. 动态 WHERE 条件构建
/// </summary>
public interface ISqlBaseService
{
    /// <summary>获取当前使用的数据库方言</summary>
    IDatabaseDialect Dialect { get; }

    /// <summary>获取分页结果（方言感知生成分页 SQL）</summary>
    RepoResult<PagedResult<T>> GetPage<T>(SqlPageOptions options) where T : new();

    /// <summary>获取分页结果（动态类型，用于无实体映射场景）</summary>
    RepoResult<PagedResult<dynamic>> GetPageDynamic(SqlPageOptions options);

    /// <summary>获取列表（动态 SQL 条件 + 方言）</summary>
    RepoResult<List<T>> GetList<T>(SqlQueryOptions? options = null) where T : new();

    /// <summary>获取列表（无实体映射，纯动态 SQL）</summary>
    RepoResult<List<dynamic>> GetListDynamic(SqlQueryOptions options);

    /// <summary>获取单条记录（动态 SQL 条件）</summary>
    RepoResult<dynamic?> GetFirstOrDefault(SqlQueryOptions options);

    /// <summary>获取单条记录（强类型）</summary>
    RepoResult<T?> GetFirstOrDefault<T>(SqlQueryOptions options) where T : new();

    /// <summary>执行非查询 SQL（INSERT/UPDATE/DELETE）</summary>
    RepoResult<int> Execute(string sql, object? param = null);

    /// <summary>执行_scalar_查询（返回单个值）</summary>
    RepoResult<T?> ExecuteScalar<T>(string sql, object? param = null);

    /// <summary>获取 DataTable（用于导出等场景）</summary>
    RepoResult<DataTable> GetDataTable(string sql, object? param = null);

    /// <summary>查询总数</summary>
    RepoResult<int> Count(SqlQueryOptions options);

    /// <summary>判断是否存在</summary>
    RepoResult<bool> Exists(SqlQueryOptions options);
}

// ==================== 查询选项 ====================

/// <summary>
///     SQL 分页查询选项
/// </summary>
public class SqlPageOptions
{
    /// <summary>表名（必填，不会自动添加引号，需要自行确保表名安全）</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>查询字段（默认 *）</summary>
    public string? SelectFields { get; set; }

    /// <summary>WHERE 条件</summary>
    public SqlCondition[]? Conditions { get; set; }

    /// <summary>JOIN 子句</summary>
    public string? JoinClause { get; set; }

    /// <summary>GROUP BY 子句</summary>
    public string? GroupBy { get; set; }

    /// <summary>HAVING 子句</summary>
    public string? Having { get; set; }

    /// <summary>排序字段</summary>
    public string? SortField { get; set; }

    /// <summary>排序方向：ASC / DESC</summary>
    public string SortDirection { get; set; } = "ASC";

    /// <summary>页码（从 1 开始）</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>每页数量</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
///     SQL 查询选项（非分页）
/// </summary>
public class SqlQueryOptions
{
    /// <summary>表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>查询字段（默认 *）</summary>
    public string? SelectFields { get; set; }

    /// <summary>WHERE 条件</summary>
    public SqlCondition[]? Conditions { get; set; }

    /// <summary>JOIN 子句</summary>
    public string? JoinClause { get; set; }

    /// <summary>排序字段</summary>
    public string? SortField { get; set; }

    /// <summary>排序方向：ASC / DESC</summary>
    public string SortDirection { get; set; } = "ASC";

    /// <summary>限制返回数量</summary>
    public int? Top { get; set; }

    /// <summary>GROUP BY 子句</summary>
    public string? GroupBy { get; set; }
}

/// <summary>
///     值类型
/// </summary>
public enum SqlOperator
{
    /// <summary>等于</summary>
    Eq,

    /// <summary>不等于</summary>
    Neq,

    /// <summary>大于</summary>
    Gt,

    /// <summary>大于等于</summary>
    Gte,

    /// <summary>小于</summary>
    Lt,

    /// <summary>小于等于</summary>
    Lte,

    /// <summary>LIKE %value%</summary>
    Like,

    /// <summary>LIKE value%</summary>
    StartsWith,

    /// <summary>LIKE %value</summary>
    EndsWith,

    /// <summary>IN (values)</summary>
    In,

    /// <summary>NOT IN</summary>
    NotIn,

    /// <summary>IS NULL</summary>
    IsNull,

    /// <summary>IS NOT NULL</summary>
    IsNotNull,

    /// <summary>BETWEEN</summary>
    Between
}

/// <summary>
///     改进版 SQL 条件模型
/// </summary>
public class SqlCondition
{
    /// <summary>字段名</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>操作符</summary>
    public SqlOperator Operator { get; set; } = SqlOperator.Eq;

    /// <summary>值（用于 eq/gt/lt 等）</summary>
    public object? Value { get; set; }

    /// <summary>第二个值（用于 BETWEEN）</summary>
    public object? Value2 { get; set; }

    /// <summary>值集合（用于 IN/NOT IN）</summary>
    public IEnumerable<object>? Values { get; set; }

    /// <summary>逻辑连接符（AND/OR，默认 AND）</summary>
    public string Logic { get; set; } = "AND";

    public SqlCondition() { }

    public SqlCondition(string field, SqlOperator op, object? value, string logic = "AND")
    {
        Field = field;
        Operator = op;
        Value = value;
        Logic = logic;
    }

    public SqlCondition(string field, SqlOperator op, IEnumerable<object> values, string logic = "AND")
    {
        Field = field;
        Operator = op;
        Values = values;
        Logic = logic;
    }
}
