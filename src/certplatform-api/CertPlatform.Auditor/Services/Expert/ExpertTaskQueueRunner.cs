using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Expert;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Expert
{
    /// <summary>
    /// 专家任务队列后台 Worker —— 领取并执行 <c>cert_expert_task_queue_item</c>。
    ///
    /// <para><b>★ 与框架 <c>QueueHostedService</c> 的关系</b>：<b>形状同构、表不同</b>。
    /// 调度机制（租约 / 指数退避 / 卡死回收）照搬已验证实现，
    /// 但读写走专用表并<b>强制 OrgCode 过滤</b>（D10：不复用 <c>yzh_queue</c>，理由见 15 号 H12）。</para>
    ///
    /// <para><b>★ 同一队列内串行</b>：领取条件要求「该队列当前无 running 项」。
    /// 这保证同一标准下的检查项按 <c>Seq</c> 顺序执行，不会并发打爆 LLM 配额，
    /// 也让日志顺序与业务顺序一致（可读）。</para>
    ///
    /// <para><b>★ 乐观领取</b>：先 SELECT 候选，再用
    /// <c>UPDATE ... WHERE Code=@code AND ItemStatus='pending'</c> 抢占，
    /// 检查影响行数 = 1 才算拿到。比 <c>FOR UPDATE SKIP LOCKED</c> 更易读且同样安全。</para>
    /// </summary>
    public class ExpertTaskQueueRunner : BackgroundService
    {
        private const int MaxConcurrent = 3;

        /// <summary>租约时长（10 分钟 &gt; 单项 LLM 超时上限）</summary>
        private const int LeaseMinutes = 10;

        private readonly IServiceProvider _sp;
        private readonly ILogger<ExpertTaskQueueRunner> _logger;
        private readonly string _workerId;
        private readonly SemaphoreSlim _sem = new(MaxConcurrent, MaxConcurrent);

        public ExpertTaskQueueRunner(IServiceProvider sp, ILogger<ExpertTaskQueueRunner> logger)
        {
            _sp = sp;
            _logger = logger;
            _workerId = $"expert-{Environment.MachineName}-{Guid.NewGuid():N}";
            if (_workerId.Length > 50) _workerId = _workerId[..50];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ExpertQueue] 后台服务已启动 workerId={WorkerId}", _workerId);

            try
            {
                var reaped = await ReapStaleAsync(stoppingToken);
                if (reaped > 0)
                    _logger.LogInformation("[ExpertQueue] 启动回收中断项 {Count} 个", reaped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpertQueue] 启动回收失败");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var claimed = await ClaimNextAsync(stoppingToken);
                    if (claimed != null)
                    {
                        _ = Task.Run(() => RunOneAsync(claimed, stoppingToken), stoppingToken);
                        continue; // 立即尝试领取下一个
                    }
                    await Task.Delay(1500, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ExpertQueue] 调度循环异常");
                    await Task.Delay(3000, stoppingToken);
                }
            }

            _logger.LogInformation("[ExpertQueue] 后台服务已停止");
        }

        // ================================================================
        // 一、领取
        // ================================================================

        private async Task<CertExpertTaskQueueItem?> ClaimNextAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();

            // ① 候选：所属队列在跑 + 该队列当前无 running 项（★ 同队列串行）+ 未到重试时间
            const string pickSql = @"
SELECT i.Code
FROM cert_expert_task_queue_item i
INNER JOIN cert_expert_task_queue q ON q.Code = i.QueueCode
WHERE q.QueueStatus = 'running' AND q.IsDeleted = 0 AND q.IsValid = 1
  AND i.ItemStatus = 'pending' AND i.IsDeleted = 0 AND i.IsValid = 1
  AND i.RetryCount < i.MaxRetryCount
  AND (i.NextRetryAt IS NULL OR i.NextRetryAt <= NOW())
  AND NOT EXISTS (
        SELECT 1 FROM cert_expert_task_queue_item r
        WHERE r.QueueCode = i.QueueCode AND r.ItemStatus = 'running' AND r.IsDeleted = 0)
