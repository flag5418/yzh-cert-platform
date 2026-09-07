namespace YZH.Core.DataBase;

/// <summary>
///     事务执行结果
///     封装事务操作的执行状态和错误信息
///     ExecuteInTransaction 方法统一返回此类型，调用方据此判断后续逻辑
/// </summary>
public class TxResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误消息（失败时）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>异常对象（失败时）</summary>
    public Exception? Exception { get; set; }

    /// <summary>影响行数（部分场景）</summary>
    public int AffectedRows { get; set; }

    public static TxResult Ok(int affectedRows = 0)
        => new() { Success = true, AffectedRows = affectedRows };

    public static TxResult Fail(string message, Exception? ex = null)
        => new() { Success = false, ErrorMessage = message, Exception = ex };
}

/// <summary>
///     事务执行结果（带返回值）
/// </summary>
public class TxResult<T> : TxResult
{
    public T? Data { get; set; }

    public static TxResult<T> Ok(T data, int affectedRows = 0)
        => new() { Success = true, Data = data, AffectedRows = affectedRows };

    public static new TxResult<T> Fail(string message, Exception? ex = null)
        => new() { Success = false, ErrorMessage = message, Exception = ex };
}
