using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace YZH.Core.Filters
{
    /// <summary>
    /// YZH ActionFilter 扩展模板
    /// 
    /// 设计约束：
    /// - 必须使用 IAsyncActionFilter（而非 IActionFilter 或中间件）
    /// - 与 Vol 的 ActionPermissionFilter 风格保持一致
    /// - 通过 Attribute 声明式标记，DI 注入服务
    /// 
    /// 使用方式：
    /// // 方式一：作为全局 Filter 注册
    /// options.Filters.Add<YZHXxxActionFilter>(int.MinValue + 100);
    /// 
    /// // 方式二：通过特性标记
    /// [YZHXxxFilter(DurationSeconds = 3)]
    /// public async Task<IActionResult> Save() { ... }
    /// </summary>

    // ════════════════════════════════════════════════════════════
    // 1. 特性定义（声明式配置）
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// YZH 自定义 Filter 特性 - 标记在 Controller 或 Action 上
    /// 
    /// 设计要点：
    /// - 继承 Attribute 和 IFilterMetadata（标记接口）
    /// - 参数通过构造函数或属性注入
    /// - 类级别标记对所有 Action 生效，方法级别可覆盖
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class YZHXxxFilterAttribute : Attribute, IFilterMetadata
    {
        /// <summary>功能参数示例</summary>
        public int DurationSeconds { get; set; } = 3;

        /// <summary>提示信息</summary>
        public string Message { get; set; } = "操作处理中";

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; } = true;
    }

    // ════════════════════════════════════════════════════════════
    // 2. ActionFilter 实现
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// YZH 自定义 ActionFilter - 实现 IAsyncActionFilter
    /// 
    /// 设计要点：
    /// - 通过构造函数注入依赖服务（IServiceProvider / ILogger 等）
    /// - 作为全局 Filter 注册时，对所有请求生效
    /// - 通过检查 context.ActionDescriptor 上的特性来决定是否执行逻辑
    /// - 异常时不阻塞请求（可用性优先）
    /// </summary>
    public class YZHXxxActionFilter : IAsyncActionFilter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<YZHXxxActionFilter> _logger;

        public YZHXxxActionFilter(
            IServiceProvider serviceProvider,
            ILogger<YZHXxxActionFilter> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            // ── 1. 获取特性配置 ──
            var attr = GetFilterAttribute(context);
            if (attr == null || !attr.Enabled)
            {
                // 未标记特性，跳过
                await next();
                return;
            }

            // ── 2. 前置处理 ──
            try
            {
                // 示例：检查条件
                if (!await CanProceedAsync(context, attr))
                {
                    // 条件不满足，短路返回
                    context.Result = new ObjectResult(new
                    {
                        Status = false,
                        Message = attr.Message
                    })
                    {
                        StatusCode = 409
                    };
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[YZHXxx] 前置处理异常: {Message}", ex.Message);
                // 异常时不阻塞请求，确保可用性优先
                await next();
                return;
            }

            // ── 3. 执行 Action ──
            var executedContext = await next();

            // ── 4. 后置处理（可选） ──
            try
            {
                await OnAfterExecutionAsync(executedContext, attr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[YZHXxx] 后置处理异常: {Message}", ex.Message);
                // 后置处理异常不影响响应
            }
        }

        /// <summary>
        /// 从 Action 上下文获取特性配置
        /// 优先级：方法级 > 类级
        /// </summary>
        private YZHXxxFilterAttribute GetFilterAttribute(ActionExecutingContext context)
        {
            // 先查方法级别
            var methodAttr = context.ActionDescriptor.EndpointMetadata
                .OfType<YZHXxxFilterAttribute>()
                .FirstOrDefault();

            if (methodAttr != null)
                return methodAttr;

            // 再查类级别
            var controllerAttr = context.Controller.GetType()
                .GetCustomAttributes(typeof(YZHXxxFilterAttribute), true)
                .FirstOrDefault() as YZHXxxFilterAttribute;

            return controllerAttr;
        }

        /// <summary>
        /// 前置检查逻辑
        /// </summary>
        private async Task<bool> CanProceedAsync(
            ActionExecutingContext context,
            YZHXxxFilterAttribute attr)
        {
            // TODO: 实现具体检查逻辑
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// 后置处理逻辑
        /// </summary>
        private async Task OnAfterExecutionAsync(
            ActionExecutedContext context,
            YZHXxxFilterAttribute attr)
        {
            // TODO: 实现具体后置逻辑
            await Task.CompletedTask;
        }
    }
}
