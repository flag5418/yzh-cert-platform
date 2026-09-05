using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace YZH.Core.Exceptions;

/// <summary>
/// 全局异常过滤器。
/// 作用范围：所有 Controller 的所有 Action。
/// 当 Service 层的 try-catch 遗漏时，这里做最终兜底。
///
/// 注册方式（Program.cs 或 Startup.cs）：
///   services.AddControllers()
///       .AddMvcOptions(options => options.Filters.Add&lt;AppExceptionFilter&gt;());
/// </summary>
public class AppExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var response = ExceptionSanitizer.Sanitize(context.Exception, "操作");

        var env = context.HttpContext.RequestServices
            .GetService<IWebHostEnvironment>();

        context.Result = new ObjectResult(new
        {
            Status = false,
            Message = response.Message,
            // 开发环境下附带错误类型名（不附带堆栈和 SQL）
            ErrorType = env?.EnvironmentName == "Development"
                ? context.Exception.GetType().Name
                : null
        })
        { StatusCode = 200 };

        context.ExceptionHandled = true;
    }
}
