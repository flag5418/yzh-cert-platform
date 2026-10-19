using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     YZH 审计日志实现（基于 Channel 的异步队列）
///     对标参考架构 YZH.Stand.Helper.log.LogHelper
///     支持结构化日志：谁 + 什么时间 + 操作什么 + 结果 + IP
/// </summary>
public class YzhAuditLogger : BackgroundService, IYzhAuditLogger
{
    private readonly ILogger<YzhAuditLogger> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentQueue<AuditLogEntry> _queue = new();

    /// <summary>保留天数（审计文本文件），默认 31 天；配置键 <c>Audit:KeepDays</c></summary>
    private readonly int _keepDays;

    /// <summary>上次清理日期（本地时区），每天只扫一次目录</summary>
    private DateOnly _lastCleanupDate = DateOnly.MinValue;

    /// <summary>数据库写入回调（由上层注入）</summary>
    public Func<AuditLogEntry, Task>? DbWriter { get; set; }

    public YzhAuditLogger(
        ILogger<YzhAuditLogger> logger,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
        _keepDays = int.TryParse(configuration["Audit:KeepDays"], out var days) && days > 0 ? days : 31;
    }

    // ==================== IYzhAuditLogger 实现 ====================

    public void Info(string action, string? userCode = null, string? userName = null,
                     string? ipAddress = null, string? extra = null)
    {
        var entry = new AuditLogEntry
        {
            Level = LogLevel.Information,
            Action = action,
            UserCode = userCode,
            UserName = userName,
            IpAddress = ipAddress,
            IsSuccess = true,
            Timestamp = DateTime.Now
        };

        Enqueue(entry);
        WriteToConsole(entry);
    }

    public void Warning(string action, string? userCode = null, string? userName = null,
                        string? message = null)
    {
        var entry = new AuditLogEntry
        {
            Level = LogLevel.Warning,
            Action = action,
            UserCode = userCode,
            UserName = userName,
            ErrorMessage = message,
            IsSuccess = false,
            Timestamp = DateTime.Now
        };

        Enqueue(entry);
        WriteToConsole(entry);
    }

    public void Error(string action, Exception? ex = null, string? userCode = null,
                      string? userName = null, string? ipAddress = null,
                      string? requestParams = null, int? statusCode = null)
    {
        var entry = new AuditLogEntry
        {
            Level = LogLevel.Error,
            Action = action,
            UserCode = userCode,
            UserName = userName,
            IpAddress = ipAddress,
            RequestParams = requestParams,
            StatusCode = statusCode,
            IsSuccess = false,
            ErrorMessage = ex?.Message,
            ExceptionType = ex?.GetType().FullName,
            StackTrace = ex?.StackTrace,
            Timestamp = DateTime.Now
        };

        Enqueue(entry);
        WriteToConsole(entry);
        _logger.LogError(ex, "[Audit] {Action} 失败 | User={UserCode} | IP={IpAddress}",
            action, userCode, ipAddress);
    }

    public void LogApiCall(AuditLogEntry entry)
    {
        Enqueue(entry);
    }

    public Task LogApiCallAsync(AuditLogEntry entry)
    {
        Enqueue(entry);
        return Task.CompletedTask;
    }

    // ==================== 后台服务 ====================

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[AuditLogger] 后台服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeue(out var entry))
                {
                    // 写入不跟随停止令牌：停机时已入队的审计必须落盘（丢失审计比多写一行严重）
                    await WriteToFileAsync(entry, CancellationToken.None);
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 正常停机：⛔ 不要记成错误（旧实现每天关机都往 error 日志写一条 TaskCanceledException 噪声）
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditLogger] 写入日志失败");
            }
        }

        _logger.LogInformation("[AuditLogger] 后台服务已停止");
    }

    // ==================== 私有方法 ====================

    /// <summary>
    /// 入队（**单消费者**）。
    ///
    /// <para>★ 2026-10-17 修正：旧实现除后台 <c>ExecuteAsync</c> 外，还在每次入队时
    /// <c>Task.Run(FlushQueueAsync)</c>，两个消费者同时 <c>TryDequeue</c> ⇒ 顺序不保证、可能丢行。
    /// 现在只有 <c>ExecuteAsync</c> 消费；后台服务是单例，队列本身无锁安全。</para>
    /// </summary>
    private void Enqueue(AuditLogEntry entry) => _queue.Enqueue(entry);

    private async Task WriteToFileAsync(AuditLogEntry entry, CancellationToken token)
    {
        try
        {
            var logDir = Path.Combine(_environment.ContentRootPath, "Logs", "Audit");
            Directory.CreateDirectory(logDir);

            // ★ 本地时间（铁律十）：旧实现用 UtcNow 命名，0:00–8:00 的日志会写进「前一天」文件，
            //   与 DB 侧 CreateTime（Now）跨日不一致，按日期检索会漏数。
            var logFile = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            var logLine = FormatLogEntry(entry);

            await File.AppendAllTextAsync(logFile, logLine + Environment.NewLine, token);

            CleanupOldLogsIfNeeded(logDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditLogger] 文件写入失败");
        }

        // 调用数据库写入回调
        if (DbWriter != null)
        {
            try
            {
                await DbWriter(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuditLogger] 数据库写入回调失败: {Action}", entry.Action);
            }
        }
        else
        {
            _logger.LogWarning("[AuditLogger] DbWriter 未设置，跳过数据库写入");
        }
    }

    private void WriteToConsole(AuditLogEntry entry)
    {
        try
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = entry.Level switch
            {
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.Gray
            };

            Console.WriteLine(FormatLogEntry(entry));
            Console.ForegroundColor = originalColor;
        }
        catch { /* 忽略控制台写入失败 */ }
    }

    /// <summary>
    /// 单行审计格式（2026-10-17）：旧实现每条占 8–12 行框线文本块，<c>grep</c>/<c>awk</c> 无法按请求聚合。
    /// <para>格式：<c>时间 | 级别 | 用户 | 操作 | 状态 | 耗时 | IP | 错误?</c>，堆栈另起块（保留首行可检索）。</para>
    /// </summary>
    private static string FormatLogEntry(AuditLogEntry entry)
    {
        var level = entry.Level switch
        {
            LogLevel.Error => "ERR",
            LogLevel.Warning => "WRN",
            LogLevel.Critical => "FTL",
            _ => "INF"
        };

        var sb = new StringBuilder();
        sb.Append($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | {level} | {entry.UserName ?? entry.UserCode ?? "anonymous"}")
          .Append($" | {entry.Action}")
          .Append($" | {entry.StatusCode?.ToString() ?? "-"}")
          .Append($" | {(entry.DurationMs.HasValue ? $"{entry.DurationMs}ms" : "-")}")
          .Append($" | {entry.IpAddress ?? "-"}");

        if (!string.IsNullOrEmpty(entry.ErrorMessage))
            sb.Append($" | 错误={entry.ErrorMessage}");

        if (!string.IsNullOrEmpty(entry.StackTrace))
            sb.Append(Environment.NewLine).Append(entry.StackTrace);

        return sb.ToString();
    }

    /// <summary>每天清理一次超期审计文件（默认保留 31 天，配置 <c>Audit:KeepDays</c>）</summary>
    private void CleanupOldLogsIfNeeded(string logDir)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_lastCleanupDate == today) return;
        _lastCleanupDate = today;

        try
        {
            var cutoff = DateTime.Now.AddDays(-_keepDays);
            foreach (var file in Directory.EnumerateFiles(logDir, "*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AuditLogger] 审计日志清理失败（目录 {Dir}）", logDir);
        }
    }
}
