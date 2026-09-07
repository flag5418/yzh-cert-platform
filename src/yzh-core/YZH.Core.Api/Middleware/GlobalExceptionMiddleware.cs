using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Exceptions;
using YZH.Core.Api.Models;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 
    /// 职责：
    /// 1. 捕获所有未处理异常
    /// 2. 统一错误响应格式
    /// 3. 记录错误日志
    /// 4. 开发环境返回详细错误，生产环境返回通用错误
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }
        
        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = StatusCodes.Status500InternalServerError;
            var errorCode = "INTERNAL_ERROR";
            var message = "服务器内部错误";
            var details = (Dictionary<string, string>)null;
            
            // 判断异常类型，返回对应的状态码和错误信息
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
                        exception.GetType().Name,
                        context.Request.Path,
                        context.Request.Method,
                        context.User?.Identity?.Name ?? "anonymous"
                    );
                    break;
            }
            
            // 构建响应
            var response = new
            {
                success = false,
                code = statusCode,
                message,
                error = errorCode,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                path = context.Request.Path,
                details = details
            };
            
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = statusCode;
            
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    }
    
    /// <summary>
    /// 中间件扩展方法
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalException(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
