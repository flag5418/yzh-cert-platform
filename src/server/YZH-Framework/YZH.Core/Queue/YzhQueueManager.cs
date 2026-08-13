using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VOL.Core.EFDbContext;

namespace YZH.Core.Queue
{
    /// <summary>
    /// yzh 通用队列引擎（框架核心，跨项目复用）
    /// <para>职责：</para>
    /// <para>1. 创建队列（资源锁 INSERT 冲突检测 + 主表 uk_source 防重复入队 + 子任务）</para>
    /// <para>2. 并发控制（SemaphoreSlim，来自 YzhQueueOptions）</para>
    /// <para>3. 超时控制 + 领取租约（locked_until 防卡死回收）</para>
    /// <para>4. 失败重试（IYzhTaskExecutor 返回 Retryable 分类 + 指数退避）</para>
    /// <para>5. 取消 / 整队重跑 / 单任务重试</para>
    /// <para>6. 进度汇总 + 终态通知（IYzhQueueNotifier）</para>
    /// </summary>
    public class YzhQueueManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<YzhQueueManager> _logger;
        private readonly YzhQueueOptions _options;
        private readonly Dictionary<string, IYzhTaskExecutor> _executors;

        private SemaphoreSlim _semaphore;

        // 运行中的任务取消令牌（用于强制取消）
        private readonly ConcurrentDictionary<long, CancellationTokenSource> _runningTokens = new();