ORDER BY q.Priority DESC, i.Seq ASC, i.CreateTime ASC
LIMIT 1";

            var picked = await db.SqlScalarAsync<string>(pickSql);
            if (!picked.Success || string.IsNullOrWhiteSpace(picked.Data)) return null;

            var code = picked.Data!;
            var now = DateTime.Now;

            // ② 乐观抢占：只有仍为 pending 才拿得到
            var affected = await db.SqlExecuteAsync(
                @"UPDATE cert_expert_task_queue_item
                  SET ItemStatus = 'running', LockCode = @worker, LockedUntil = @until,
                      StartTime = IFNULL(StartTime, @now), UpdateTime = @now
                  WHERE Code = @code AND ItemStatus = 'pending' AND IsDeleted = 0",
                new { worker = _workerId, until = now.AddMinutes(LeaseMinutes), now, code });

            if (!affected.Success || affected.Data != 1) return null;

            var item = (await db.GetOneAsync<CertExpertTaskQueueItem>(x => x.Code == code)).Data;
            if (item == null) return null;

            await SafeLogAsync(db, item, ExpertTaskConst.LogAction.QueueProgress,
                ExpertTaskConst.LogLevel.Info,
                $"开始执行：{item.ItemType} / {item.ItemCode}", null, now);
            return item;
        }

        /// <summary>回收租约过期的 running 项（进程崩溃 / 卡死）</summary>
        private async Task<int> ReapStaleAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();

            var stale = (await db.GetListAsync<CertExpertTaskQueueItem>(x =>
                x.ItemStatus == ExpertTaskConst.Item.Running
                && x.LockedUntil != null && x.LockedUntil < DateTime.Now
                && !x.IsDeleted)).Data ?? new List<CertExpertTaskQueueItem>();

            var now = DateTime.Now;
            foreach (var it in stale)
            {
                it.RetryCount++;
                it.LockCode = null;
                it.LockedUntil = null;
                it.UpdateTime = now;

                if (it.RetryCount >= it.MaxRetryCount)
                {
                    it.ItemStatus = ExpertTaskConst.Item.Failed;
                    it.ErrorType = "retryable";
                    it.ErrorMessage = "Worker 租约过期，任务执行中断，重试次数已耗尽";
                    it.FinishTime = now;
                }
                else
                {
                    it.ItemStatus = ExpertTaskConst.Item.Pending;
                    it.NextRetryAt = now.AddSeconds(BackoffSeconds(it.RetryCount));
                    it.ErrorMessage = "Worker 租约过期，准备重试";
                }

                await db.UpdateAsync(it,
                    nameof(CertExpertTaskQueueItem.ItemStatus),
                    nameof(CertExpertTaskQueueItem.RetryCount),
                    nameof(CertExpertTaskQueueItem.LockCode),
                    nameof(CertExpertTaskQueueItem.LockedUntil),
                    nameof(CertExpertTaskQueueItem.NextRetryAt),
                    nameof(CertExpertTaskQueueItem.ErrorType),
                    nameof(CertExpertTaskQueueItem.ErrorMessage),
                    nameof(CertExpertTaskQueueItem.FinishTime),
                    nameof(CertExpertTaskQueueItem.UpdateTime));

                await SafeLogAsync(db, it, ExpertTaskConst.LogAction.ItemExtractRetry,
                    ExpertTaskConst.LogLevel.Warn, "租约过期回收：" + it.ErrorMessage, null, now);
            }

            return stale.Count;
        }

        // ================================================================
        // 二、执行
        // ================================================================

        private async Task RunOneAsync(CertExpertTaskQueueItem item, CancellationToken stoppingToken)
        {
            await _sem.WaitAsync(stoppingToken);
            var started = DateTime.Now;
            ItemExecOutcome outcome;

            try
            {
                using var scope = _sp.CreateScope();
                var sp = scope.ServiceProvider;

                var executors = sp.GetServices<IExpertItemExecutor>()
                    .ToDictionary(x => x.ItemType, x => x, StringComparer.OrdinalIgnoreCase);

                if (!executors.TryGetValue(item.ItemType, out var executor))
                {
                    outcome = new ItemExecOutcome
                    {
                        Success = false,
                        Retryable = false,
                        Message = $"未注册 {item.ItemType} 类型的执行器"
                    };
                }
                else
                {
                    var payload = ExpertItemPayload.Parse(item.Payload);
                    outcome = await executor.ExecuteAsync(item, payload, sp, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                outcome = new ItemExecOutcome
                {
                    Success = false,
                    Retryable = true,
                    Message = "服务停止，执行中断"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpertQueue] 执行异常 item={Code}", item.Code);
                outcome = new ItemExecOutcome
                {
                    Success = false,
                    Retryable = true,
                    Message = ex.Message
                };
            }
            finally
            {
                _sem.Release();
            }

            var durationMs = (int)(DateTime.Now - started).TotalMilliseconds;

            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
                await FinalizeAsync(db, item, outcome, durationMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpertQueue] 收口失败 item={Code}", item.Code);
            }
        }

        /// <summary>收口：写项状态 → 重算队列 → 重算任务 → 写日志</summary>
        private async Task FinalizeAsync(
            IDbOrm db, CertExpertTaskQueueItem item, ItemExecOutcome outcome, int durationMs)
        {
            var now = DateTime.Now;
            var fresh = (await db.GetOneAsync<CertExpertTaskQueueItem>(x => x.Code == item.Code)).Data;
            if (fresh == null) return;

            string action;
            string level;

            if (outcome.Success)
            {
                fresh.ItemStatus = outcome.IsSkipped
                    ? ExpertTaskConst.Item.Skipped
                    : ExpertTaskConst.Item.Completed;
                fresh.ErrorType = null;
                fresh.ErrorMessage = null;
                fresh.NextRetryAt = null;
                action = outcome.IsSkipped
                    ? ExpertTaskConst.LogAction.ItemSkip
                    : ExpertTaskConst.LogAction.ItemExtractOk;
                level = outcome.IsSkipped ? ExpertTaskConst.LogLevel.Warn : ExpertTaskConst.LogLevel.Info;
            }
            else
            {
                fresh.RetryCount++;
                var canRetry = outcome.Retryable && fresh.RetryCount < fresh.MaxRetryCount;
                if (canRetry)
                {
                    fresh.ItemStatus = ExpertTaskConst.Item.Pending;
                    fresh.NextRetryAt = now.AddSeconds(BackoffSeconds(fresh.RetryCount));
                    action = ExpertTaskConst.LogAction.ItemExtractRetry;
                    level = ExpertTaskConst.LogLevel.Warn;
                }
                else
                {
                    fresh.ItemStatus = ExpertTaskConst.Item.Failed;
                    fresh.NextRetryAt = null;
                    action = ExpertTaskConst.LogAction.ItemExtractFail;
                    level = ExpertTaskConst.LogLevel.Error;
                }
                fresh.ErrorType = outcome.Retryable ? "retryable" : "permanent";
                fresh.ErrorMessage = ExpertNcCheckExecutor.Truncate(outcome.Message, 2000);
            }

            fresh.FinishTime = outcome.Success ? now : fresh.FinishTime;
            fresh.LockCode = null;
            fresh.LockedUntil = null;
            fresh.UpdateTime = now;

            await db.UpdateAsync(fresh,
                nameof(CertExpertTaskQueueItem.ItemStatus),
                nameof(CertExpertTaskQueueItem.RetryCount),
                nameof(CertExpertTaskQueueItem.ErrorType),
                nameof(CertExpertTaskQueueItem.ErrorMessage),
                nameof(CertExpertTaskQueueItem.NextRetryAt),
                nameof(CertExpertTaskQueueItem.FinishTime),
                nameof(CertExpertTaskQueueItem.LockCode),
                nameof(CertExpertTaskQueueItem.LockedUntil),
                nameof(CertExpertTaskQueueItem.UpdateTime));

            await SafeLogAsync(db, fresh, action, level,
                outcome.Message, durationMs, now);

            await RefreshQueueAsync(db, fresh.QueueCode ?? "", now);
            await RefreshTaskAsync(db, fresh.TaskCode ?? "", now);
        }

        /// <summary>重算队列计数与状态</summary>
        private async Task RefreshQueueAsync(IDbOrm db, string queueCode, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(queueCode)) return;

            var q = (await db.GetOneAsync<CertExpertTaskQueue>(x => x.Code == queueCode)).Data;
            if (q == null) return;

            var items = (await db.GetListAsync<CertExpertTaskQueueItem>(x =>
                x.QueueCode == queueCode && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskQueueItem>();

            var total = items.Count;
            var done = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Completed);
            var failed = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Failed);
            var skipped = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Skipped);
            var pending = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Pending);
            var running = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Running);

            q.TotalCount = total;
            q.DoneCount = done;
            q.FailedCount = failed;
            q.SkippedCount = skipped;
            q.Progress = total > 0
                ? Math.Round((decimal)(done + failed + skipped) / total * 100, 2) : 0m;

            if (pending > 0 || running > 0)
            {
                // 保持原状态：running 才是「可领取」，pending 是暂停态
            }
            else if (failed > 0)
            {
                q.QueueStatus = ExpertTaskConst.Queue.Failed;
                q.FinishTime = now;
            }
            else
            {
                q.QueueStatus = ExpertTaskConst.Queue.Completed;
                q.FinishTime = now;
            }

            q.LockCode = null;
            q.LockedUntil = null;
            q.UpdateTime = now;

            await db.UpdateAsync(q,
                nameof(CertExpertTaskQueue.TotalCount),
                nameof(CertExpertTaskQueue.DoneCount),
                nameof(CertExpertTaskQueue.FailedCount),
                nameof(CertExpertTaskQueue.SkippedCount),
                nameof(CertExpertTaskQueue.Progress),
                nameof(CertExpertTaskQueue.QueueStatus),
                nameof(CertExpertTaskQueue.FinishTime),
                nameof(CertExpertTaskQueue.LockCode),
                nameof(CertExpertTaskQueue.LockedUntil),
                nameof(CertExpertTaskQueue.UpdateTime));

            // ★ 同步回写「标准子任务」——否则详情 Tab1 永远停在「待启动」
            //   （队列已 completed 而子任务还显示 pending_run，是静默不一致）。
            await RefreshSubTaskAsync(db, q, items, now);
        }

        /// <summary>重算标准子任务（<c>cert_expert_task_standard</c>）的计数与执行状态</summary>
        private static async Task RefreshSubTaskAsync(
            IDbOrm db, CertExpertTaskQueue q,
            List<CertExpertTaskQueueItem> items, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(q.SubTaskCode)) return;

            var sub = (await db.GetOneAsync<CertExpertTaskStandard>(x =>
                x.Code == q.SubTaskCode)).Data;
            if (sub == null) return;

            var total = items.Count;
            var done = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Completed);
            var failed = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Failed);
            var skipped = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Skipped);
            var pending = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Pending);
            var running = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Running);

            sub.ItemCount = total;
            sub.DoneCount = done;
            sub.FailedCount = failed;
            sub.SkippedCount = skipped;
            sub.PendingCount = pending + running;
            sub.Progress = total > 0
                ? Math.Round((decimal)(done + failed + skipped) / total * 100, 2) : 0m;

            var finished = pending == 0 && running == 0;
            if (finished)
            {
                sub.ExecStatus = failed > 0
                    ? ExpertTaskConst.Exec.Failed
                    : ExpertTaskConst.Exec.Completed;
                sub.FinishTime ??= now;
            }
            else if (sub.ExecStatus == ExpertTaskConst.Exec.PendingRun
                     || sub.ExecStatus == ExpertTaskConst.Exec.Failed)
            {
                sub.ExecStatus = ExpertTaskConst.Exec.Running;
            }

            sub.StartTime ??= q.StartTime;
            sub.LastError = q.LastError;
            sub.UpdateTime = now;

            await db.UpdateAsync(sub,
                nameof(CertExpertTaskStandard.ItemCount),
                nameof(CertExpertTaskStandard.DoneCount),
                nameof(CertExpertTaskStandard.FailedCount),
                nameof(CertExpertTaskStandard.SkippedCount),
                nameof(CertExpertTaskStandard.PendingCount),
                nameof(CertExpertTaskStandard.Progress),
                nameof(CertExpertTaskStandard.ExecStatus),
                nameof(CertExpertTaskStandard.StartTime),
                nameof(CertExpertTaskStandard.FinishTime),
                nameof(CertExpertTaskStandard.LastError),
                nameof(CertExpertTaskStandard.UpdateTime));
        }

        /// <summary>重算任务计数与执行状态（★ 全部队列终态 ⇒ 任务 completed，<b>D36 锁自动释放</b>）</summary>
        private async Task RefreshTaskAsync(IDbOrm db, string taskCode, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(taskCode)) return;

            var task = (await db.GetOneAsync<CertExpertTask>(x => x.Code == taskCode)).Data;
            if (task == null) return;

            var items = (await db.GetListAsync<CertExpertTaskQueueItem>(x =>
                x.TaskCode == taskCode && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskQueueItem>();

            var total = items.Count;
            var done = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Completed);
            var failed = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Failed);
            var skipped = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Skipped);
            var pending = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Pending);
            var running = items.Count(i => i.ItemStatus == ExpertTaskConst.Item.Running);

            task.TotalItemCount = total;
            task.FailedCount = failed;
            task.SkippedCount = skipped;
            task.PendingCount = pending + running;
            task.Progress = total > 0
                ? Math.Round((decimal)(done + failed + skipped) / total * 100, 2) : 0m;

            var finished = pending == 0 && running == 0;
            if (finished)
            {
                task.ExecStatus = failed > 0
                    ? ExpertTaskConst.Exec.Failed
                    : ExpertTaskConst.Exec.Completed;
                task.FinishTime ??= now;
            }
            else if (task.ExecStatus == ExpertTaskConst.Exec.PendingRun
                     || task.ExecStatus == ExpertTaskConst.Exec.Failed)
            {
                task.ExecStatus = ExpertTaskConst.Exec.Running;
            }

            // 缺口数（未处理）
            var gapCount = (await db.CountAsync<CertExpertTaskDataGap>(x =>
                x.TaskCode == taskCode && x.GapStatus == "pending" && !x.IsDeleted)).Data;
            task.GapCount = gapCount;

            task.UpdateTime = now;

            await db.UpdateAsync(task,
                nameof(CertExpertTask.TotalItemCount),
                nameof(CertExpertTask.FailedCount),
                nameof(CertExpertTask.SkippedCount),
                nameof(CertExpertTask.PendingCount),
                nameof(CertExpertTask.Progress),
                nameof(CertExpertTask.ExecStatus),
                nameof(CertExpertTask.FinishTime),
                nameof(CertExpertTask.GapCount),
                nameof(CertExpertTask.UpdateTime));

            if (finished)
            {
                await SafeLogAsync(db, new CertExpertTaskQueueItem
                {
                    TaskCode = taskCode,
                    OrgCode = task.OrgCode
                }, ExpertTaskConst.LogAction.TaskFinished,
                    failed > 0 ? ExpertTaskConst.LogLevel.Warn : ExpertTaskConst.LogLevel.Info,
                    $"任务执行结束：完成 {done} / 跳过 {skipped} / 失败 {failed}（共 {total}）",
                    null, now);
            }
        }

        // ================================================================
        // 三、辅助
        // ================================================================

        private static int BackoffSeconds(int retryCount)
        {
            var secs = 5 * (int)Math.Pow(3, Math.Max(0, retryCount - 1));
            if (secs > 300) secs = 300;
            return secs + Random.Shared.Next(0, 5);
        }

        /// <summary>写任务日志（⛔ 日志失败绝不影响主流程）</summary>
        private async Task SafeLogAsync(
            IDbOrm db, CertExpertTaskQueueItem item, string action, string level,
            string? message, int? durationMs, DateTime now)
        {
            try
            {
                await db.InsertAsync(new CertExpertTaskLog
                {
                    Code = Guid.NewGuid().ToString("N"),
                    OrgCode = item.OrgCode,
                    TaskCode = item.TaskCode,
                    QueueCode = item.QueueCode,
                    TaskItemCode = item.TaskItemCode,
                    ItemType = item.ItemType,
                    ItemName = null,
                    LogAction = action,
                    LogLevel = level,
                    Message = ExpertNcCheckExecutor.Truncate(message, 2000),
                    DurationMs = durationMs,
                    IsAutoResult = true,
                    OperatorCode = "system",
                    OperatorName = "系统",
                    OperateTime = now,
                    CreateTime = now,
                    IsValid = 1,
                    IsDeleted = false,
                    Sort = 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ExpertQueue] 写日志失败 action={Action}", action);
            }
        }
    }
}
