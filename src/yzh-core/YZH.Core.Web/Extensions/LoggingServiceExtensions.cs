using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace YZH.Core.Web.Extensions;

/// <summary>
/// 运行日志注册（框架能力）
///
/// <para>★ 运行日志 ≠ 审计日志：</para>
/// <para>· 本类负责**排障**：ILogger/异常堆栈 → <c>Logs/app-*.log</c>（Information+）与
///   <c>Logs/error-*.log</c>（Warning+），按日滚动、保留 31/90 天；</para>
/// <para>· 审计（谁在何时干了什么）仍走 <c>YzhAuditLogger</c> → <c>Logs/Audit/*.log</c> + <c>sys_log</c>，
///   两条路互不替代（口径见 docs/40-实施/分析报告/XiHan日志体系借鉴与当前后端日志缺口评估-V1.md）。</para>
///
/// <para>配置：<c>Logging:Directory</c>（可选，默认 ContentRoot/Logs）。生产容器须把该目录挂 volume。</para>
/// <para>⛔ 不要写 <c>AppContext.BaseDirectory</c>：那里是 bin/Debug/net8.0，dotnet clean 会清空。</para>
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// 单行模板：时间 | 级别 | TraceId | 来源类 | 消息（异常另起堆栈块）。
    /// <para>TraceId 由 Serilog 从当前 <c>Activity.Current</c> 自动填充（W3C 32 位十六进制）；
    /// 无 Activity 时渲染为空（如纯后台线程），与 <c>X-Trace-Id</c> 口径一致。</para>
    /// </summary>
    public const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{TraceId}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>解析日志目录（相对路径按进程当前目录解析），并确保目录存在</summary>
    public static string ResolveLogDirectory(IConfiguration? configuration = null)
    {
        var configured = configuration?["Logging:Directory"];
        var baseDir = Directory.GetCurrentDirectory();

        var dir = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(baseDir, "Logs")
            : Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(baseDir, configured);

        dir = Path.GetFullPath(dir);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// 挂载文件日志：app-（Information+，31 天）+ error-（Warning+，90 天），单文件 100 MB 滚动。
    /// </summary>
    public static WebApplicationBuilder AddYzhFileLogging(this WebApplicationBuilder builder)
    {
        var logDir = ResolveLogDirectory(builder.Configuration);
        var appLog = Path.Combine(logDir, "app-.log");
        var errorLog = Path.Combine(logDir, "error-.log");
        var minimumLevel = ParseLevel(builder.Configuration["Logging:LogLevel:Default"]);

        Console.WriteLine($"[YZH] 运行日志目录 = {logDir}（最低级别 {minimumLevel}）");

        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration
                .MinimumLevel.Is(minimumLevel)
                // 压掉框架噪声（与 appsettings 原口径一致），避免 MVC 每条请求 7–9 行
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: OutputTemplate)
                .WriteTo.File(
                    path: appLog,
                    outputTemplate: OutputTemplate,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31,
                    fileSizeLimitBytes: 100 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    shared: true)
                .WriteTo.File(
                    path: errorLog,
                    outputTemplate: OutputTemplate,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 90,
                    fileSizeLimitBytes: 100 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    restrictedToMinimumLevel: LogEventLevel.Warning,
                    shared: true);
        });

        return builder;
    }

    private static LogEventLevel ParseLevel(string? level) =>
        Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;
}
