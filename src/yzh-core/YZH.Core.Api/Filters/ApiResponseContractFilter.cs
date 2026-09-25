using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Api.Filters
{
    /// <summary>
    /// 信封不变量运行时守卫（B-R4 / 前后端信封统一改造计划 P3）
    ///
    /// 职责：结果放行前校验 <see cref="ApiResponse{T}"/> / <see cref="ApiResponse"/> 三条不变量，
    ///       违规即抛（开发期立刻 500 暴露，不把坏信封发给前端）：
    ///       ① success:false ⇒ err 非空（失败原因唯一出口）
    ///       ② success:true  ⇒ err 为空（成功不许带错误文本）
    ///       ③ success:false ⇒ message 为空（错误文本只从 err 出，前端单一读取点）
    ///
    /// 说明：ApiResponse 构造已封闭（仅静态工厂能创建），正常代码路径不可能违规；
    ///       本过滤器是**防回潮绊线**——拦住未来绕过工厂的拼装（如反射/序列化回填）。
    ///       非信封响应（匿名对象、P-* 补丁的 Result、文件流等）一律跳过，不参与判定。
    /// </summary>
    public class ApiResponseContractFilter : IResultFilter
    {
        private readonly ILogger<ApiResponseContractFilter> _logger;

        public ApiResponseContractFilter(ILogger<ApiResponseContractFilter> logger)
        {
            _logger = logger;
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not ObjectResult { Value: { } value })
                return;

            var type = value.GetType();
            if (!IsEnvelope(type))
                return;

            var success = (bool)GetProperty(type, value, "Success");
            var err = GetProperty(type, value, "Err") as string ?? string.Empty;
            var message = GetProperty(type, value, "Message") as string ?? string.Empty;

            if (!success && string.IsNullOrWhiteSpace(err))
                Raise(context, type, "① success:false 但 err 为空", value);
            if (success && !string.IsNullOrWhiteSpace(err))
                Raise(context, type, "② success:true 但 err 非空", value);
            if (!success && !string.IsNullOrEmpty(message))
                Raise(context, type, "③ success:false 但 message 非空（错误文本只应从 err 出）", value);
        }

        public void OnResultExecuted(ResultExecutedContext context) { }

        private void Raise(ResultExecutingContext context, Type type, string rule, object value)
        {
            var action = context.ActionDescriptor.DisplayName ?? "(unknown action)";
            _logger.LogError("ApiResponse 契约违规（B-R4 {Rule}）：{Action} → {Value}", rule, action, value);
            throw new InvalidOperationException($"ApiResponse 契约违规（B-R4 {rule}）：{action}");
        }

        private static bool IsEnvelope(Type type) =>
            type == typeof(ApiResponse)
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>));

        private static object? GetProperty(Type type, object value, string name) =>
            type.GetProperty(name)?.GetValue(value);
    }
}
