namespace YZH.Core.DataBase.NoSql;

/// <summary>
///     NoSQL 操作结果封装
///     替代老架构的 out string Err 模式
/// </summary>
public class NoSqlOperationResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误消息（失败时）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>异常对象（失败时）</summary>
    public Exception? Exception { get; set; }

    /// <summary>影响条目数（批量操作时）</summary>
    public int AffectedCount { get; set; }

    public static NoSqlOperationResult Ok(int affectedCount = 0)
        => new() { Success = true, AffectedCount = affectedCount };

    public static NoSqlOperationResult Fail(string message, Exception? ex = null)
        => new() { Success = false, ErrorMessage = message, Exception = ex };
}

/// <summary>
///     NoSQL 操作结果（带数据）
/// </summary>
public class NoSqlOperationResult<T> : NoSqlOperationResult
{
    public T? Data { get; set; }

    public static NoSqlOperationResult<T> Ok(T data, int affectedCount = 0)
        => new() { Success = true, Data = data, AffectedCount = affectedCount };

    public static new NoSqlOperationResult<T> Fail(string message, Exception? ex = null)
        => new() { Success = false, ErrorMessage = message, Exception = ex };
}

/// <summary>
///     NoSQL 查询结果封装
/// </summary>
public class NoSqlQueryResult<T>
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误消息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>查询数据</summary>
    public T? Data { get; set; }

    /// <summary>剩余 TTL（秒），-1 表示永不过期，-2 表示 key 不存在</summary>
    public long? RemainingTtl { get; set; }

    public static NoSqlQueryResult<T> Ok(T data, long? ttl = null)
        => new() { Success = true, Data = data, RemainingTtl = ttl };

    public static NoSqlQueryResult<T> Fail(string message)
        => new() { Success = false, ErrorMessage = message };
}
