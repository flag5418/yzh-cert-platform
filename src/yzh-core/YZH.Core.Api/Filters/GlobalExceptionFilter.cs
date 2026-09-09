using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Exceptions;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Api.Models;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Filters
{
    /// <summary>
    /// 全局异常过滤器
    /// 
    /// 职责：
    /// 1. 捕获 Controller 层异常
    /// 2. 统一错误响应格式
    /// 3. 记录错误日志
    /// 4. 区分开发/生产环境错误信息
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;
        private readonly IHostEnvironment _environment;
        
        public GlobalExceptionFilter(
            ILogger<GlobalExceptionFilter> logger,
            IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }
        
        public void OnException(ExceptionContext context)
        {
            var exception = context.Exception;
            var statusCode = StatusCodes.Status500InternalServerError;
            var errorCode = "INTERNAL_ERROR";
            var message = "服务器内部错误";
            var details = (Dictionary<string, string>)null;
            
            // 判断异常类型（注意顺序：子类必须在基类之前）
            switch (exception)
            {
                case YZHValidationException validationEx:
                    statusCode = validationEx.StatusCode;
                    errorCode = validationEx.ErrorCode;
                    message = validationEx.Message;
                    details = validationEx.FieldErrors;
                    break;

                case YZHForbiddenException:
                    statusCode = 403;
                    errorCode = "FORBIDDEN";
                    message = "权限不足";
                    break;

                case YZHNotFoundException:
                    statusCode = 404;
                    errorCode = "NOT_FOUND";
                    message = "资源不存在";
                    break;

                case YZHDuplicateException:
                    statusCode = 409;
                    errorCode = "DUPLICATE";
                    message = "数据已存在";
                    break;

                case YZHReferencedException:
                    statusCode = 400;
                    errorCode = "REFERENCED";
                    message = "数据被其他业务引用，无法操作";
                    break;

                case YZHBusinessException bizEx:
                    statusCode = bizEx.StatusCode;
                    errorCode = bizEx.ErrorCode;
                    message = bizEx.Message;
                    break;

                default:
                    // 记录详细错误日志
                    _logger.LogError(
                        exception,
                        "未处理异常: {Message}, Type: {Type}, Path: {Path}, Method: {Method}, User: {User}",
                        exception.Message,
                        exception.GetType().FullName,
                        context.HttpContext.Request.Path,
                        context.HttpContext.Request.Method,
                        context.HttpContext.User?.Identity?.Name ?? "anonymous"
                    );
                    break;
            }
            
            // 构建响应
            var response = new ApiResponse<object>
            {
                Success = false,
                Code = statusCode,
                Message = _environment.IsDevelopment() ? message : "操作失败，请联系管理员",
                Timestamp = DateTime.UtcNow,
                Data = details != null ? new { errorCode, fieldErrors = details } : null
            };
            
            context.Result = new ObjectResult(response)
            {
                StatusCode = statusCode
            };
            
            context.ExceptionHandled = true;
        }
    }
}
