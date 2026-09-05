namespace YZH.Core.Stand.Models;

/// <summary>
///     分页查询参数
/// </summary>
public class PagerOptions
{
    /// <summary>当前页（从1开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页行数</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>排序列名</summary>
    public string? SortBy { get; set; }

    /// <summary>排序方向 asc/desc</summary>
    public string? SortDirection { get; set; } = "asc";

    /// <summary>搜索关键字</summary>
    public string? SearchKey { get; set; }

    /// <summary>高级过滤条件</summary>
    public List<FilterItem>? Filters { get; set; }

    /// <summary>跳过分页取全量</summary>
    public bool NoPage { get; set; }
}

public class FilterItem
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";  // eq/neq/gt/lt/like/in
    public object? Value { get; set; }
}
