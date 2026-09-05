namespace YZH.Core.Exceptions;

/// <summary>
/// 业务异常。消息内容可以直接展示给前端用户。
/// 使用：throw new AppException("机构编号不允许修改");
/// </summary>
public class AppException : System.Exception
{
    /// <summary>错误码（自动生成，格式：ERR-yyyyMMdd-NNNNNN）</summary>
    public string ErrorCode { get; }

    public AppException(string message) : base(message)
    {
        ErrorCode = $"ERR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }

    public AppException(string message, System.Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = $"ERR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }
}

/// <summary>
/// 数据库异常（脱敏后展示给前端）。
/// 由 ExceptionSanitizer 从原始 DbException 转换而来。
/// </summary>
public class DbException : AppException
{
    /// <summary>原始异常类型名（用于日志，不返回给前端）</summary>
    public string OriginalExceptionType { get; }

    public DbException(string friendlyMessage, System.Exception original)
        : base(friendlyMessage, original)
    {
        OriginalExceptionType = original?.GetType().Name ?? "Unknown";
    }
}
