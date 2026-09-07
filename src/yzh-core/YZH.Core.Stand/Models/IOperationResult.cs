namespace YZH.Core.Stand.Models;

/// <summary>
///     操作结果基接口 — 所有写操作的标准返回值
/// </summary>
public interface IOperationResult
{
    bool Success { get; }
    string Message { get; }
    Exception? Exception { get; }
}

/// <summary>
///     操作结果（带数据）
/// </summary>
public interface IOperationResult<out T> : IOperationResult
{
    T? Data { get; }
}

/// <summary>
///     操作结果实现
/// </summary>
public class OperationResult : IOperationResult
{
    public bool Success { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public Exception? Exception { get; protected set; }

    public static OperationResult Ok(string message = "操作成功")
        => new() { Success = true, Message = message };

    public static OperationResult Fail(string message, Exception? ex = null)
        => new() { Success = false, Message = message, Exception = ex };
}

/// <summary>
///     操作结果实现（带数据）
/// </summary>
public class OperationResult<T> : IOperationResult<T>
{
    public bool Success { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public Exception? Exception { get; protected set; }
    public T? Data { get; protected set; }

    public static OperationResult<T> Ok(T data, string message = "操作成功")
        => new() { Success = true, Message = message, Data = data };

    public static new OperationResult<T> Fail(string message, Exception? ex = null)
        => new() { Success = false, Message = message, Exception = ex };
}

/// <summary>
///     批量操作结果
/// </summary>
public class BatchOperationResult : IOperationResult
{
    public bool Success { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public Exception? Exception { get; protected set; }
    public int SuccessCount { get; protected set; }
    public int FailCount { get; protected set; }
    public List<(string Code, string Error)> Failures { get; protected set; } = new();

    public static BatchOperationResult Ok(int successCount, string message = "批量操作成功")
        => new() { Success = true, SuccessCount = successCount, Message = message };

    public static BatchOperationResult Partial(int successCount, int failCount, List<(string, string)> failures)
        => new() { Success = false, SuccessCount = successCount, FailCount = failCount, Failures = failures, Message = $"成功 {successCount} 条，失败 {failCount} 条" };

    public static BatchOperationResult Fail(string message, Exception? ex = null)
        => new() { Success = false, Message = message, Exception = ex };
}