        public YzhQueueManager(
            IServiceProvider serviceProvider,
            ILogger<YzhQueueManager> logger,
            IOptions<YzhQueueOptions> options,
            IEnumerable<IYzhTaskExecutor> executors)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options?.Value ?? new YzhQueueOptions();
            _executors = (executors ?? Enumerable.Empty<IYzhTaskExecutor>())
                .Where(e => !string.IsNullOrEmpty(e.TaskType))
                .GroupBy(e => e.TaskType)
                .ToDictionary(g => g.Key, g => g.First());
            _semaphore = new SemaphoreSlim(_options.MaxConcurrent, _options.MaxConcurrent);
            _logger.LogInformation($"[YzhQueueManager] 队列引擎已初始化：并发 {_options.MaxConcurrent}，超时 {_options.TimeoutSeconds}s，租约 {_options.LeaseMinutes}min");
        }

        /// <summary>资源表名常量（业务侧可按需扩展）</summary>
        public const string RESOURCE_DIR = "cert_standard_directory";
        public const string RESOURCE_FILE = "cert_standard_directory_file";

        #region 入参 DTO

        /// <summary>
        /// 创建队列入参
        /// </summary>
        public class CreateQueueRequest
        {
            public string QueueType { get; set; }
            public string QueueName { get; set; }
            public string ScopeKey { get; set; }
            public string ScopeInfoJson { get; set; }
            public string SourceType { get; set; }
            public string SourceId { get; set; }
            public int UserId { get; set; }
            public string UserName { get; set; }
            public string OrgCode { get; set; }

            /// <summary>队列级资源锁（taskNo=null）与任务级资源锁（taskNo=1..N，与 Tasks 顺序对应）</summary>
            public List<ResourceLockItem> ResourceLocks { get; set; } = new();

            public List<TaskItem> Tasks { get; set; } = new();
        }

        public class ResourceLockItem
        {
            public string ResourceTable { get; set; }
            public string ResourceCode { get; set; }
            public string ResourceName { get; set; }
            public int? TaskNo { get; set; }
        }

        public class TaskItem
        {
            public string TaskType { get; set; }
            public string Payload { get; set; }
            public string TaskId { get; set; }
        }

        #endregion

        #region 创建队列（资源锁冲突检测 + 防重复入队）

        /// <summary>
        /// 创建队列（事务内：资源锁 + 主表 + 子任务）
        /// 任一资源已被其他运行中队列锁定 → INSERT 撞 uk_active 唯一键 → 返回冲突
        /// </summary>
        public async Task<(bool ok, string error, string queueCode, int count)> CreateQueueAsync(CreateQueueRequest req)
        {
            if (req == null || req.Tasks == null || req.Tasks.Count == 0)
                return (true, null, null, 0);
            if (string.IsNullOrEmpty(req.QueueType))
                return (false, "队列类型不能为空", null, 0);

            var queueCode = $"Q-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..20];

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            try
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                // 1. 资源锁
                var lockRows = new List<YzhQueueResourceLock>();
                var taskNo = 1;
                foreach (var item in req.ResourceLocks ?? new List<ResourceLockItem>())
                {
                    lockRows.Add(new YzhQueueResourceLock
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
                        CreateDate = DateTime.Now
                    });
                    taskNo++;
                }
                db.Set<YzhQueueResourceLock>().AddRange(lockRows);
                await db.SaveChangesAsync(); // 撞 uk_active → MySqlException 1062

                // 2. 主表（uk_source 防重复入队：同一来源只能建一次队列）
                var queue = new YzhQueue
                {
                    Code = Guid.NewGuid().ToString("N"),
                    QueueCode = queueCode,
                    QueueType = req.QueueType,
                    QueueName = req.QueueName ?? $"{req.QueueType}-{req.Tasks.Count}个任务",
                    ScopeKey = req.ScopeKey,
                    ScopeInfo = req.ScopeInfoJson,
                    SourceType = req.SourceType,
                    SourceId = req.SourceId,
                    Status = "running",
                    TotalCount = req.Tasks.Count,
                    PendingCount = req.Tasks.Count,
                    Progress = 0,
                    StartTime = DateTime.Now,
                    OrgCode = req.OrgCode,
                    CreateID = req.UserId,
                    Creator = req.UserName,
                    CreateDate = DateTime.Now
                };
                db.Set<YzhQueue>().Add(queue);
                await db.SaveChangesAsync();

                // 3. 子任务（task_no 从 1 开始对应资源锁的 TaskNo）
                taskNo = 1;
                foreach (var task in req.Tasks)
                {
                    var taskLocks = lockRows.Where(r => r.TaskNo == taskNo).Select(r => r.Code).ToArray();
                    db.Set<YzhQueueTask>().Add(new YzhQueueTask
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
                    });
                    taskNo++;
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation($"[YzhQueueManager] 队列已创建: {queueCode}，{req.Tasks.Count} 个任务，type={req.QueueType}，scope={req.ScopeKey}");
                return (true, null, queueCode, req.Tasks.Count);
            }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
            {
                // 资源锁冲突：定位持有者
                var keys = (req.ResourceLocks ?? new List<ResourceLockItem>())
                    .Select(r => $"{r.ResourceTable}|{r.ResourceCode}").ToList();
                var holder = await db.Set<YzhQueueResourceLock>().AsNoTracking()
                    .Where(r => r.Status == "locked" && keys.Contains(r.ActiveKey))
                    .OrderBy(r => r.CreateTime)
                    .FirstOrDefaultAsync();
                var holderQueue = holder != null
                    ? await db.Set<YzhQueue>().AsNoTracking().FirstOrDefaultAsync(q => q.QueueCode == holder.QueueCode)
                    : null;
                var typeName = holderQueue?.QueueType switch
                {
                    "file_convert" => "文档转换",
                    "auto_verify" => "自动核验",
                    "report_generate" => "报告生成",
                    _ => "队列任务"
                };
                return (false, $"资源「{holder?.ResourceName ?? keys.FirstOrDefault()}」正被队列 {holder?.QueueCode}（{typeName}）处理中，请稍后操作", null, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[YzhQueueManager] 创建队列失败: {queueCode}");
                return (false, $"创建队列失败：{ex.Message}", null, 0);
            }
        }

        #endregion

        #region 互斥查询

        /// <summary>
        /// 按 scope_key 查询运行中的队列（pending/running）
        /// </summary>
        public async Task<YzhQueue> FindRunningQueueByScopeKeyAsync(string scopeKey)
        {
            if (string.IsNullOrEmpty(scopeKey)) return null;
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            return await db.Set<YzhQueue>().AsNoTracking()
                .Where(q => q.ScopeKey == scopeKey && (q.Status == "pending" || q.Status == "running"))
                .OrderBy(q => q.CreateDate)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// 资源锁命中信息
        /// </summary>
        public class QueueLockHit
        {
            public string ResourceTable { get; set; }
            public string ResourceCode { get; set; }
            public string ResourceName { get; set; }
            public string QueueCode { get; set; }
            public string QueueType { get; set; }
            public string Status { get; set; }
        }

        /// <summary>
        /// 查询资源（含多编码）是否被锁；命中返回持有者信息，否则 null
        /// </summary>
        public async Task<QueueLockHit> FindResourceLockAsync(string resourceTable, List<string> resourceCodes)
        {
            if (string.IsNullOrEmpty(resourceTable) || resourceCodes == null || resourceCodes.Count == 0)
                return null;
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var keys = resourceCodes.Where(c => !string.IsNullOrEmpty(c)).Select(c => $"{resourceTable}|{c}").ToList();
            if (keys.Count == 0) return null;
            var hit = await db.Set<YzhQueueResourceLock>().AsNoTracking()
                .Where(r => r.Status == "locked" && keys.Contains(r.ActiveKey))
                .OrderBy(r => r.CreateTime)
                .FirstOrDefaultAsync();
            if (hit == null) return null;
            var queue = await db.Set<YzhQueue>().AsNoTracking()
                .FirstOrDefaultAsync(q => q.QueueCode == hit.QueueCode);
            return new QueueLockHit
            {
                ResourceTable = hit.ResourceTable,
                ResourceCode = hit.ResourceCode,
                ResourceName = hit.ResourceName,
                QueueCode = hit.QueueCode,
                QueueType = queue?.QueueType,
                Status = "locked"
            };
        }

        #endregion

        #region 领取与执行（租约）

        /// <summary>
        /// 获取下一个待处理任务（带锁定 + 租约 + 卡死回收）
        /// </summary>
        public async Task<YzhQueueTask> GetNextPendingTaskAsync(string workerId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            // 1. 卡死回收：租约过期的 processing 任务 → 回 pending 重试 / 重试耗尽则 failed
            await ReapStaleTasksAsync(db);

            // 2. 领取下一个 pending（含退避到期条件）
            var task = await db.Set<YzhQueueTask>()
                .FromSqlRaw(@"
                    SELECT * FROM yzh_queue_task
                    WHERE status = 'pending'
                      AND retry_count < max_retry_count
                      AND (next_retry_at IS NULL OR next_retry_at <= NOW())
                    ORDER BY priority DESC, create_time ASC
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED")
                .AsTracking()
                .FirstOrDefaultAsync();

            if (task == null)
                return null;

            // 3. 更新为 processing + 写入租约
            task.Status = "processing";
            task.LockedAt = DateTime.Now;
            task.LockedBy = workerId;
            task.ProcessTime = DateTime.Now;
            task.LockedUntil = DateTime.Now.AddMinutes(_options.LeaseMinutes);
            task.NextRetryAt = null;
            await db.SaveChangesAsync();

            return task;
        }

        /// <summary>
        /// 租约回收：租约过期的 processing 任务视为 Worker 已死
        /// </summary>
        private async Task ReapStaleTasksAsync(VOLContext db)
        {
            var stale = await db.Set<YzhQueueTask>().AsTracking()
                .Where(j => j.Status == "processing" && j.LockedUntil != null && j.LockedUntil < DateTime.Now)
                .ToListAsync();
            if (stale.Count == 0) return;

            foreach (var task in stale)
            {
                task.RetryCount++;
                if (task.RetryCount >= task.MaxRetryCount)
                {
                    task.Status = "failed";
                    task.ErrorType = "retryable";
                    task.ErrorMessage = "任务执行中断（Worker 租约过期），重试次数已耗尽";
                    task.CompleteTime = DateTime.Now;
                    task.LockedUntil = null;
                    await ReleaseTaskLocksByCodesAsync(task.LockCodes);
                    await NotifyTaskStateChangedAsync(task, "failed", task.ErrorMessage);
                }
                else
                {
                    task.Status = "pending";
                    task.ErrorMessage = "任务执行中断（Worker 租约过期），准备重试";
                    task.NextRetryAt = DateTime.Now.AddSeconds(BackoffSeconds(task.RetryCount));
                    task.LockedUntil = null;
                    await NotifyTaskStateChangedAsync(task, "pending", task.ErrorMessage);
                }
            }
            await db.SaveChangesAsync();
            foreach (var task in stale)
                await RefreshQueueProgressAsync(task.QueueCode);
        }

        /// <summary>
        /// 执行任务（并发控制 + 超时 + 错误分类 + 锁释放 + 进度刷新）
        /// </summary>
        public async Task ExecuteTaskAsync(YzhQueueTask task, CancellationToken stoppingToken)
        {
            await _semaphore.WaitAsync(stoppingToken);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            _runningTokens[task.Id] = cts;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

                // 续期租约，并取回同一 DbContext 中已跟踪的实例（避免 Attach 同 Id 双实例冲突）
                var taskRow = await db.Set<YzhQueueTask>().AsTracking().FirstOrDefaultAsync(j => j.Id == task.Id);
                if (taskRow == null)
                {
                    _logger.LogWarning($"[YzhQueueManager] 任务不存在，跳过: {task.Id}");
                    return;
                }
                taskRow.LockedUntil = DateTime.Now.AddMinutes(_options.LeaseMinutes);
                await db.SaveChangesAsync();

                // 执行器分发
                if (!_executors.TryGetValue(taskRow.TaskType, out var executor))
                {
                    throw new NotSupportedException($"没有注册 {taskRow.TaskType} 类型的任务执行器（IYzhTaskExecutor）");
                }

                _logger.LogInformation($"[YzhQueueManager] 开始执行: {taskRow.TaskType} (queue={taskRow.QueueCode}, id={taskRow.Id})");

                var result = await executor.ExecuteAsync(taskRow, cts.Token);
                if (result != null && !result.Success)
                {
                    throw new Exception(result.Message ?? "任务执行失败");
                }

                // 标记任务完成（执行器只负责业务执行，任务状态由队列引擎统一管理）
                taskRow.Status = "completed";
                taskRow.CompleteTime = DateTime.Now;
                taskRow.LockedUntil = null;
                await db.SaveChangesAsync();

                // 成功后释放本任务资源锁
                await ReleaseTaskLocksAsync(taskRow);
                await RefreshQueueProgressAsync(taskRow.QueueCode);
                _logger.LogInformation($"[YzhQueueManager] 任务执行成功: {taskRow.Id}");
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                await HandleTaskFailureAsync(task, "任务执行超时或已取消，任务被强制终止", isCancel: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[YzhQueueManager] 任务执行失败: {task.Id}");
                await HandleTaskFailureAsync(task, ex.InnerException?.Message ?? ex.Message, isCancel: false);
            }
            finally
            {
                _runningTokens.TryRemove(task.Id, out _);
                cts.Dispose();
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 任务失败处理：错误分类（retryable/permanent）→ 退避重试 或 置 failed
        /// </summary>
        private async Task HandleTaskFailureAsync(YzhQueueTask task, string message, bool isCancel)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var taskRow = await db.Set<YzhQueueTask>().AsTracking().FirstOrDefaultAsync(j => j.Id == task.Id);
            if (taskRow == null) return;

            // 取消队列触发的取消（或超时）：若队列已取消则直接置 cancelled；否则走退避重试
            if (isCancel)
            {
                var queue = await db.Set<YzhQueue>().AsNoTracking().FirstOrDefaultAsync(q => q.QueueCode == taskRow.QueueCode);
                if (queue != null && queue.Status == "cancelled")
                {
                    taskRow.Status = "cancelled";
                    taskRow.ErrorMessage = message;
                    taskRow.CompleteTime = DateTime.Now;
                    await db.SaveChangesAsync();
                    await NotifyTaskStateChangedAsync(taskRow, "cancelled", message);
                    await RefreshQueueProgressAsync(taskRow.QueueCode);
                    return;
                }
            }

            taskRow.RetryCount++;
            var retryable = isCancel || ClassifyError(message);
            taskRow.ErrorType = retryable ? "retryable" : "permanent";
            taskRow.ErrorMessage = message;
            if (retryable && taskRow.RetryCount < taskRow.MaxRetryCount)
            {
                // 退避重试（锁保留，防止重试期间资源被改）
                taskRow.Status = "pending";
                taskRow.NextRetryAt = DateTime.Now.AddSeconds(BackoffSeconds(taskRow.RetryCount));
                taskRow.LockedUntil = null;
                await db.SaveChangesAsync();
                await NotifyTaskStateChangedAsync(taskRow, "pending", message);
            }
            else
            {
                taskRow.Status = "failed";
                taskRow.CompleteTime = DateTime.Now;
                taskRow.LockedUntil = null;
                await db.SaveChangesAsync();
                await ReleaseTaskLocksByCodesAsync(taskRow.LockCodes);
                // 最终失败：通知业务侧恢复资源可见性
                await NotifyTaskStateChangedAsync(taskRow, "failed", message);
            }
            await RefreshQueueProgressAsync(taskRow.QueueCode);
        }

        /// <summary>
        /// 通用错误分类：文件类/格式类问题为永久错误（重试无意义），其余可重试
        /// 业务侧更精确的分类可在 IYzhTaskExecutor.ExecuteAsync 返回 Retryable=false 覆盖
        /// </summary>
        private bool ClassifyError(string message)
        {
            if (string.IsNullOrEmpty(message)) return true;
            var m = message.ToLower();
            var permanent = new[]
            {
                "不存在", "已被修改", "已被删除", "不是合法", "不支持", "无法解析",
                "非法", "损坏", "libreoffice 不可用", "权限", "拒绝访问", "没有注册"
            };
            return !permanent.Any(p => m.Contains(p));
        }

        /// <summary>
        /// 指数退避 + 抖动（5s→15s→45s→135s，上限 5 分钟）
        /// </summary>
        private int BackoffSeconds(int retryCount)
        {
            var secs = 5 * (int)Math.Pow(3, Math.Max(0, retryCount - 1));
            if (secs > 300) secs = 300;
            return secs + Random.Shared.Next(0, 5);
        }

        /// <summary>
        /// 通知业务侧任务状态变更（文件可见性联动等）
        /// </summary>
        private async Task NotifyTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            try
            {
                if (_executors.TryGetValue(task.TaskType, out var executor))
                    await executor.OnTaskStateChangedAsync(task, newStatus, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[YzhQueueManager] 任务状态变更回调失败: {task.Id} -> {newStatus}");
            }
        }

        #endregion

        #region 取消 / 重试

        /// <summary>
        /// 取消指定来源批次的所有任务（兼容旧接口，按 taskId）
        /// </summary>
        public async Task CancelBatchAsync(string taskId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var queueCodes = await db.Set<YzhQueueTask>()
                .Where(j => j.TaskId == taskId && j.QueueCode != null)
                .Select(j => j.QueueCode)
                .Distinct()
                .ToListAsync();
            foreach (var queueCode in queueCodes)
                await CancelQueueAsync(queueCode);

            if (!string.IsNullOrEmpty(taskId))
            {
                using var nScope = _serviceProvider.CreateScope();
                var notifier = nScope.ServiceProvider.GetRequiredService<IYzhQueueNotifier>();
                // 兼容旧 SignalR 事件（业务侧扩展可用）
            }
        }

        /// <summary>
        /// 取消指定队列（终止 processing + pending→cancelled + 批量释放锁 + 主表终态 + 通知）
        /// </summary>
        public async Task<(bool ok, string error)> CancelQueueAsync(string queueCode)
        {
            if (string.IsNullOrEmpty(queueCode)) return (false, "队列编码不能为空");
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var queue = await db.Set<YzhQueue>().AsTracking().FirstOrDefaultAsync(q => q.QueueCode == queueCode);
            if (queue == null) return (false, "队列不存在");
            if (IsTerminal(queue.Status)) return (false, "队列已结束，无需取消");

            // 1. 主表先置终态（先于取消令牌，保证处理中任务的失败处理能看到 cancelled，不会误走重试）
            queue.Status = "cancelled";
            queue.EndTime = DateTime.Now;
            queue.CancelledCount = queue.TotalCount - queue.CompletedCount - queue.FailedCount;
            await db.SaveChangesAsync();

            // 2. 终止 processing（令牌取消后，对应任务由 HandleTaskFailureAsync 置 cancelled）
            var processingTasks = await db.Set<YzhQueueTask>()
                .Where(j => j.QueueCode == queueCode && j.Status == "processing")
                .ToListAsync();
            foreach (var task in processingTasks)
                if (_runningTokens.TryRemove(task.Id, out var cts)) cts.Cancel();

            // 3. pending → cancelled
            await db.Set<YzhQueueTask>()
                .Where(j => j.QueueCode == queueCode && j.Status == "pending")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, "cancelled")
                    .SetProperty(j => j.CompleteTime, DateTime.Now));

            // 3.1 恢复本队列涉及资源的可见性（业务侧联动）
            var tasks = await db.Set<YzhQueueTask>().AsNoTracking()
                .Where(j => j.QueueCode == queueCode)
                .ToListAsync();
            foreach (var task in tasks)
                await NotifyTaskStateChangedAsync(task, "cancelled", "任务已取消，可重新发起");

            // 4. 批量释放锁
            await db.Set<YzhQueueResourceLock>()
                .Where(r => r.QueueCode == queueCode && r.Status == "locked")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "released")
                    .SetProperty(r => r.ActiveKey, (string)null)
                    .SetProperty(r => r.ReleaseTime, DateTime.Now));
            await db.SaveChangesAsync();

            await NotifyQueueTerminalAsync(queue);
            _logger.LogInformation($"[YzhQueueManager] 队列已取消: {queueCode}");
            return (true, null);
        }

        /// <summary>
        /// 单个子任务重试（failed/cancelled → pending）
        /// </summary>
        public async Task<(bool ok, string error)> RetryTaskAsync(long taskId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var task = await db.Set<YzhQueueTask>().AsTracking().FirstOrDefaultAsync(j => j.Id == taskId);
            if (task == null) return (false, "任务不存在");
            if (task.Status != "failed" && task.Status != "cancelled")
                return (false, "仅失败/取消的任务可重试");

            var relock = await RelockTaskAsync(db, task);
            if (!relock.ok) return relock;

            task.Status = "pending";
            task.RetryCount = 0;
            task.ErrorMessage = null;
            task.ErrorType = null;
            task.NextRetryAt = DateTime.Now;
            task.CompleteTime = null;
            await db.SaveChangesAsync();

            // 重试期间重新隐藏资源（业务侧联动）
            await NotifyTaskStateChangedAsync(task, "pending", null);

            await RefreshQueueProgressAsync(task.QueueCode);
            return (true, null);
        }

        /// <summary>
        /// 整队重跑（failed/cancelled → pending）
        /// </summary>
        public async Task<(bool ok, string error)> RetryQueueAsync(string queueCode)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var queue = await db.Set<YzhQueue>().AsTracking().FirstOrDefaultAsync(q => q.QueueCode == queueCode);
            if (queue == null) return (false, "队列不存在");
            if (!IsTerminal(queue.Status)) return (false, "仅已结束的队列可整队重跑");

            var tasks = await db.Set<YzhQueueTask>().AsTracking()
                .Where(j => j.QueueCode == queueCode && (j.Status == "failed" || j.Status == "cancelled"))
                .ToListAsync();
            if (tasks.Count == 0) return (false, "没有可重试的任务");

            // 重新加锁（任一资源已被其他队列占用则整体失败）
            foreach (var task in tasks)
            {
                var relock = await RelockTaskAsync(db, task);
                if (!relock.ok) return relock;
                task.Status = "pending";
                task.RetryCount = 0;
                task.ErrorMessage = null;
                task.ErrorType = null;
                task.NextRetryAt = DateTime.Now;
                task.CompleteTime = null;
            }
            await db.SaveChangesAsync();

            // 重试期间重新隐藏资源
            foreach (var task in tasks)
                await NotifyTaskStateChangedAsync(task, "pending", null);

            await RefreshQueueProgressAsync(queueCode);
            return (true, null);
        }

        /// <summary>
        /// 重新加锁（锁行已释放 → 置回 locked；撞 uk_active 则冲突）
        /// </summary>
        private async Task<(bool ok, string error)> RelockTaskAsync(VOLContext db, YzhQueueTask task)
        {
            var codes = (task.LockCodes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (codes.Length == 0) return (true, null);
            try
            {
                await db.Set<YzhQueueResourceLock>()
                    .Where(r => codes.Contains(r.Code) && r.Status == "released")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.Status, "locked")
                        .SetProperty(r => r.ActiveKey, r => r.ResourceTable + "|" + r.ResourceCode)
                        .SetProperty(r => r.ReleaseTime, (DateTime?)null));
                return (true, null);
            }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
            {
                var name = await db.Set<YzhQueueResourceLock>().AsNoTracking()
                    .Where(r => codes.Contains(r.Code)).Select(r => r.ResourceName).FirstOrDefaultAsync();
                return (false, $"资源「{name}」已被其他队列锁定，无法重试，请稍后再试");
            }
        }

        #endregion

        #region 进度与查询

        /// <summary>
        /// 汇总子表刷新主表进度/状态；进入终态时批量释放锁并发送终态通知
        /// </summary>
        private async Task RefreshQueueProgressAsync(string queueCode)
        {
            if (string.IsNullOrEmpty(queueCode)) return;
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var queue = await db.Set<YzhQueue>().AsTracking().FirstOrDefaultAsync(q => q.QueueCode == queueCode);
            if (queue == null) return;

            var tasks = await db.Set<YzhQueueTask>().AsNoTracking().Where(j => j.QueueCode == queueCode).ToListAsync();
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
                // 重试后重新开始
                queue.EndTime = null;
                queue.StartTime = DateTime.Now;
            }
            queue.Status = newStatus;

            if (IsTerminal(newStatus))
            {
                queue.EndTime = DateTime.Now;
                // 批量释放剩余锁
                await db.Set<YzhQueueResourceLock>()
                    .Where(r => r.QueueCode == queueCode && r.Status == "locked")
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.Status, "released")
                        .SetProperty(r => r.ActiveKey, (string)null)
                        .SetProperty(r => r.ReleaseTime, DateTime.Now));
            }
            await db.SaveChangesAsync();

            if (IsTerminal(newStatus) && !wasTerminal)
            {
                await NotifyQueueTerminalAsync(queue);
            }
        }

        private static bool IsTerminal(string status)
            => status == "completed" || status == "failed" || status == "cancelled";

        /// <summary>
        /// 终态通知：交业务侧实现（消息落库 + SignalR 等）
        /// </summary>
        private async Task NotifyQueueTerminalAsync(YzhQueue queue)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var notifier = scope.ServiceProvider.GetRequiredService<IYzhQueueNotifier>();
                await notifier.NotifyAsync(queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[YzhQueueManager] 终态通知发送失败: {queue.QueueCode}");
            }
        }

        /// <summary>
        /// 获取批次进度（兼容旧接口 ConvertProgressPanel）
        /// </summary>
        public async Task<object> GetBatchProgressAsync(string taskId)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var tasks = await db.Set<YzhQueueTask>().AsNoTracking()
                .Where(j => j.TaskId == taskId)
                .ToListAsync();

            return new
            {
                taskId,
                total = tasks.Count,
                completed = tasks.Count(j => j.Status == "completed"),
                failed = tasks.Count(j => j.Status == "failed"),
                processing = tasks.Count(j => j.Status == "processing"),
                pending = tasks.Count(j => j.Status == "pending"),
                cancelled = tasks.Count(j => j.Status == "cancelled"),
                isFinished = tasks.All(j => j.Status == "completed" || j.Status == "failed" || j.Status == "cancelled")
            };
        }

        /// <summary>
        /// 全局队列状态（兼容旧接口）
        /// </summary>
        public async Task<object> GetQueueStatusAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var tasks = await db.Set<YzhQueueTask>().AsNoTracking().ToListAsync();
            var queues = await db.Set<YzhQueue>().AsNoTracking().ToListAsync();

            return new
            {
                totalPending = tasks.Count(j => j.Status == "pending"),
                totalProcessing = tasks.Count(j => j.Status == "processing"),
                totalCompleted = tasks.Count(j => j.Status == "completed"),
                totalFailed = tasks.Count(j => j.Status == "failed"),
                totalCancelled = tasks.Count(j => j.Status == "cancelled"),
                runningQueues = queues.Count(q => q.Status == "running"),
                pendingQueues = queues.Count(q => q.Status == "pending"),
                maxConcurrent = _options.MaxConcurrent,
                timeoutSeconds = _options.TimeoutSeconds,
                runningWorkers = _options.MaxConcurrent - _semaphore.CurrentCount
            };
        }

        /// <summary>
        /// 队列监控统计卡
        /// </summary>
        public async Task<object> GetQueueStatsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var queues = await db.Set<YzhQueue>().AsNoTracking().ToListAsync();
            var today = DateTime.Today;
            return new
            {
                runningQueues = queues.Count(q => q.Status == "running"),
                pendingQueues = queues.Count(q => q.Status == "pending"),
                todayCompleted = queues.Count(q => q.Status == "completed" && q.EndTime >= today),
                todayFailed = queues.Count(q => q.Status == "failed" && q.EndTime >= today),
                todayCancelled = queues.Count(q => q.Status == "cancelled" && q.EndTime >= today),
                maxConcurrent = _options.MaxConcurrent,
                runningWorkers = _options.MaxConcurrent - _semaphore.CurrentCount
            };
        }

        /// <summary>
        /// 队列主表分页（Tabs + 时间过滤）
        /// </summary>
        public async Task<object> GetQueueListAsync(string type, string status, DateTime? startTime, DateTime? endTime, int page, int rows)
        {
            if (page <= 0) page = 1;
            if (rows <= 0) rows = 20;
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var query = db.Set<YzhQueue>().AsNoTracking().Where(q => q.DeleteTime == null);
            if (!string.IsNullOrEmpty(type)) query = query.Where(q => q.QueueType == type);
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "executing")
                    query = query.Where(q => q.Status == "pending" || q.Status == "running");
                else
                    query = query.Where(q => q.Status == status);
            }
            if (startTime.HasValue) query = query.Where(q => q.CreateDate >= startTime.Value);
            if (endTime.HasValue) query = query.Where(q => q.CreateDate <= endTime.Value);

            var total = await query.CountAsync();
            var list = await query.OrderByDescending(q => q.CreateDate)
                .Skip((page - 1) * rows).Take(rows)
                .ToListAsync();

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
                    creator = q.Creator,
                    sourceType = q.SourceType,
                    sourceId = q.SourceId,
                    startTime = q.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    endTime = q.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    createDate = q.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList()
            };
        }

        /// <summary>
        /// 队列详情（主表 + 子任务明细 + 资源锁列表）
        /// </summary>
        public async Task<object> GetQueueDetailAsync(string queueCode)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var queue = await db.Set<YzhQueue>().AsNoTracking()
                .FirstOrDefaultAsync(q => q.QueueCode == queueCode);
            if (queue == null) return null;

            var tasks = await db.Set<YzhQueueTask>().AsNoTracking()
                .Where(j => j.QueueCode == queueCode)
                .OrderBy(j => j.CreateTime)
                .ToListAsync();
            var locks = await db.Set<YzhQueueResourceLock>().AsNoTracking()
                .Where(r => r.QueueCode == queueCode)
                .OrderBy(r => r.CreateTime)
                .ToListAsync();

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
                    creator = queue.Creator,
                    sourceType = queue.SourceType,
                    sourceId = queue.SourceId,
                    startTime = queue.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    endTime = queue.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    createDate = queue.CreateDate?.ToString("yyyy-MM-dd HH:mm:ss")
                },
                tasks = tasks.Select((j, i) => new
                {
                    id = j.Id,
                    taskNo = i + 1,
                    taskType = j.TaskType,
                    payload = TryParsePayload(j.Payload),
                    status = j.Status,
                    retryCount = j.RetryCount,
                    errorType = j.ErrorType,
                    errorMessage = j.ErrorMessage,
                    processTime = j.ProcessTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    completeTime = j.CompleteTime?.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList(),
                locks = locks.Select(r => new
                {
                    code = r.Code,
                    resourceTable = r.ResourceTable,
                    resourceCode = r.ResourceCode,
                    resourceName = r.ResourceName,
                    taskNo = r.TaskNo,
                    status = r.Status,
                    createTime = r.CreateTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    releaseTime = r.ReleaseTime?.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList()
            };
        }

        private static object TryParsePayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;
            try { return JsonSerializer.Deserialize<JsonElement>(payload); }
            catch { return payload; }
        }

        #endregion

        #region 锁释放

        /// <summary>
        /// 释放任务持有的资源锁
        /// </summary>
        public async Task ReleaseTaskLocksAsync(YzhQueueTask task)
            => await ReleaseTaskLocksByCodesAsync(task?.LockCodes);

        private async Task ReleaseTaskLocksByCodesAsync(string lockCodes)
        {
            if (string.IsNullOrEmpty(lockCodes)) return;
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();
            var codes = lockCodes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (codes.Length == 0) return;
            await db.Set<YzhQueueResourceLock>()
                .Where(r => codes.Contains(r.Code) && r.Status == "locked")
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, "released")
                    .SetProperty(r => r.ActiveKey, (string)null)
                    .SetProperty(r => r.ReleaseTime, DateTime.Now));
        }

        #endregion
    }
}
