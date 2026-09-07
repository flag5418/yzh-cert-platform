using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using LogLevel = YZH.Core.Api.Services.LogLevel;
using MELLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace YZH.Core.Api.Filters;

/// <summary>
///     全局 API 审计过滤器
///     对标 Ape.Volo 的 AuditingFilter
///     记录所有 API 调用的：谁 + 什么时间 + 操作什么 + 结果 + IP
/// </summary>
public class YzhAuditingFilter : IAsyncActionFilter
{
    private readonly IYzhAuditLogger _auditLogger;
    private readonly IUserContext _userContext;
    private readonly ILogger<YzhAuditingFilter> _logger;

    // 不需要审计的路径前缀
    private static readonly string[] ExcludedPaths =
    ["/api/health", "/swagger", "/favicon"];

    public YzhAuditingFilter(
        IYzhAuditLogger auditLogger,
        IUserContext userContext,
        ILogger<YzhAuditingFilter> logger)
    {
        _auditLogger = auditLogger;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var path = context.HttpContext.Request.Path.Value ?? "";

        // 排除不需要审计的路径
        if (IsExcluded(path))
        {
            await next();
            return;
        }

        var startTime = DateTime.UtcNow;
        var userCode = _userContext.UserCode;
        var userName = _userContext.UserName;
        var ipAddress = _userContext.ClientIp;
        var httpMethod = context.HttpContext.Request.Method;

            // 记录请求参数（脱敏）
            var requestParams = SerializeParams(context.ActionArguments.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value));

        _logger.LogInformation("[Audit] 开始执行 {Action} | User={UserCode} | IP={IpAddress}",
            context.ActionDescriptor.DisplayName, userCode, ipAddress);

        try
        {
            // 执行实际方法
            var executedContext = await next();

            // 记录成功日志
            await _auditLogger.LogApiCallAsync(new AuditLogEntry
            {
                Level = LogLevel.Information,
                Timestamp = startTime,
                UserCode = userCode,
                UserName = userName,
                HttpMethod = httpMethod,
                Path = path,
                IpAddress = ipAddress,
                UserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString(),
                Action = context.ActionDescriptor.DisplayName,
                RequestParams = requestParams,
                StatusCode = executedContext.HttpContext.Response.StatusCode,
                DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                IsSuccess = true
            });
        }
        catch (Exception ex)
        {
            // 记录错误日志
            await _auditLogger.LogApiCallAsync(new AuditLogEntry
            {
                Level = LogLevel.Error,
                Timestamp = startTime,
                UserCode = userCode,
                UserName = userName,
                HttpMethod = httpMethod,
                Path = path,
                IpAddress = ipAddress,
                UserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString(),
                Action = context.ActionDescriptor.DisplayName,
                RequestParams = requestParams,
                StatusCode = 500,
                DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ExceptionType = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                CallerInfo = GetCallerInfo()
            });

            throw;
        }
    }

    private static bool IsExcluded(string path)
    {
        return ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string SerializeParams(Dictionary<string, object> arguments)
    {
        try
        {
            // 脱敏处理（过滤密码等敏感字段）
            var filtered = arguments.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Key.ToLower().Contains("password")
                    || kvp.Key.ToLower().Contains("secret")
                    || kvp.Key.ToLower().Contains("token")
                    ? "***"
                    : kvp.Value?.ToString() ?? "null"
            );
            return System.Text.Json.JsonSerializer.Serialize(filtered);
        }
        catch
        {
            return "[序列化失败]";
        }
    }

    private static string GetCallerInfo()
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(true);
            var frame = st.GetFrame(2); // 跳过 GetCallerInfo 和 OnActionExecutionAsync
            if (frame == null) return "未知位置";

            var method = frame.GetMethod();
            var fileName = frame.GetFileName();
            var lineNumber = frame.GetFileLineNumber();

            var shortFileName = !string.IsNullOrEmpty(fileName)
                ? System.IO.Path.GetFileName(fileName)
                : "未知文件";

            var className = method?.DeclaringType?.Name ?? "未知类";
            var methodName = method?.Name ?? "未知方法";

            return lineNumber > 0
                ? $"{className}.{methodName} | {shortFileName} | 行号:{lineNumber}"
                : $"{className}.{methodName}";
        }
        catch
        {
            return "未知位置";
        }
    }
}
