using System.Collections.Concurrent;
using System.Text;
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
    private readonly ConcurrentQueue<AuditLogEntry> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _flushing;

    public YzhAuditLogger(
        ILogger<YzhAuditLogger> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
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
            Timestamp = DateTime.UtcNow
        };

        EnqueueAndTryFlush(entry);
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
            Timestamp = DateTime.UtcNow
        };

        EnqueueAndTryFlush(entry);
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
            Timestamp = DateTime.UtcNow
        };

        EnqueueAndTryFlush(entry);
        WriteToConsole(entry);
        _logger.LogError(ex, "[Audit] {Action} 失败 | User={UserCode} | IP={IpAddress}",
            action, userCode, ipAddress);
    }

    public void LogApiCall(AuditLogEntry entry)
    {
        EnqueueAndTryFlush(entry);
    }

    public Task LogApiCallAsync(AuditLogEntry entry)
    {
        EnqueueAndTryFlush(entry);
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
                    await WriteToFileAsync(entry, stoppingToken);
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuditLogger] 写入日志失败");
            }
        }

        _logger.LogInformation("[AuditLogger] 后台服务已停止");
    }

    // ==================== 私有方法 ====================

    private void EnqueueAndTryFlush(AuditLogEntry entry)
    {
        _queue.Enqueue(entry);

        // 异步刷新（不阻塞主线程）
        _ = Task.Run(async () =>
        {
            await FlushQueueAsync();
        });
    }

    private async Task FlushQueueAsync()
    {
        if (!_semaphore.Wait(0)) return; // 已在刷新中

        try
        {
            var entries = new List<AuditLogEntry>();
            while (_queue.TryDequeue(out var entry))
                entries.Add(entry);

            if (entries.Count == 0) return;

            foreach (var entry in entries)
                await WriteToFileAsync(entry, CancellationToken.None);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task WriteToFileAsync(AuditLogEntry entry, CancellationToken token)
    {
        try
        {
            var logDir = Path.Combine(_environment.ContentRootPath, "Logs", "Audit");
            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"{DateTime.UtcNow:yyyy-MM-dd}.log");
            var logLine = FormatLogEntry(entry);

            await File.AppendAllTextAsync(logFile, logLine + Environment.NewLine, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditLogger] 文件写入失败");
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

    private string FormatLogEntry(AuditLogEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"【{(entry.Level == LogLevel.Error ? "错误日志" : entry.Level == LogLevel.Warning ? "警告日志" : "信息日志")}】");
        sb.AppendLine($"时间: {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"用户: {entry.UserName ?? entry.UserCode ?? "anonymous"} ({entry.IpAddress ?? "unknown"})");
        sb.AppendLine($"操作: {entry.Action}");

        if (!string.IsNullOrEmpty(entry.ErrorMessage))
            sb.AppendLine($"错误: {entry.ErrorMessage}");

        if (entry.StatusCode.HasValue)
            sb.AppendLine($"状态: {entry.StatusCode}");

        if (entry.DurationMs.HasValue)
            sb.AppendLine($"耗时: {entry.DurationMs}ms");

        if (!string.IsNullOrEmpty(entry.StackTrace))
        {
            sb.AppendLine("───────────────────────────────────────────────────────");
            sb.AppendLine("堆栈跟踪:");
            sb.AppendLine(entry.StackTrace);
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        return sb.ToString();
    }
}
