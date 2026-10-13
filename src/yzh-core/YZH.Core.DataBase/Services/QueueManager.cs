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

        // 从系统参数读取并发控制（启动时读取一次，生效需重启服务）
        _maxConcurrent = ReadConfigInt("queue_max_concurrent", 4);
        _timeoutSeconds = ReadConfigInt("queue_timeout_seconds", 300);
        _leaseMinutes = ReadConfigInt("queue_lease_minutes", 10);

        _executors = (executors ?? Enumerable.Empty<IYzhTaskExecutor>())
            .Where(e => !string.IsNullOrEmpty(e.TaskType))
            .GroupBy(e => e.TaskType)
            .ToDictionary(g => g.Key, g => g.First());

        _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
        _logger.LogInformation("[QueueManager] 队列引擎已启动，并发 Worker 数: {MaxConcurrent}", _maxConcurrent);
    }

    private int ReadConfigInt(string key, int defaultValue)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
            // 注意：QueueManager 是 Singleton，此处在构造函数中使用同步等待是允许的
            var sql = "SELECT ConfigValue FROM cert_sys_config WHERE ConfigKey = @key AND IsValid = 1 AND IsDeleted = 0";
            var result = orm.SqlScalarAsync<string>(sql, new { key }).GetAwaiter().GetResult().Data;

            if (!string.IsNullOrEmpty(result) && int.TryParse(result, out var val))
            {
                return val > 0 ? val : defaultValue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QueueManager] 读取系统参数 {Key} 失败，使用默认值 {Default}", key, defaultValue);
        }
        return defaultValue;
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

        /// <summary>[已废弃] 旧版数字用户 Id（无消费方，仅为兼容调用方而保留）</summary>
        public int UserId { get; set; }

        /// <summary>
        ///     ★ <b>创建人用户 Code（<c>Sys_User.Code</c>）</b> → 写入 <c>yzh_queue.CreateBy</c>。
        ///
        ///     <para>契约（与 <c>BaseEntity</c> 一致：<c>CreateBy</c> 存 <b>Code</b>）：
        ///     全仓 <c>CreateBy</c> / <c>IUserContext.UserCode</c> / 推送分组键 / JWT <c>code</c> claim
        ///     必须同键。⛔ 别再把它写成登录名 —— 那样姓名解析、按 Code 关联、推送寻址都会静默失效。</para>
        ///
        ///     <para>⚠️ 后台 / 系统发起的队列（无用户上下文）留空即可：
        ///     空 = 「系统发起」，比编一个用户更诚实；但**代价是终态通知没有收件人**（站内信 + SignalR 都会跳过），
        ///     需在调用方补：能从父队列继承的（如转换→提取）就继承，见
        ///     <c>OfficeConvertTaskExecutor.ResolveParentCreatorAsync</c>。</para>
        /// </summary>
        public string UserCode { get; set; } = string.Empty;

        /// <summary>
        ///     创建人登录名（→ <c>yzh_queue_task.UserName</c>，仅供人工排查）。
        ///     ⛔ <b>不要把它写进 <c>yzh_queue.CreateBy</c></b>（那列要求 Code）。
        /// </summary>
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

        /// <summary>
        /// 业务明细键（T1 列，T4 启用）：如专家任务的 NC 检查项 Code / 报告章节 Code。
        /// 仅供追溯与续跑定位，⛔ 不参与调度（调度键仍是 Code / TaskType）。
        /// </summary>
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>同队列内明细序号（T1 列，T4 启用）——保序展示与串行组排序（T5）用。</summary>
        public int Seq { get; set; }
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
                // ★ 契约：CreateBy = 用户 Code（BaseEntity 注释口径）。空 = 系统发起，归一为 NULL
                //   —— 不让 "" 与 NULL 两种空值并存（否则任何 `CreateBy <> ''` 类判据都会漏掉一半）。
                CreateBy = string.IsNullOrWhiteSpace(req.UserCode) ? null : req.UserCode.Trim(),
                CreateTime = DateTime.UtcNow
            };
            // 主表必须先落库：InsertAsync 失败只返回 Fail 不抛异常，这里必须检查，
            // 否则会出现「锁/任务已插入而主表无记录」的幽灵队列（监测页永远看不到）
            var insertResult = await orm.InsertAsync(queue);
            if (!insertResult.Success)
            {
                _logger.LogError("[QueueManager] 队列主表插入失败：{QueueCode}, {Error}", queueCode, insertResult.Error);
                // 主表失败必须回滚已插入的资源锁，否则幽灵锁（ActiveKey 占用）会永久阻塞该资源的后续队列
                await orm.SqlExecuteAsync("UPDATE yzh_queue_resource_lock SET Status = 'released', ActiveKey = NULL, ReleaseTime = NOW() WHERE QueueCode = @qc AND Status = 'locked'", new { qc = queueCode });
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
                    LockCodes = string.Join(",", taskLocks),
                    ItemCode = task.ItemCode ?? "",
                    Seq = task.Seq
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
        // ★ T5（2026-10-13）同队列串行：同 QueueCode 存在 processing 则不领
        //   —— 对齐旧 ExpertTaskQueueRunner「无 running 才领」（D10 特性保全）。
        //   serial 组 = QueueCode；跨队列照常并行，不受此约束。
        var sql = @"SELECT * FROM yzh_queue_task t
                    WHERE t.Status = 'pending'
                      AND t.RetryCount < t.MaxRetryCount
                      AND (t.NextRetryAt IS NULL OR t.NextRetryAt <= NOW())
                      AND NOT EXISTS (
                          SELECT 1 FROM yzh_queue_task r
                          WHERE r.QueueCode = t.QueueCode AND r.Status = 'processing' AND r.IsDeleted = 0)
                    ORDER BY t.Priority DESC, t.CreateTime ASC, t.Seq ASC
                    LIMIT 1 FOR UPDATE SKIP LOCKED";
        var result = await orm.SqlQueryAsync<YzhQueueTask>(sql);
        var task = result.Data?.FirstOrDefault();
        if (task == null) return null;

        // 乐观抢占（业务键 Code — 准则 A）：只有仍为 pending 才拿得到（affected != 1 ⇒ 被并发 worker 抢走，让出）
        var now = DateTime.Now;
        var updateSql = @"UPDATE yzh_queue_task
                         SET Status = 'processing', LockedAt = @lockedAt, LockedBy = @lockedBy,
                             ProcessTime = @processTime, LockedUntil = @lockedUntil, NextRetryAt = NULL
                         WHERE Code = @code AND Status = 'pending'";
        var claim = await orm.SqlExecuteAsync(updateSql, new { lockedAt = now, lockedBy = workerId, processTime = now, lockedUntil = now.AddMinutes(_leaseMinutes), code = task.Code });
        if (!claim.Success || claim.Data != 1) return null;

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

            // ★ T3（2026-10-13）：跳过 = 终态但非失败 —— 置 skipped，计入 SkippedCount，不重试。
            if (result != null && result.IsSkipped)
            {
                await orm.SqlExecuteAsync("UPDATE yzh_queue_task SET Status = 'skipped', CompleteTime = @ct, LockedUntil = NULL, ErrorMessage = @msg WHERE Code = @code",
                    new { ct = now, msg = result.Message ?? "任务跳过", code = task.Code });
                await ReleaseTaskLocksByCodesAsync(orm, task.LockCodes);
                await RefreshQueueProgressAsync(orm, task.QueueCode);
                return;
            }

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

    public async Task<(bool ok, string? error)> CancelQueueAsync(string queueCode, string? orgCode = null)
    {
        if (string.IsNullOrEmpty(queueCode)) return (false, "队列编码不能为空");
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return (false, "队列不存在");
        // ★ 租户隔离（H12 / R-03）：调用方传了 orgCode 时校验归属，越权 = 视为不存在（不泄露他租户队列的存在性）
        if (!string.IsNullOrEmpty(orgCode) && queue.OrgCode != orgCode) return (false, "队列不存在或无权访问");
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

        // 6. 终态通知（入口已保证「非终态 → cancelled」是一次边沿转换）
        await NotifyTerminalAsync(queue);

        return (true, null);
    }

    public async Task<(bool ok, string? error)> RetryTaskAsync(string taskCode, string? orgCode = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var result = await orm.GetOneAsync<YzhQueueTask>(j => j.Code == taskCode);
        var task = result.Data;
        if (task == null) return (false, "任务不存在");
        // ★ 租户隔离（H12 / R-03）：传了 orgCode 则校验任务归属，越权 = 视为不存在
        if (!string.IsNullOrEmpty(orgCode) && task.OrgCode != orgCode) return (false, "任务不存在或无权访问");
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

    public async Task<(bool ok, string? error)> RetryQueueAsync(string queueCode, string? orgCode = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return (false, "队列不存在");
        // ★ 租户隔离（H12 / R-03）：传了 orgCode 则校验归属，越权 = 视为不存在
        if (!string.IsNullOrEmpty(orgCode) && queue.OrgCode != orgCode) return (false, "队列不存在或无权访问");
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
        var skipped = tasks.Count(j => j.Status == "skipped");
        var processing = tasks.Count(j => j.Status == "processing");
        var pending = tasks.Count(j => j.Status == "pending");

        queue.TotalCount = total;
        queue.CompletedCount = completed;
        queue.FailedCount = failed;
        queue.CancelledCount = cancelled;
        queue.SkippedCount = skipped;
        queue.ProcessingCount = processing;
        queue.PendingCount = pending;
        // ★ T3：skipped 计入「已处理」进度（跳过是业务终态，不是失败）
        queue.Progress = total > 0 ? (int)((decimal)(completed + failed + cancelled + skipped) / total * 100) : 0;

        var wasTerminal = IsTerminal(queue.Status);
        string newStatus;
        if (pending > 0 || processing > 0) newStatus = "running";
        else if (failed > 0) newStatus = "failed";
        else if (cancelled > 0) newStatus = "cancelled";
        else newStatus = "completed";   // 全 skipped 也算 completed（跳过≠失败）

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

        // 终态**边沿**通知（仅「刚进入终态」发一次；重跑回 running 后再次进入终态会再发）
        if (IsTerminal(newStatus) && !wasTerminal)
            await NotifyTerminalAsync(queue);
    }

    /// <summary>
    /// 队列终态通知（IYzhQueueNotifier —— 业务侧实现：消息落库 cert_message + SignalR 实时推送）。
    /// 通知是旁路：任何失败只记日志，绝不影响队列状态机。
    /// </summary>
    private async Task NotifyTerminalAsync(YzhQueue queue)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            foreach (var notifier in scope.ServiceProvider.GetServices<IYzhQueueNotifier>())
                await notifier.NotifyAsync(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[QueueManager] 队列终态通知失败: {QueueCode}", queue.QueueCode);
        }
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

    /// <summary>
    ///     调用 <paramref name="resolver"/> 解析创建人姓名，<b>异常不外抛</b>（解析是旁路，⛔ 不能因为查不到人就让队列页打不开）。
    /// </summary>
    private static async Task<string> SafeResolveCreatorAsync(
        Func<string?, Task<string>> resolver, string? userCode)
    {
        try { return await resolver(userCode); }
        catch { return string.Empty; }
    }

    /// <summary>队列统计。<paramref name="orgCode"/> 传入时仅统计该租户的队列（H12 / R-03）；null = 平台全量。</summary>
    public async Task<object> GetQueueStatsAsync(string? orgCode = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queues = (await orm.GetListAsync<YzhQueue>()).Data ?? new List<YzhQueue>();
        if (!string.IsNullOrEmpty(orgCode))
            queues = queues.Where(q => q.OrgCode == orgCode).ToList();
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

    /// <summary>
    /// 队列列表查询（全项目共用）
    ///
    /// <para>★ <paramref name="orgCode"/> 租户过滤（H12 / R-03）：
    /// 传入时仅返回该 <c>OrgCode</c> 的队列（专家端强制传工作区 Code <c>ws.Code</c>）；
    /// null / 空 = 平台全量（后台监控页合法视角）。对齐 <c>ExpertTaskService.GetQueueListAsync</c>
    /// 的强制工作区过滤形状。</para>
    ///
    /// <para>★ <paramref name="scopeResolver"/> 可选：<code>Func&lt;queueType, scopeKey, Task&lt;string&gt;&gt;</code>
    /// —— 调用方注入按队列类型解析 ScopeKey 为人话的回调（框架层不感知业务实体）。
    /// 不传则 <c>scopeDisplay</c> 回落为空字符串。</para>
    ///
    /// <para>★ <paramref name="creatorResolver"/> 可选：<code>Func&lt;userCode, Task&lt;string&gt;&gt;</code>
    /// —— 把 <c>CreateBy</c>（★ 用户 Code）解析成人话姓名。
    /// ⚠️ <b>必须有</b>：<c>CreateBy</c> 按契约存的是 Code，界面上直接渲染会变成
    /// 「创建人 = df6e03e9…」（裸 Code 当姓名）—— 这正是 2026-10-09 用户报障的那类问题。
    /// 框架层（DataBase）不引用 <c>Sys_User</c>（在 Api 层）⇒ 只能由调用方注入；不传则回落为空。</para>
    /// </summary>
    public async Task<object> GetQueueListAsync(string? type, string? status, DateTime? startTime, DateTime? endTime, int page, int rows,
        string? orgCode = null,
        Func<string, string?, Task<string>>? scopeResolver = null,
        Func<string?, Task<string>>? creatorResolver = null)
    {
        if (page <= 0) page = 1;
        if (rows <= 0) rows = 20;
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();

        var allResult = await orm.GetListAsync<YzhQueue>();
        var query = (allResult.Data ?? new List<YzhQueue>()).AsEnumerable();

        // ★ 租户过滤（H12 / R-03）：传了 orgCode 只看本租户；null = 平台全量
        if (!string.IsNullOrEmpty(orgCode))
            query = query.Where(q => q.OrgCode == orgCode);
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

        // ★ 解析 scopeDisplay：逐个调用 resolver（若有）
        var rowsResult = new List<object>();
        foreach (var q in list)
        {
            // ★ 创建人姓名（CreateBy 是 Code，直接上界面 = 裸 Code 当人名）
            string createByName = "";
            if (creatorResolver != null)
            {
                try { createByName = await creatorResolver(q.CreateBy); }
                catch { createByName = string.Empty; }
            }

            string scopeDisplay = "";
            if (scopeResolver != null)
            {
                try { scopeDisplay = await scopeResolver(q.QueueType, q.ScopeKey); }
                catch { /* 解析失败不影响列表显示 */ }
            }
            rowsResult.Add(new
            {
                queueCode = q.QueueCode,
                queueType = q.QueueType,
                queueName = q.QueueName,
                scopeKey = q.ScopeKey,
                scopeDisplay,
                status = q.Status,
                totalCount = q.TotalCount,
                completedCount = q.CompletedCount,
                failedCount = q.FailedCount,
                processingCount = q.ProcessingCount,
                pendingCount = q.PendingCount,
                cancelledCount = q.CancelledCount,
                progress = q.Progress,
                createBy = q.CreateBy,
                // ★ 已解析的人话姓名（查不到/无创建人 = 空串，前端回落显示 createBy 或「—」）
                createByName,
                sourceType = q.SourceType,
                sourceId = q.SourceId,
                startTime = q.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = q.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                createTime = q.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        return new { total, rows = rowsResult };
    }

    /// <summary>队列详情。<paramref name="orgCode"/> 传入时校验归属，越权返回 null（H12 / R-03「越权读为空」）。</summary>
    public async Task<object?> GetQueueDetailAsync(string queueCode,
        Func<string?, Task<string>>? creatorResolver = null,
        string? orgCode = null)
    {
        using var scope = _serviceProvider.CreateScope();
        var orm = scope.ServiceProvider.GetRequiredService<IDbOrm>();
        var queueResult = await orm.GetOneAsync<YzhQueue>(q => q.QueueCode == queueCode);
        var queue = queueResult.Data;
        if (queue == null) return null;
        // ★ 租户隔离（H12 / R-03）：传了 orgCode 且归属不符 ⇒ 返回 null（越权读为空，不泄露存在性）
        if (!string.IsNullOrEmpty(orgCode) && queue.OrgCode != orgCode) return null;

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
                // ★ 已解析的人话姓名（同列表：CreateBy 是 Code，不能直接当人名渲染）
                createByName = creatorResolver == null ? string.Empty : await SafeResolveCreatorAsync(creatorResolver, queue.CreateBy),
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
