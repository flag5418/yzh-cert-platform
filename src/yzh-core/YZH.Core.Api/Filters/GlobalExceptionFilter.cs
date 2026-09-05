using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Filters;

/// <summary>
///     全局异常过滤器（Controller 层最终兜底）
///     当中间件未捕获异常时（如非管道内异常），此过滤器作为二次保护
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionFilter(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled) return;

        var isDev = _environment.EnvironmentName?.Equals("Development", StringComparison.OrdinalIgnoreCase) ?? false;
        var message = isDev
            ? context.Exception.ToString()
            : $"服务器内部错误: {context.Exception.Message}";

        context.Result = new ObjectResult(ApiResponse.Error(message))
        {
            StatusCode = 500
        };

        context.ExceptionHandled = true;
    }
}
