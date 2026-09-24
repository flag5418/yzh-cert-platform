namespace YZH.Core.Stand.Models;

/// <summary>
///     分页查询参数
/// </summary>
public class PagerOptions
{
    /// <summary>服务端最大分页大小（钳制，防止单请求拉全表）</summary>
    public const int MaxPageSize = 500;

    /// <summary>当前页（从1开始）</summary>
    public int Page { get; set; } = 1;

    /// <summary>每页行数（服务端钳制到 [1, MaxPageSize]）</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>钳制后的分页大小（供 SQL 层使用）</summary>
    public int SafePageSize
    {
        get
        {
            var p = PageSize;
            if (p < 1) p = 20;
            if (p > MaxPageSize) p = MaxPageSize;
            return p;
        }
    }

    /// <summary>钳制后的页码（供 SQL 层使用）</summary>
    public int SafePage
    {
        get
        {
            var p = Page;
            if (p < 1) p = 1;
            return p;
        }
    }

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

    /// <summary>
    ///     是否包含已禁用记录（IsValid=0）
    ///     false（默认）：分页查询硬过滤 IsValid=1
    ///     true：跳过 IsValid 硬过滤（ShowDisabled 开关使用）
    /// </summary>
    public bool IncludeDisabled { get; set; }
}

public class FilterItem
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";  // eq/neq/gt/lt/like/in
    public object? Value { get; set; }
}
