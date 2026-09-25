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
            int httpStatus;
            string err;
            object data;

            // 判断异常类型（注意顺序：子类必须在基类之前）
            // ★ 信封统一（P0-b）：业务异常 → HTTP 200 + err 原文；
            //   只有「系统异常」（未预期异常）才 500 + 脱敏。
            //   权限不足属于基础设施信号，保持 403（22 §三矩阵）。
            switch (exception)
            {
                case YZHForbiddenException forbiddenEx:
                    httpStatus = StatusCodes.Status403Forbidden;
                    err = string.IsNullOrWhiteSpace(forbiddenEx.Message) ? "权限不足" : forbiddenEx.Message;
                    data = new { errorCode = forbiddenEx.ErrorCode };
                    break;

                case YZHValidationException validationEx:
                    // 校验失败：HTTP 200，字段级错误随 data 回传
                    httpStatus = StatusCodes.Status200OK;
                    err = validationEx.Message;
                    data = new { errorCode = validationEx.ErrorCode, fieldErrors = validationEx.FieldErrors };
                    break;

                case YZHBusinessException bizEx:
                    // 已知业务语义（NotFound / Duplicate / Referenced / Business…）
                    // → HTTP 200 + 原文（用户必须看到「机构不存在」这类真实原因）
                    httpStatus = StatusCodes.Status200OK;
                    err = bizEx.Message;
                    data = new { errorCode = bizEx.ErrorCode };
                    break;

                default:
                    // 系统异常：记详细日志（永不外泄给用户）
                    _logger.LogError(
                        exception,
                        "未处理异常: {Message}, Type: {Type}, Path: {Path}, Method: {Method}, User: {User}",
                        exception.Message,
                        exception.GetType().FullName,
                        context.HttpContext.Request.Path,
                        context.HttpContext.Request.Method,
                        context.HttpContext.User?.Identity?.Name ?? "anonymous"
                    );
                    httpStatus = StatusCodes.Status500InternalServerError;
                    // ★ 只脱敏系统异常：业务异常上面已按原文返回，不受环境影响
                    err = _environment.IsDevelopment()
                        ? exception.Message
                        : "操作失败，请联系管理员";
                    data = new { errorCode = "INTERNAL_ERROR" };
                    break;
            }

            // code 与 HTTP 解耦：业务拒绝恒 400，系统异常 500，403 保留 403（22 §三矩阵）
            var envelopeCode = httpStatus == StatusCodes.Status200OK
                ? 400
                : httpStatus;

            var response = ApiResponse<object>.FromError(err, envelopeCode, data);

            context.Result = new ObjectResult(response)
            {
                StatusCode = httpStatus
            };

            context.ExceptionHandled = true;
        }
    }
}
