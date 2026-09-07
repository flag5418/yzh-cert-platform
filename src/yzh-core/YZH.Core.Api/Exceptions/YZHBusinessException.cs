using System;
using System.Collections.Generic;

namespace YZH.Core.Api.Exceptions
{
    /// <summary>
    /// YZH 业务异常基类
    /// 用于抛出自定义业务错误，携带状态码和错误码
    /// </summary>
    public class YZHBusinessException : Exception
    {
        /// <summary>HTTP 状态码</summary>
        public int StatusCode { get; }
        
        /// <summary>业务错误码</summary>
        public string ErrorCode { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="statusCode">HTTP 状态码，默认 400</param>
        /// <param name="errorCode">业务错误码，默认 BUSINESS_ERROR</param>
        public YZHBusinessException(
            string message, 
            int statusCode = 400, 
            string errorCode = "BUSINESS_ERROR")
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
        
        /// <summary>
        /// 构造函数（带 inner exception）
        /// </summary>
        public YZHBusinessException(
            string message, 
            Exception inner,
            int statusCode = 400, 
            string errorCode = "BUSINESS_ERROR")
            : base(message, inner)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }
    
    /// <summary>
    /// 参数校验异常
    /// 用于抛出参数校验失败错误，携带字段级错误信息
    /// </summary>
    public class YZHValidationException : YZHBusinessException
    {
        /// <summary>字段级错误映射</summary>
        public Dictionary<string, string> FieldErrors { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="fieldErrors">字段错误映射</param>
        public YZHValidationException(Dictionary<string, string> fieldErrors)
            : base("参数校验失败", 422, "VALIDATION_ERROR")
        {
            FieldErrors = fieldErrors ?? new Dictionary<string, string>();
        }
        
        /// <summary>
        /// 构造函数（单字段错误）
        /// </summary>
        public YZHValidationException(string field, string error)
            : base($"字段 {field} 校验失败: {error}", 422, "VALIDATION_ERROR")
        {
            FieldErrors = new Dictionary<string, string> { [field] = error };
        }
    }
    
    /// <summary>
    /// 权限不足异常
    /// 用于抛出权限校验失败错误
    /// </summary>
    public class YZHForbiddenException : YZHBusinessException
    {
        public YZHForbiddenException(string message = "权限不足")
            : base(message, 403, "FORBIDDEN")
        {
        }
    }
    
    /// <summary>
    /// 资源不存在异常
    /// 用于抛出资源不存在错误
    /// </summary>
    public class YZHNotFoundException : YZHBusinessException
    {
        public YZHNotFoundException(string message = "资源不存在")
            : base(message, 404, "NOT_FOUND")
        {
        }
    }
    
    /// <summary>
    /// 数据已存在异常
    /// 用于抛出数据重复错误
    /// </summary>
    public class YZHDuplicateException : YZHBusinessException
    {
        public YZHDuplicateException(string message = "数据已存在")
            : base(message, 409, "DUPLICATE")
        {
        }
    }
    
    /// <summary>
    /// 数据引用异常
    /// 用于抛出数据被引用无法删除错误
    /// </summary>
    public class YZHReferencedException : YZHBusinessException
    {
        public YZHReferencedException(string message = "数据被其他业务引用，无法删除")
            : base(message, 400, "REFERENCED")
        {
        }
    }
    
    /// <summary>
    /// 系统错误异常
    /// 用于抛出系统内部错误
    /// </summary>
    public class YZHSystemException : YZHBusinessException
    {
        public YZHSystemException(string message = "系统内部错误", Exception? inner = null)
            : base(message, inner, 500, "SYSTEM_ERROR")
        {
        }
    }
}
