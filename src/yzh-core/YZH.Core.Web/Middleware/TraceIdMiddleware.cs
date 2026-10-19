using System.Diagnostics;

namespace YZH.Core.Web.Middleware;

/// <summary>
/// 链路 ID 中间件（框架能力）
///
/// <para>作用：把 W3C TraceId 暴露给**前端与排障**，让三处能对上同一根线：</para>
/// <para>① 响应头 <c>X-Trace-Id</c>（前端报错截图里能直接看到）；</para>
/// <para>② <c>HttpContext.Items["TraceId"]</c>（异常响应的 data.traceId）；</para>
/// <para>③ Serilog 日志行的 <c>[TraceId]</c>（同一 Activity）。</para>
///
/// <para>取值顺序：<c>Activity.Current.TraceId</c>（ASP.NET Core 每请求都有）
/// → 入站 <c>X-Trace-Id</c>（跨服务透传）→ <c>HttpContext.TraceIdentifier</c>（兜底）。</para>
///
/// <para>⚠️ 必须注册在管线**最前**（CORS/Swagger 之前），否则异常响应可能缺头。</para>
/// </summary>
public sealed class TraceIdMiddleware
{
    public const string HeaderName = "X-Trace-Id";
    public const string ItemKey = "TraceId";

    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToHexString();

        if (string.IsNullOrEmpty(traceId))
            traceId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(traceId))
            traceId = context.TraceIdentifier;

        context.Items[ItemKey] = traceId;

        // 响应头在写响应体之前设置；若下游已开始写（如 SignalR 长连接）则忽略失败
        try
        {
            context.Response.Headers[HeaderName] = traceId;
        }
        catch (InvalidOperationException)
        {
            // 响应已开始，无法再写头 —— 不影响主流程
        }

        await _next(context);
    }
}
