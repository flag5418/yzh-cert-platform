using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Models;
using YZH.Core.DataBase.MultiDatabase;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace YZH.Core.DataBase.Services;

/// <summary>
/// yzh 通用队列引擎（框架核心，跨项目复用）
/// <para>职责：创建队列（资源锁冲突检测）+ 并发控制 + 超时回收 + 失败重试 + 取消/重跑 + 进度汇总 + 终态通知</para>
/// <para>ORM：SqlSugar（通过 IDbContextFactory 按需创建 SqlSugarClient）</para>
/// <para>
/// 保留原始 SQL 原因：本类大量使用 MySQL 特有语法（FOR UPDATE SKIP LOCKED、NOW() 内联、
/// 批量 UPDATE ... WHERE ... IN），SqlSugar LINQ 无法优雅表达这些操作，
/// 且队列引擎为性能关键路径，原始 SQL 可精确控制执行计划。
/// 所有 SQL 均已参数化，不存在注入风险。
/// </para>
/// </summary>
public class QueueManager
{
    public const string RESOURCE_DIR = "cert_standard_directory";
    public const string RESOURCE_FILE = "cert_standard_directory_file";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueueManager> _logger;
    private readonly int _maxConcurrent;
    private readonly int _timeoutSeconds;
    private readonly int _leaseMinutes;
    private readonly Dictionary<string, IYzhTaskExecutor> _executors;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _runningTokens = new();

