namespace YZH.Core.DataBase;

/// <summary>
///     仓储操作结果
///     封装 Repository 层的操作状态和错误信息
///     所有仓储方法返回此类型，调用方据此判断后续逻辑
/// </summary>
public class RepoResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误消息（失败时）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>异常对象（失败时）</summary>
    public Exception? Exception { get; set; }

    /// <summary>影响行数</summary>
    public int AffectedRows { get; set; }

    public static RepoResult Ok(int affectedRows = 0)
        => new() { Success = true, AffectedRows = affectedRows };

    public static RepoResult Fail(string message, Exception? ex = null)
        => new() { Success = false, ErrorMessage = message, Exception = ex };
}

/// <summary>
///     仓储操作结果（带返回数据）
/// </summary>
public class RepoResult<T> : RepoResult
{
    public T? Data { get; set; }

    public static RepoResult<T> Ok(T data, int affectedRows = 0)
        => new() { Success = true, Data = data, AffectedRows = affectedRows };

    public static new RepoResult<T> Fail(string message, Exception? ex = null)
        => new() { Success = false, ErrorMessage = message, Exception = ex };
}
