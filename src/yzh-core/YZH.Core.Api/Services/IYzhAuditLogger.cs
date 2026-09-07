namespace YZH.Core.Api.Services;

/// <summary>
///     审计日志服务接口（增强版）
///     对标参考架构 YZH.Stand.Helper.log.LogHelper
///     结构化记录：谁 + 什么时间 + 操作什么 + 结果 + IP
/// </summary>
public interface IYzhAuditLogger
{
    /// <summary>记录信息日志</summary>
    void Info(string action, string? userCode = null, string? userName = null,
              string? ipAddress = null, string? extra = null);

    /// <summary>记录警告日志</summary>
    void Warning(string action, string? userCode = null, string? userName = null,
                 string? message = null);

    /// <summary>记录错误日志</summary>
    void Error(string action, Exception? ex = null, string? userCode = null,
               string? userName = null, string? ipAddress = null,
               string? requestParams = null, int? statusCode = null);

    /// <summary>记录 API 调用审计</summary>
    void LogApiCall(AuditLogEntry entry);

    /// <summary>异步记录（推荐用于请求管道）</summary>
    Task LogApiCallAsync(AuditLogEntry entry);
}

/// <summary>
///     审计日志条目（结构化）
/// </summary>
public class AuditLogEntry
{
    /// <summary>日志ID</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>日志级别</summary>
    public LogLevel Level { get; init; }

    /// <summary>操作时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>操作人编码</summary>
    public string? UserCode { get; init; }

    /// <summary>操作人姓名</summary>
    public string? UserName { get; init; }

    /// <summary>HTTP方法</summary>
    public string? HttpMethod { get; init; }

    /// <summary>请求路径</summary>
    public string? Path { get; init; }

    /// <summary>客户端IP</summary>
    public string? IpAddress { get; init; }

    /// <summary>User-Agent</summary>
    public string? UserAgent { get; init; }

    /// <summary>操作描述</summary>
    public string? Action { get; init; }

    /// <summary>请求参数（脱敏）</summary>
    public string? RequestParams { get; init; }

    /// <summary>响应状态码</summary>
    public int? StatusCode { get; init; }

    /// <summary>执行耗时（毫秒）</summary>
    public long? DurationMs { get; init; }

    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>异常类型</summary>
    public string? ExceptionType { get; init; }

    /// <summary>堆栈跟踪</summary>
    public string? StackTrace { get; init; }

    /// <summary>调用位置（文件:方法:行号）</summary>
    public string? CallerInfo { get; init; }
}

/// <summary>
///     日志级别
/// </summary>
public enum LogLevel
{
    Information,
    Warning,
    Error,
    Critical
}