    public QueueManager(
        IServiceProvider serviceProvider,
        ILogger<QueueManager> logger,
        IEnumerable<IYzhTaskExecutor> executors)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _maxConcurrent = 4;
        _timeoutSeconds = 300;
        _leaseMinutes = 10;
        _executors = (executors ?? Enumerable.Empty<IYzhTaskExecutor>())
            .Where(e => !string.IsNullOrEmpty(e.TaskType))
            .GroupBy(e => e.TaskType)
            .ToDictionary(g => g.Key, g => g.First());
        _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
    }

    #region 入参 DTO

    public class CreateQueueRequest
    {
        public string QueueType { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
        public string ScopeKey { get; set; } = string.Empty;
        public string ScopeInfoJson { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string OrgCode { get; set; } = string.Empty;
        public List<ResourceLockItem> ResourceLocks { get; set; } = new();
        public List<TaskItem> Tasks { get; set; } = new();
    }

    public class ResourceLockItem
    {
        public string ResourceTable { get; set; } = string.Empty;
        public string ResourceCode { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public int? TaskNo { get; set; }
    }

    public class TaskItem
    {
        public string TaskType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
    }

    #endregion

    #region 创建队列

    public async Task<(bool ok, string? error, string? queueCode, int count)> CreateQueueAsync(CreateQueueRequest req)
    {
        if (req?.Tasks == null || req.Tasks.Count == 0)
            return (true, null, null, 0);
        if (string.IsNullOrEmpty(req.QueueType))
            return (false, "队列类型不能为空", null, 0);

        var queueCode = $"Q-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..20];
        var factory = _serviceProvider.GetRequiredService<IDbContextFactory>();

        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();

        try
        {
            // 1. 资源锁
            var lockRows = new List<YzhQueueResourceLock>();
            var taskNo = 1;
            foreach (var item in req.ResourceLocks ?? new List<ResourceLockItem>())
            {
                var lockRow = new YzhQueueResourceLock
                {
                    Code = Guid.NewGuid().ToString("N"),
                    QueueCode = queueCode,
                    ResourceTable = item.ResourceTable,
                    ResourceCode = item.ResourceCode,
                    ResourceName = item.ResourceName ?? item.ResourceCode,
                    TaskNo = item.TaskNo,
                    Status = "locked",
                    ActiveKey = $"{item.ResourceTable}|{item.ResourceCode}",
                    OrgCode = req.OrgCode,
                    LockTime = DateTime.UtcNow
                };
                lockRows.Add(lockRow);
                await orm.InsertAsync(lockRow);
                taskNo++;
            }

            // 2. 主表
            var queue = new YzhQueue
            {
                Code = Guid.NewGuid().ToString("N"),
                QueueCode = queueCode,
                QueueType = req.QueueType,
                QueueName = req.QueueName ?? $"{req.QueueType}-{req.Tasks.Count}个任务",
                ScopeKey = req.ScopeKey,
                // ScopeInfo 映射到 JSON 列：空串不是合法 JSON 会导致整条 INSERT 静默失败，必须归一为 null
                ScopeInfo = string.IsNullOrWhiteSpace(req.ScopeInfoJson) ? null : req.ScopeInfoJson,
                SourceType = req.SourceType,
                SourceId = req.SourceId,
                Status = "running",
                TotalCount = req.Tasks.Count,
                PendingCount = req.Tasks.Count,
                Progress = 0,
                StartTime = DateTime.Now,
                OrgCode = req.OrgCode,
                CreateBy = req.UserName,
                CreateTime = DateTime.UtcNow
            };
            // 主表必须先落库：InsertAsync 失败只返回 Fail 不抛异常，这里必须检查，
            // 否则会出现「锁/任务已插入而主表无记录」的幽灵队列（监测页永远看不到）
            var insertResult = await orm.InsertAsync(queue);
            if (!insertResult.Success)
            {
                _logger.LogError("[QueueManager] 队列主表插入失败：{QueueCode}, {Error}", queueCode, insertResult.Error);
                return (false, $"队列主表插入失败：{insertResult.Error}", null, 0);
            }

            // 3. 子任务
            taskNo = 1;
            foreach (var task in req.Tasks)
            {
                var taskLocks = lockRows.Where(r => r.TaskNo == taskNo).Select(r => r.Code).ToArray();
                var queueTask = new YzhQueueTask
                {
                    Code = Guid.NewGuid().ToString("N"),
                    QueueCode = queueCode,
                    TaskType = task.TaskType,
                    Payload = task.Payload,
                    Status = "pending",
                    CreateTime = DateTime.Now,
                    TaskId = task.TaskId ?? req.SourceId,
                    UserId = req.UserId,
                    UserName = req.UserName,
                    OrgCode = req.OrgCode,
                    Priority = 0,
                    LockCodes = string.Join(",", taskLocks)
                };
                await orm.InsertAsync(queueTask);
                taskNo++;
            }

            return (true, null, queueCode, req.Tasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[QueueManager] 创建队列失败: {QueueCode}", queueCode);
            return (false, $"创建队列失败：{ex.Message}", null, 0);
        }
    }

    #endregion

    #region 互斥查询

    public async Task<YzhQueue?> FindRunningQueueByScopeKeyAsync(string scopeKey)
    {
        if (string.IsNullOrEmpty(scopeKey)) return null;
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var result = await orm.GetOneAsync<YzhQueue>(q => q.ScopeKey == scopeKey && (q.Status == "pending" || q.Status == "running"));
        return result.Data;
    }

    public class QueueLockHit
    {
        public string ResourceTable { get; set; } = string.Empty;
        public string ResourceCode { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string QueueCode { get; set; } = string.Empty;
        public string? QueueType { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public async Task<QueueLockHit?> FindResourceLockAsync(string resourceTable, List<string> resourceCodes)
    {
        if (string.IsNullOrEmpty(resourceTable) || resourceCodes == null || resourceCodes.Count == 0)
            return null;
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var keys = resourceCodes.Where(c => !string.IsNullOrEmpty(c)).Select(c => $"{resourceTable}|{c}").ToList();
        if (keys.Count == 0) return null;

        var locks = await orm.GetListAsync<YzhQueueResourceLock>(r => r.Status == "locked");
        var hit = locks.Data?.FirstOrDefault(r => keys.Contains(r.ActiveKey ?? ""));
        if (hit == null) return null;

        var queue = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == hit.QueueCode);
        return new QueueLockHit
        {
            ResourceTable = hit.ResourceTable,
            ResourceCode = hit.ResourceCode,
            ResourceName = hit.ResourceName ?? "",
            QueueCode = hit.QueueCode,
            QueueType = queue.Data?.QueueType,
            Status = "locked"
        };
    }

    #endregion

    #region 领取与执行

    public async Task<YzhQueueTask?> GetNextPendingTaskAsync(string workerId)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();

        // 卡死回收
        await ReapStaleTasksAsync(orm);

        // 领取下一个 pending（MySQL FOR UPDATE SKIP LOCKED）
        var sql = @"SELECT * FROM yzh_queue_task
                    WHERE Status = 'pending'
                      AND RetryCount < MaxRetryCount
                      AND (NextRetryAt IS NULL OR NextRetryAt <= NOW())
                    ORDER BY Priority DESC, CreateTime ASC
                    LIMIT 1 FOR UPDATE SKIP LOCKED";
        var result = await orm.SqlQueryAsync<YzhQueueTask>(sql);
        var task = result.Data?.FirstOrDefault();
        if (task == null) return null;

        // 更新为 processing + 租约（业务键 Code — 准则 A）
        var now = DateTime.Now;
        var updateSql = @"UPDATE yzh_queue_task
                         SET Status = 'processing', LockedAt = @lockedAt, LockedBy = @lockedBy,
                             ProcessTime = @processTime, LockedUntil = @lockedUntil, NextRetryAt = NULL
                         WHERE Code = @code";
        await orm.SqlExecuteAsync(updateSql, new { lockedAt = now, lockedBy = workerId, processTime = now, lockedUntil = now.AddMinutes(_leaseMinutes), code = task.Code });

        task.Status = "processing";
        task.LockedAt = now;
        task.LockedBy = workerId;
        task.ProcessTime = now;
        task.LockedUntil = now.AddMinutes(_leaseMinutes);
        return task;
    }

    public async Task<int> ReapStaleTasksOnStartupAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var result = await orm.GetListAsync<YzhQueueTask>(j => j.Status == "processing");
        var orphaned = result.Data ?? new List<YzhQueueTask>();
        if (orphaned.Count == 0) return 0;

        foreach (var task in orphaned)
            await ReapTaskAsync(orm, task, "进程重启，任务执行中断");
        return orphaned.Count;
    }

    private async Task ReapStaleTasksAsync(IDbOrm orm)
    {
        var result = await orm.GetListAsync<YzhQueueTask>(j => j.Status == "processing" && j.LockedUntil != null && j.LockedUntil < DateTime.Now);
        var stale = result.Data ?? new List<YzhQueueTask>();
        if (stale.Count == 0) return;

        foreach (var task in stale)
            await ReapTaskAsync(orm, task, "Worker 租约过期，任务执行中断");
    }

    private async Task ReapTaskAsync(IDbOrm orm, YzhQueueTask task, string reason)
    {
        task.RetryCount++;
        if (task.RetryCount >= task.MaxRetryCount)
        {
            task.Status = "failed";
            task.ErrorType = "retryable";
            task.ErrorMessage = $"{reason}，重试次数已耗尽";
            task.CompleteTime = DateTime.Now;
            task.LockedUntil = null;
            await ReleaseTaskLocksByCodesAsync(orm, task.LockCodes);
        }
        else
        {
            task.Status = "pending";
            task.ErrorMessage = $"{reason}，准备重试";
            task.NextRetryAt = DateTime.Now.AddSeconds(BackoffSeconds(task.RetryCount));
            task.LockedUntil = null;
        }
        await orm.UpdateAsync(task);
    }

    public async Task ExecuteTaskAsync(YzhQueueTask task, CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
        _runningTokens[task.Id] = cts;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();

            // 续期租约
            var now = DateTime.Now;
            await orm.SqlExecuteAsync("UPDATE yzh_queue_task SET LockedUntil = @lt WHERE Code = @code", new { lt = now.AddMinutes(_leaseMinutes), code = task.Code });

            // 队列已取消：跳过执行
            var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == task.QueueCode);
            if (queueResult.Data?.Status == "cancelled")
            {
                await orm.SqlExecuteAsync("UPDATE yzh_queue_task SET Status = 'cancelled', CompleteTime = @ct, LockedUntil = NULL, ErrorMessage = '队列已取消，任务跳过执行' WHERE Code = @code", new { ct = now, code = task.Code });
                await RefreshQueueProgressAsync(orm, task.QueueCode);
                return;
            }

            // 执行器分发
            if (!_executors.TryGetValue(task.TaskType, out var executor))
                throw new NotSupportedException($"没有注册 {task.TaskType} 类型的任务执行器（IYzhTaskExecutor）");

            var result = await executor.ExecuteAsync(task, cts.Token);
            if (result != null && !result.Success)
                throw new Exception(result.Message ?? "任务执行失败");

            // 标记完成
            await orm.SqlExecuteAsync("UPDATE yzh_queue_task SET Status = 'completed', CompleteTime = @ct, LockedUntil = NULL WHERE Code = @code", new { ct = now, code = task.Code });
            await ReleaseTaskLocksByCodesAsync(orm, task.LockCodes);
            await RefreshQueueProgressAsync(orm, task.QueueCode);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            await HandleTaskFailureAsync(task, "任务执行超时或已取消，任务被强制终止", isCancel: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[QueueManager] 任务执行失败: {TaskId}", task.Id);
            await HandleTaskFailureAsync(task, ex.InnerException?.Message ?? ex.Message, isCancel: false);
        }
        finally
        {
            _runningTokens.TryRemove(task.Id, out _);
            cts.Dispose();
            _semaphore.Release();
        }
    }

    private async Task HandleTaskFailureAsync(YzhQueueTask task, string message, bool isCancel)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var result = await orm.GetOneAsync<YzhQueueTask>(j => j.Code == task.Code);
        var taskRow = result.Data;
        if (taskRow == null) return;

        if (isCancel)
        {
            var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == taskRow.QueueCode);
            if (queueResult.Data?.Status == "cancelled")
            {
                await orm.SqlExecuteAsync("UPDATE yzh_queue_task SET Status = 'cancelled', ErrorMessage = @msg, CompleteTime = @ct WHERE Code = @code",
                    new { msg = message, ct = DateTime.Now, code = taskRow.Code });
                await RefreshQueueProgressAsync(orm, taskRow.QueueCode);
                return;
            }
        }

        taskRow.RetryCount++;
        var retryable = isCancel || ClassifyError(message);
        taskRow.ErrorType = retryable ? "retryable" : "permanent";
        taskRow.ErrorMessage = message;
        if (retryable && taskRow.RetryCount < taskRow.MaxRetryCount)
        {
            taskRow.Status = "pending";
            taskRow.NextRetryAt = DateTime.Now.AddSeconds(BackoffSeconds(taskRow.RetryCount));
            taskRow.LockedUntil = null;
        }
        else
        {
            taskRow.Status = "failed";
            taskRow.CompleteTime = DateTime.Now;
            taskRow.LockedUntil = null;
            await ReleaseTaskLocksByCodesAsync(orm, taskRow.LockCodes);
        }
        await orm.UpdateAsync(taskRow);
        await RefreshQueueProgressAsync(orm, taskRow.QueueCode);
    }

    private bool ClassifyError(string? message)
    {
        if (string.IsNullOrEmpty(message)) return true;
        var m = message.ToLower();
        var permanent = new[] { "不存在", "已被修改", "已被删除", "不是合法", "不支持", "无法解析", "非法", "损坏", "libreoffice 不可用", "权限", "拒绝访问", "没有注册" };
        return !permanent.Any(p => m.Contains(p));
    }

    private int BackoffSeconds(int retryCount)
    {
        var secs = 5 * (int)Math.Pow(3, Math.Max(0, retryCount - 1));
        if (secs > 300) secs = 300;
        return secs + Random.Shared.Next(0, 5);
    }

    #endregion

    #region 取消/重试

    public async Task CancelBatchAsync(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var tasks = await orm.GetListAsync<YzhQueueTask>(j => j.TaskId == taskId);
        var queueCodes = tasks.Data?.Select(j => j.QueueCode).Distinct().ToList() ?? new List<string>();
        foreach (var queueCode in queueCodes)
            await CancelQueueAsync(queueCode);
    }

    public async Task<(bool ok, string? error)> CancelQueueAsync(string queueCode)
    {
        if (string.IsNullOrEmpty(queueCode)) return (false, "队列编码不能为空");
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return (false, "队列不存在");
        if (IsTerminal(queue.Status)) return (false, "队列已结束，无需取消");

        // 1. 主表先置终态
        queue.Status = "cancelled";
        queue.EndTime = DateTime.Now;
        queue.CancelledCount = queue.TotalCount - queue.CompletedCount - queue.FailedCount;
        await orm.UpdateAsync(queue);

        // 2. 终止 processing
        var processingResult = await orm.GetListAsync<YzhQueueTask>(j => j.QueueCode == queueCode && j.Status == "processing");
        foreach (var task in processingResult.Data ?? new List<YzhQueueTask>())
            if (_runningTokens.TryRemove(task.Id, out var cts)) cts.Cancel();

        // 3. pending → cancelled
        await orm.SqlExecuteAsync("UPDATE yzh_queue_task SET Status = 'cancelled', CompleteTime = NOW() WHERE QueueCode = @qc AND Status = 'pending'", new { qc = queueCode });

        // 4. 批量释放锁
        await orm.SqlExecuteAsync("UPDATE yzh_queue_resource_lock SET Status = 'released', ActiveKey = NULL, ReleaseTime = NOW() WHERE QueueCode = @qc AND Status = 'locked'", new { qc = queueCode });

        // 5. 业务清理钩子
        try
        {
            var handlers = scope.ServiceProvider.GetServices<IYzhQueueCancelHandler>();
            foreach (var handler in handlers)
                await handler.OnQueueCancelledAsync(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[QueueManager] 队列取消清理回调失败: {QueueCode}", queueCode);
        }

        return (true, null);
    }

    public async Task<(bool ok, string? error)> RetryTaskAsync(string taskCode)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var result = await orm.GetOneAsync<YzhQueueTask>(j => j.Code == taskCode);
        var task = result.Data;
        if (task == null) return (false, "任务不存在");
        if (task.Status != "failed") return (false, "仅失败的任务可重试");

        task.Status = "pending";
        task.RetryCount = 0;
        task.ErrorMessage = null;
        task.ErrorType = null;
        task.NextRetryAt = DateTime.Now;
        task.CompleteTime = null;
        await orm.UpdateAsync(task);
        await RefreshQueueProgressAsync(orm, task.QueueCode);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> RetryQueueAsync(string queueCode)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return (false, "队列不存在");
        if (!IsTerminal(queue.Status)) return (false, "仅已结束的队列可整队重跑");

        var tasksResult = await orm.GetListAsync<YzhQueueTask>(j => j.QueueCode == queueCode && j.Status == "failed");
        var tasks = tasksResult.Data ?? new List<YzhQueueTask>();
        if (tasks.Count == 0) return (false, "没有可重试的任务");

        foreach (var task in tasks)
        {
            task.Status = "pending";
            task.RetryCount = 0;
            task.ErrorMessage = null;
            task.ErrorType = null;
            task.NextRetryAt = DateTime.Now;
            task.CompleteTime = null;
            await orm.UpdateAsync(task);
        }
        await RefreshQueueProgressAsync(orm, queueCode);
        return (true, null);
    }

    #endregion

    #region 进度与查询

    private async Task RefreshQueueProgressAsync(IDbOrm orm, string queueCode)
    {
        if (string.IsNullOrEmpty(queueCode)) return;
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return;

        var tasksResult = await orm.GetListAsync<YzhQueueTask>(j => j.QueueCode == queueCode);
        var tasks = tasksResult.Data ?? new List<YzhQueueTask>();
        var total = tasks.Count;
        var completed = tasks.Count(j => j.Status == "completed");
        var failed = tasks.Count(j => j.Status == "failed");
        var cancelled = tasks.Count(j => j.Status == "cancelled");
        var processing = tasks.Count(j => j.Status == "processing");
        var pending = tasks.Count(j => j.Status == "pending");

        queue.TotalCount = total;
        queue.CompletedCount = completed;
        queue.FailedCount = failed;
        queue.CancelledCount = cancelled;
        queue.ProcessingCount = processing;
        queue.PendingCount = pending;
        queue.Progress = total > 0 ? (int)((decimal)(completed + failed + cancelled) / total * 100) : 0;

        var wasTerminal = IsTerminal(queue.Status);
        string newStatus;
        if (pending > 0 || processing > 0) newStatus = "running";
        else if (failed > 0) newStatus = "failed";
        else if (cancelled > 0) newStatus = "cancelled";
        else newStatus = "completed";

        if (newStatus == "running" && wasTerminal)
        {
            queue.EndTime = null;
            queue.StartTime = DateTime.Now;
        }
        queue.Status = newStatus;

        if (IsTerminal(newStatus))
        {
            queue.EndTime = DateTime.Now;
            await orm.SqlExecuteAsync("UPDATE yzh_queue_resource_lock SET Status = 'released', ActiveKey = NULL, ReleaseTime = NOW() WHERE QueueCode = @qc AND Status = 'locked'", new { qc = queueCode });
        }
        await orm.UpdateAsync(queue);
    }

    private static bool IsTerminal(string status) => status is "completed" or "failed" or "cancelled";

    public async Task<object> GetBatchProgressAsync(string taskId)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var result = await orm.GetListAsync<YzhQueueTask>(j => j.TaskId == taskId);
        var tasks = result.Data ?? new List<YzhQueueTask>();
        return new
        {
            taskId,
            total = tasks.Count,
            completed = tasks.Count(j => j.Status == "completed"),
            failed = tasks.Count(j => j.Status == "failed"),
            processing = tasks.Count(j => j.Status == "processing"),
            pending = tasks.Count(j => j.Status == "pending"),
            cancelled = tasks.Count(j => j.Status == "cancelled"),
            isFinished = tasks.All(j => j.Status is "completed" or "failed" or "cancelled")
        };
    }

    public async Task<object> GetQueueStatusAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var tasks = (await orm.GetListAsync<YzhQueueTask>()).Data ?? new List<YzhQueueTask>();
        var queues = (await orm.GetListAsync<YzhQueue>()).Data ?? new List<YzhQueue>();
        return new
        {
            totalPending = tasks.Count(j => j.Status == "pending"),
            totalProcessing = tasks.Count(j => j.Status == "processing"),
            totalCompleted = tasks.Count(j => j.Status == "completed"),
            totalFailed = tasks.Count(j => j.Status == "failed"),
            totalCancelled = tasks.Count(j => j.Status == "cancelled"),
            runningQueues = queues.Count(q => q.Status == "running"),
            pendingQueues = queues.Count(q => q.Status == "pending"),
            maxConcurrent = _maxConcurrent,
            timeoutSeconds = _timeoutSeconds,
            runningWorkers = _maxConcurrent - _semaphore.CurrentCount
        };
    }

    public async Task<object> GetQueueStatsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queues = (await orm.GetListAsync<YzhQueue>()).Data ?? new List<YzhQueue>();
        var today = DateTime.Today;
        return new
        {
            runningQueues = queues.Count(q => q.Status == "running"),
            pendingQueues = queues.Count(q => q.Status == "pending"),
            todayCompleted = queues.Count(q => q.Status == "completed" && q.EndTime >= today),
            todayFailed = queues.Count(q => q.Status == "failed" && q.EndTime >= today),
            todayCancelled = queues.Count(q => q.Status == "cancelled" && q.EndTime >= today),
            maxConcurrent = _maxConcurrent,
            runningWorkers = _maxConcurrent - _semaphore.CurrentCount
        };
    }

    public async Task<object> GetQueueListAsync(string? type, string? status, DateTime? startTime, DateTime? endTime, int page, int rows)
    {
        if (page <= 0) page = 1;
        if (rows <= 0) rows = 20;
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();

        var allResult = await orm.GetListAsync<YzhQueue>();
        var query = (allResult.Data ?? new List<YzhQueue>()).AsEnumerable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(q => q.QueueType == type);
        if (!string.IsNullOrEmpty(status))
        {
            if (status == "executing")
                query = query.Where(q => q.Status == "pending" || q.Status == "running");
            else
                query = query.Where(q => q.Status == status);
        }
        if (startTime.HasValue)
            query = query.Where(q => q.CreateTime >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(q => q.CreateTime <= endTime.Value);

        var total = query.Count();
        var list = query.OrderByDescending(q => q.CreateTime)
            .Skip((page - 1) * rows).Take(rows).ToList();

        return new
        {
            total,
            rows = list.Select(q => new
            {
                queueCode = q.QueueCode,
                queueType = q.QueueType,
                queueName = q.QueueName,
                scopeKey = q.ScopeKey,
                status = q.Status,
                totalCount = q.TotalCount,
                completedCount = q.CompletedCount,
                failedCount = q.FailedCount,
                processingCount = q.ProcessingCount,
                pendingCount = q.PendingCount,
                cancelledCount = q.CancelledCount,
                progress = q.Progress,
                createBy = q.CreateBy,
                sourceType = q.SourceType,
                sourceId = q.SourceId,
                startTime = q.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = q.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                createTime = q.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList()
        };
    }

    public async Task<object?> GetQueueDetailAsync(string queueCode)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return null;

        var tasksResult = await orm.GetListAsync<YzhQueueTask>(j => j.QueueCode == queueCode);
        var locksResult = await orm.GetListAsync<YzhQueueResourceLock>(r => r.QueueCode == queueCode);

        return new
        {
            queue = new
            {
                queueCode = queue.QueueCode,
                queueType = queue.QueueType,
                queueName = queue.QueueName,
                scopeKey = queue.ScopeKey,
                status = queue.Status,
                totalCount = queue.TotalCount,
                completedCount = queue.CompletedCount,
                failedCount = queue.FailedCount,
                processingCount = queue.ProcessingCount,
                pendingCount = queue.PendingCount,
                cancelledCount = queue.CancelledCount,
                progress = queue.Progress,
                createBy = queue.CreateBy,
                sourceType = queue.SourceType,
                sourceId = queue.SourceId,
                startTime = queue.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = queue.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                createTime = queue.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
            },
            tasks = (tasksResult.Data ?? new List<YzhQueueTask>()).Select((j, i) =>
            {
                string? fileCode = null, fileName = null, convertType = null;
                if (!string.IsNullOrEmpty(j.Payload))
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(j.Payload);
                        if (doc.RootElement.TryGetProperty("fileCode", out var fc)) fileCode = fc.GetString();
                        if (doc.RootElement.TryGetProperty("fileName", out var fn)) fileName = fn.GetString();
                        if (doc.RootElement.TryGetProperty("convertType", out var ct)) convertType = ct.GetString();
                    }
                    catch { }
                }
                return new
                {
                    code = j.Code,
                    taskNo = i + 1,
                    taskType = j.TaskType,
                    fileCode,
                    fileName,
                    convertType,
                    status = j.Status,
                    retryCount = j.RetryCount,
                    errorType = j.ErrorType,
                    errorMessage = j.ErrorMessage,
                    processTime = j.ProcessTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    completeTime = j.CompleteTime?.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }).ToList(),
            locks = (locksResult.Data ?? new List<YzhQueueResourceLock>()).Select(r => new
            {
                code = r.Code,
                resourceTable = r.ResourceTable,
                resourceCode = r.ResourceCode,
                resourceName = r.ResourceName,
                taskNo = r.TaskNo,
                status = r.Status,
                createTime = r.LockTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                releaseTime = r.ReleaseTime?.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList()
        };
    }

    #endregion

    #region 锁释放

    private async Task ReleaseTaskLocksByCodesAsync(IDbOrm orm, string? lockCodes)
    {
        if (string.IsNullOrEmpty(lockCodes)) return;
        var codes = lockCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (codes.Length == 0) return;
        foreach (var code in codes)
        {
            await orm.SqlExecuteAsync("UPDATE yzh_queue_resource_lock SET Status = 'released', ActiveKey = NULL, ReleaseTime = NOW() WHERE Code = @code AND Status = 'locked'", new { code });
        }
    }

    #endregion
}
