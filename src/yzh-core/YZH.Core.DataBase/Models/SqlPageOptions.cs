namespace YZH.Core.DataBase.Models;

/// <summary>
///     分页查询选项（API 入参模型）
///     封装分页查询所需的所有参数
/// </summary>
public class SqlPageOptions
{
    /// <summary>表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>查询字段（默认 *）</summary>
    public string SelectFields { get; set; } = "*";

    /// <summary>页码（从 1 开始）</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>每页数量</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>排序字段</summary>
    public string? SortField { get; set; }

    /// <summary>排序方向：ASC / DESC</summary>
    public string SortDirection { get; set; } = "ASC";

    /// <summary>JOIN 子句</summary>
    public string? JoinClause { get; set; }

    /// <summary>分组字段</summary>
    public string? GroupBy { get; set; }

    /// <summary>分组过滤条件</summary>
    public string? Having { get; set; }

    /// <summary>查询条件数组</summary>
    public SqlCondition[]? Conditions { get; set; }
}
