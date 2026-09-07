namespace YZH.Core.DataBase.Sql;

/// <summary>
///     数据库方言接口
///     处理不同数据库在分页、引号、参数、函数等方面的差异
/// </summary>
public interface IDatabaseDialect
{
    /// <summary>数据库类型</summary>
    DatabaseType DbType { get; }

    /// <summary>参数前缀（@ 或 :）</summary>
    string ParameterPrefix { get; }

    /// <summary>标识符引号符（表名/列名）</summary>
    (string Open, string Close) IdentifierQuotes { get; }

    /// <summary>字符串字面量引号</summary>
    string StringQuote { get; }

    /// <summary>生成分页 SQL</summary>
    /// <param name="baseSql">基础查询 SQL（不含分页）</param>
    /// <param name="offset">偏移量（从 0 开始）</param>
    /// <param name="limit">每页数量</param>
    string BuildPaginationSql(string baseSql, int offset, int limit);

    /// <summary>生成分页 SQL（带总数量查询）</summary>
    string BuildCountSql(string baseSql);

    /// <summary>转义标识符（表名/列名）</summary>
    string EscapeIdentifier(string identifier);

    /// <summary>转义字符串值（防注入）</summary>
    string EscapeString(string value);

    /// <summary>获取当前时间函数</summary>
    string NowFunction { get; }

    /// <summary>字符串连接函数</summary>
    string ConcatFunction(IEnumerable<string> expressions);

    /// <summary>转换日期为字符串</summary>
    string DateFormatFunction(string column, string format);

    /// <summary>判断列是否存在子字符串</summary>
    string ContainsFunction(string column, string value);

    /// <summary>限制返回行数（Top N）</summary>
    string BuildTopSql(string baseSql, int topN);
}

/// <summary>
///     新增分页请求规范
/// </summary>
public class OffsetPager
{
    /// <summary>页码（从 1 开始）</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>每页数量</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>排序字段</summary>
    public string? OrderBy { get; set; }

    /// <summary>排序方向：ASC / DESC</summary>
    public string OrderDirection { get; set; } = "ASC";

    /// <summary>计算偏移量</summary>
    public int Offset => (PageNumber - 1) * PageSize;
}
