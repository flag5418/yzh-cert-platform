using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Filters;

/// <summary>
///     防重复提交过滤器
///     
///     原理：
///     1. 基于请求路径 + 请求体内容 + 用户 Token 生成唯一指纹
///     2. 使用 MemoryCache（单实例）或 Redis（分布式）存储请求指纹
///     3. 设置滑动过期时间（默认 5 秒），过期后自动移除
///     4. 如果指纹已存在，返回 409 Conflict
///     
///     使用方式：
///     - 全局注册：所有 POST/PUT/DELETE 自动防重
///     - 单独标记：[Idempotent(Seconds = 10)] 自定义过期时间
///     - 跳过防重：[Idempotent(Ignore = true)]
/// </summary>
public class IdempotentFilter : IAsyncActionFilter
{
    private readonly IMemoryCache _cache;
    private readonly int _seconds;

    public IdempotentFilter(IMemoryCache cache, int seconds = 5)
    {
        _cache = cache;
        _seconds = seconds;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. 检查各种"跳过防重"标记
        var ignoreAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<IdempotentIgnoreAttribute>()
            .FirstOrDefault();
        if (ignoreAttr != null)
        {
            await next();
            return;
        }

        // 检查 [Idempotent(Ignore = true)]
        var idempotentAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<IdempotentAttribute>()
            .FirstOrDefault();
        if (idempotentAttr?.Ignore == true)
        {
            await next();
            return;
        }

        // 2. 只拦截写操作（POST/PUT/DELETE）
        var method = context.HttpContext.Request.Method;
        if (method != "POST" && method != "PUT" && method != "DELETE")
        {
            await next();
            return;
        }

        // 3. 获取自定义过期时间
        var secondsAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<IdempotentSecondsAttribute>()
            .FirstOrDefault();
        var expireSeconds = secondsAttr?.Seconds ?? idempotentAttr?.Seconds ?? _seconds;

        // 4. 生成请求指纹
        var fingerprint = GenerateFingerprint(context);

        // 5. 检查是否已存在
        if (_cache.TryGetValue(fingerprint, out _))
        {
            // 重复请求，返回 409
            context.Result = new ObjectResult(ApiResponse.Fail("请勿重复提交，请稍后重试", 409))
            {
                StatusCode = 409
            };
            return;
        }

        // 6. 记录指纹（带过期时间）
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromSeconds(expireSeconds))
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(expireSeconds * 2));

        _cache.Set(fingerprint, true, cacheEntryOptions);

        // 7. 执行 Action
        await next();
    }

    /// <summary>
    ///     生成请求指纹（基于 路径 + 用户 + 请求体哈希）
    /// </summary>
    private static string GenerateFingerprint(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;

        // 获取用户标识
        var userId = context.HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

        // 请求路径
        var path = request.Path.Value ?? request.Path.ToString();

        // 读取请求体（如果是 JSON）
        var bodyHash = "";
        if (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            // 从参数中序列化生成哈希
            var sb = new StringBuilder();
            foreach (var arg in context.ActionArguments)
            {
                sb.Append(arg.Key);
                sb.Append(arg.Value?.ToString());
            }
            bodyHash = ComputeHash(sb.ToString());
        }

        return $"idem:{userId}:{path}:{bodyHash}";
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes)[..16]; // 取前16位即可
    }
}

/// <summary>
///     标记方法/控制器跳过防重复提交
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class IdempotentIgnoreAttribute : Attribute
{
}

/// <summary>
///     自定义防重复提交过期时间
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class IdempotentSecondsAttribute : Attribute
{
    public int Seconds { get; }

    public IdempotentSecondsAttribute(int seconds)
    {
        Seconds = seconds;
    }
}

/// <summary>
///     防重复提交快捷特性（组合 Ignore 和 Seconds）
///     用法：[Idempotent(Seconds = 10)] 或 [Idempotent(Ignore = true)]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class IdempotentAttribute : Attribute
{
    public int Seconds { get; set; } = 5;
    public bool Ignore { get; set; }
}
