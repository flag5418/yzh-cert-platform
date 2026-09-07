using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Exceptions;

namespace YZH.Core.Api.Filters
{
    /// <summary>
    /// 输入验证过滤器
    /// 
    /// 功能：
    /// 1. 自动验证请求参数
    /// 2. 收集字段级错误
    /// 3. 返回统一的验证错误响应
    /// </summary>
    public class ValidationFilter : IActionFilter
    {
        private readonly ILogger<ValidationFilter> _logger;
        
        public ValidationFilter(ILogger<ValidationFilter> logger)
        {
            _logger = logger;
        }
        
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // 验证所有 action 参数
            foreach (var arg in context.ActionArguments)
            {
                var key = arg.Key;
                var value = arg.Value;
                
                if (value == null)
                    continue;
                
                // 跳过简单类型和字符串
                if (IsSimpleType(value))
                    continue;
                
                // 验证对象
                var errors = ValidateObject(value);
                if (errors.Any())
                {
                    _logger.LogWarning(
                        "参数验证失败: {Key}, Errors: {@Errors}",
                        key,
                        errors
                    );
                    
                    throw new YZHValidationException(errors);
                }
            }
        }
        
        public void OnActionExecuted(ActionExecutedContext context)
        {
            // 无需处理
        }
        
        /// <summary>
        /// 验证对象
        /// </summary>
        private Dictionary<string, string> ValidateObject(object obj)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(obj);
            
            Validator.TryValidateObject(obj, context, results, true);
            
            return results
                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "unknown")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => r.ErrorMessage).FirstOrDefault() ?? "验证失败"
                );
        }
        
        /// <summary>
        /// 判断是否为简单类型
        /// </summary>
        private bool IsSimpleType(object obj)
        {
            var type = obj.GetType();
            
            return type.IsPrimitive 
                || type == typeof(string)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid)
                || type == typeof(Enum);
        }
    }
}
