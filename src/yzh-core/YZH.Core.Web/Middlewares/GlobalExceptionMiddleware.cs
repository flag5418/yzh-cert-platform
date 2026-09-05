using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace YZH.Core.Web.Middlewares;

/// <summary>
///     全局异常处理中间件
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private const int SlowRequestThresholdMs = 5000;

    public GlobalExceptionMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                LogWarning($"慢请求: {context.Request.Method} {context.Request.Path} | 耗时: {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError(ex, $"全局异常: {context.Request.Method} {context.Request.Path}");

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";

                var isDev = _environment.IsDevelopment();
                var message = isDev ? ex.Message : $"服务器内部错误: {ex.Message}";
                var response = new
                {
                    success = false,
                    message,
                    exceptionType = isDev ? ex.GetType().Name : null,
                    stackTrace = isDev ? ex.StackTrace : null,
                    requestPath = context.Request.Path.Value,
                    elapsedMs = isDev ? stopwatch.ElapsedMilliseconds : 0L
                };

                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await context.Response.WriteAsync(json);
            }
        }
    }

    private static void LogError(Exception ex, string message)
    {
        try { Console.Error.WriteLine("[ERROR] " + message + " | " + ex.GetType().Name + ": " + ex.Message); } catch { }
    }

    private static void LogWarning(string message)
    {
        try { Console.WriteLine("[WARN] " + message); } catch { }
    }
}
