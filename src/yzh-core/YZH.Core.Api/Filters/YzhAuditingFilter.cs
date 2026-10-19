using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Interfaces;
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

        // ★ 两个时间口径分开（2026-10-17 / 铁律十）：
        //   ・审计时间戳（落库/上屏）= 本地时间 DateTime.Now；
        //   ・耗时计算 = UtcNow 差值（属「耗时统计」合法场景，不受时区/校时影响）。
        //   旧实现把 UtcNow 当 Timestamp ⇒ 审计文件/DB 比日志早 8 小时（0–8 点还会跨日）。
        var startedAtUtc = DateTime.UtcNow;
        var startedAtLocal = DateTime.Now;
        var userCode = _userContext.UserCode;
        var userName = _userContext.UserName;
        var ipAddress = _userContext.ClientIp;
        var httpMethod = context.HttpContext.Request.Method;

            // 结构化接口信息：控制器名 + 动作名 + 控制器命名空间（用于「模块」归类）
            var routeValues = context.ActionDescriptor.RouteValues;
            var controllerName = routeValues.TryGetValue("controller", out var c) ? c : null;
            var actionName = routeValues.TryGetValue("action", out var a) ? a : null;
            var controllerNamespace = context.Controller?.GetType().Namespace;

            // 接口名称 / 描述：反射方法上的 [ApiDescription] 特性
            //   （OperationLog「操作」列显示接口名称、Module 列显示业务分组；缺失时下游回退）
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var endpointMethod = descriptor?.MethodInfo;
            var apiName = endpointMethod != null
                ? ApiDescriptionResolver.ResolveName(endpointMethod)
                : null;
            var apiGroup = endpointMethod != null
                ? ApiDescriptionResolver.ResolveGroup(endpointMethod)
                : null;

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
                Timestamp = startedAtLocal,
                UserCode = userCode,
                UserName = userName,
                HttpMethod = httpMethod,
                Path = path,
                IpAddress = ipAddress,
                UserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString(),
                Action = context.ActionDescriptor.DisplayName,
                Controller = controllerName,
                ActionName = actionName,
                ControllerNamespace = controllerNamespace,
                ApiName = apiName,
                ApiGroup = apiGroup,
                RequestParams = requestParams,
                StatusCode = executedContext.HttpContext.Response.StatusCode,
                DurationMs = (long)(DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                IsSuccess = true
            });
        }
        catch (Exception ex)
        {
            // 记录错误日志
            await _auditLogger.LogApiCallAsync(new AuditLogEntry
            {
                Level = LogLevel.Error,
                Timestamp = startedAtLocal,
                UserCode = userCode,
                UserName = userName,
                HttpMethod = httpMethod,
                Path = path,
                IpAddress = ipAddress,
                UserAgent = context.HttpContext.Request.Headers["User-Agent"].ToString(),
                Action = context.ActionDescriptor.DisplayName,
                Controller = controllerName,
                ActionName = actionName,
                ControllerNamespace = controllerNamespace,
                ApiName = apiName,
                ApiGroup = apiGroup,
                RequestParams = requestParams,
                StatusCode = 500,
                DurationMs = (long)(DateTime.UtcNow - startedAtUtc).TotalMilliseconds,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ExceptionType = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                CallerInfo = GetCallerInfo()
            });

            throw;
        }
    }

    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "UserPwd", "Token", "Secret", "ApiKey", "EncryptionKey",
        "IdCard", "Phone", "PhoneNo", "Email", "BankAccount", "Captcha", "VerificationCode"
    };

    private static bool IsExcluded(string path)
    {
        return ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string SerializeParams(Dictionary<string, object> arguments)
    {
        try
        {
            var filtered = arguments.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    // 脱敏：敏感字段替换为 [REDACTED]
                    if (SensitiveFields.Contains(kvp.Key))
                        return "[REDACTED]";
                    return kvp.Value?.ToString() ?? "null";
                }
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
