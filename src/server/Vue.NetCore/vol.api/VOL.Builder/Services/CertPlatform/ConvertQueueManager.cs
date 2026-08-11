using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Dir;
using VOL.Entity.CertPlatform.Sys;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 文件转换队列管理器
    /// 职责：
    /// 1. 并发控制（SemaphoreSlim，从系统参数读取）
    /// 2. 超时控制（CancellationTokenSource，300s）
    /// 3. 强制取消（杀进程 + 清理）
    /// 4. SignalR 进度推送
    /// 5. 消息持久化
    /// </summary>
    public class ConvertQueueManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConvertQueueManager> _logger;
        private readonly IConvertNotifier _notifier;

        private SemaphoreSlim _semaphore;
        private int _maxConcurrent;
        private int _timeoutSeconds;

        // 运行中的任务取消令牌（用于强制取消）
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _runningTokens = new();

        public ConvertQueueManager(
            IServiceProvider serviceProvider,
            ILogger<ConvertQueueManager> logger,
            IConvertNotifier notifier)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _notifier = notifier;
            _maxConcurrent = 5;
            _timeoutSeconds = 300;
            _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
        }

        /// <summary>
        /// 从系统参数重新加载配置
        /// </summary>
        public void ReloadConfig()
        {
            using var scope = _serviceProvider.CreateScope();
            var configService = scope.ServiceProvider.GetRequiredService<ISysConfigService>();

            var newMax = configService.GetInt("convert_max_concurrent", 5);
            var newTimeout = configService.GetInt("convert_timeout_seconds", 300);

            if (newMax != _maxConcurrent)
            {
                _maxConcurrent = newMax;
                _semaphore.Dispose();
                _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
                _logger.LogInformation($"[ConvertQueueManager] 并发数已更新: {newMax}");
            }

            _timeoutSeconds = newTimeout;
            _logger.LogInformation($"[ConvertQueueManager] 超时时间: {newTimeout}s");
        }

        /// <summary>
        /// 获取下一个待处理任务（带锁定）
        /// </summary>
        public async Task<ConvertJob> GetNextPendingJobAsync(string workerId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            // 1. 查找下一个 pending 任务（按优先级降序、创建时间升序）
            var job = await db.Set<ConvertJob>()
                .FromSqlRaw(@"
                    SELECT * FROM cert_file_convert_job
                    WHERE status = 'pending' AND retry_count < max_retry_count
                    ORDER BY priority DESC, create_time ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED")
                .FirstOrDefaultAsync();

            if (job == null)
                return null;

            // 2. 更新为 processing 状态
            job.Status = "processing";
            job.LockedAt = DateTime.Now;
            job.LockedBy = workerId;
            job.ProcessTime = DateTime.Now;
            await db.SaveChangesAsync();

            return job;
        }

        /// <summary>
        /// 执行转换任务（带并发控制 + 超时 + 通知）
        /// </summary>
        public async Task ExecuteJobAsync(ConvertJob job, CancellationToken stoppingToken)
        {
            await _semaphore.WaitAsync(stoppingToken);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
            _runningTokens[job.Id] = cts;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var convertService = scope.ServiceProvider.GetRequiredService<OfficeConvertService>();
                var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

                _logger.LogInformation($"[ConvertQueueManager] 开始转换: {job.FileCode}");

                await convertService.ExecuteConvertAsync(job, cts.Token);

                // 推送完成通知
                await NotifyProgress(job, "completed", "转换成功");

                // 持久化消息
                await messageService.CreateAsync(
                    job.UserId ?? 0,
                    job.UserName,
                    "文档转换完成",
                    $"{job.FileCode} 转换成功",
                    "convert",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        taskId = job.TaskId,
                        fileCode = job.FileCode,
                        status = "completed",
                        targetPath = job.TargetPath
                    })
                );

                _logger.LogInformation($"[ConvertQueueManager] 转换成功: {job.FileCode}");
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // 超时或手动取消
                _logger.LogWarning($"[ConvertQueueManager] 转换超时/取消: {job.FileCode}");

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
                var jobToUpdate = await db.Set<ConvertJob>().FirstOrDefaultAsync(j => j.Id == job.Id);
                if (jobToUpdate != null)
                {
                    jobToUpdate.Status = "failed";
                    jobToUpdate.ErrorMessage = "转换超时，已被强制终止";
                    jobToUpdate.RetryCount++;
                    await db.SaveChangesAsync();
                }

                await NotifyProgress(job, "failed", "转换超时，已被强制终止");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ConvertQueueManager] 转换失败: {job.FileCode}");
                await NotifyProgress(job, "failed", ex.Message);
            }
            finally
            {
                _runningTokens.TryRemove(job.Id, out _);
                cts.Dispose();
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 取消指定批次的所有未完成任务
        /// </summary>
        public async Task CancelBatchAsync(string taskId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            // 1. 取消正在处理的任务
            var processingJobs = await db.Set<ConvertJob>()
                .Where(j => j.TaskId == taskId && j.Status == "processing")
                .ToListAsync();

            foreach (var job in processingJobs)
            {
                if (_runningTokens.TryRemove(job.Id, out var cts))
                {
                    cts.Cancel();
                }
            }

            // 2. 更新待处理任务为 cancelled
            var pendingJobs = await db.Set<ConvertJob>()
                .Where(j => j.TaskId == taskId && j.Status == "pending")
                .ToListAsync();

            foreach (var job in pendingJobs)
            {
                job.Status = "cancelled";
                job.CompleteTime = DateTime.Now;
            }

            await db.SaveChangesAsync();

            // 3. 推送取消通知
            if (!string.IsNullOrEmpty(taskId))
            {
                await _notifier.SendToConvertGroup(taskId, new
                    {
                        title = "转换已取消",
                        message = $"批次 {taskId} 的转换任务已全部取消",
                        date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        value = "convert_cancelled",
                        data = new { taskId }
                    });
            }

            _logger.LogInformation($"[ConvertQueueManager] 批次 {taskId} 已取消，正在处理 {processingJobs.Count} 个，待处理 {pendingJobs.Count} 个");
        }

        /// <summary>
        /// 获取批次转换进度
        /// </summary>
        public async Task<object> GetBatchProgressAsync(string taskId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var jobs = await db.Set<ConvertJob>()
                .AsNoTracking()
                .Where(j => j.TaskId == taskId)
                .ToListAsync();

            return new
            {
                taskId,
                total = jobs.Count,
                completed = jobs.Count(j => j.Status == "completed"),
                failed = jobs.Count(j => j.Status == "failed"),
                processing = jobs.Count(j => j.Status == "processing"),
                pending = jobs.Count(j => j.Status == "pending"),
                cancelled = jobs.Count(j => j.Status == "cancelled"),
                isFinished = jobs.All(j => j.Status == "completed" || j.Status == "failed" || j.Status == "cancelled")
            };
        }

        /// <summary>
        /// 获取全局队列状态
        /// </summary>
        public async Task<object> GetQueueStatusAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var jobs = await db.Set<ConvertJob>().AsNoTracking().ToListAsync();

            return new
            {
                totalPending = jobs.Count(j => j.Status == "pending"),
                totalProcessing = jobs.Count(j => j.Status == "processing"),
                totalCompleted = jobs.Count(j => j.Status == "completed"),
                totalFailed = jobs.Count(j => j.Status == "failed"),
                totalCancelled = jobs.Count(j => j.Status == "cancelled"),
                maxConcurrent = _maxConcurrent,
                timeoutSeconds = _timeoutSeconds,
                runningWorkers = _maxConcurrent - _semaphore.CurrentCount
            };
        }

        /// <summary>
        /// 推送进度通知
        /// </summary>
        private async Task NotifyProgress(ConvertJob job, string status, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var progress = await db.Set<ConvertJob>()
                .AsNoTracking()
                .Where(j => j.TaskId == job.TaskId)
                .GroupBy(j => j.TaskId)
                .Select(g => new
                {
                    total = g.Count(),
                    completed = g.Count(j => j.Status == "completed"),
                    failed = g.Count(j => j.Status == "failed")
                })
                .FirstOrDefaultAsync();

            var notification = new
            {
                title = status == "completed" ? "文档转换完成" : "文档转换失败",
                message = $"{job.FileCode} - {message}",
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                value = "convert_progress",
                data = new
                {
                    taskId = job.TaskId,
                    fileCode = job.FileCode,
                    status,
                    progress,
                    convertedStoragePath = status == "completed" ? job.TargetPath : null
                }
            };

            // 按 taskId 推送到组
            if (!string.IsNullOrEmpty(job.TaskId))
            {
                await _notifier.SendToConvertGroup(job.TaskId, notification);
            }

            // 也推送给用户
            if (!string.IsNullOrEmpty(job.UserName))
            {
                await _notifier.SendToUser(job.UserName, notification);
            }
        }
    }
}
