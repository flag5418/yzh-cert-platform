using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Expert
{
    /// <summary>
    /// 专家队列收口器（★ 队列复用 T4 抽出 —— 单一逻辑，两处消费）：
    /// <list type="number">
    ///   <item>旧 <c>ExpertTaskQueueRunner</c>（过渡期兜底，T8 退役后只剩 2）；</item>
    ///   <item><b>新 yzh_queue 路径的 T4 回写桥</b> —— 新引擎执行完一项后，
    ///         同步旧表 item 状态 + 三层进度重算（队列 → 标准子任务 → 任务头），
    ///         保证详情页 UI 读模型不断流（旧三表是 UI 读模型，T8 才删）。</item>
    /// </list>
    ///
    /// <para><b>为什么静态</b>：无构造依赖（只收 <c>IDbOrm</c> + 可选 <c>ILogger</c>），
    /// 避免为它注册 DI；调用方均已在自己的 scope 里有 <c>IDbOrm</c>。</para>
    ///
    /// <para>⚠️ 收口语义与重试计数（T4 过渡期的两轨同步）：
    /// 每次执行尝试 ⇒ 调一次 <see cref="FinalizeAsync"/> ⇒ 旧表 <c>RetryCount</c> +1；
    /// 同一次尝试也让 <c>yzh_queue_task.RetryCount</c> +1（QueueManager.HandleTaskFailureAsync）。
    /// 两边同起跑、同步增、同上限（默认 3）⇒ 终态判定一致。</para>
    /// </summary>
    public static class ExpertQueueFinalizer
    {
        /// <summary>
        /// T4 回写桥入口：按（TaskCode + 实体行 TaskItemCode）定位旧队列项并收口。
        /// <para>找不到旧队列项（老数据漂移 / 提交尚未落库的竞态）⇒ 静默返回 —— 新引擎侧
        /// 已成功执行，⛔ 不能因回写失败把框架任务打成 failed。</para>
        /// </summary>
        public static async Task WriteBackFromNewPathAsync(
            IDbOrm db, ILogger? logger, string? taskCode, string? taskItemCode,
            ItemExecOutcome outcome, int durationMs = 0)
        {
            if (string.IsNullOrWhiteSpace(taskCode) || string.IsNullOrWhiteSpace(taskItemCode)) return;
            var item = (await db.GetOneAsync<CertExpertTaskQueueItem>(x =>
                x.TaskCode == taskCode && x.TaskItemCode == taskItemCode && !x.IsDeleted)).Data;
            if (item == null) return;
            await FinalizeAsync(db, item, outcome, durationMs, logger);
        }

        /// <summary>收口：写项状态 → 重算队列 → 重算任务 → 写日志（自 runner 原样抽出，逻辑零改动）</summary>
        public static async Task FinalizeAsync(
            IDbOrm db, CertExpertTaskQueueItem item, ItemExecOutcome outcome,
            int durationMs, ILogger? logger = null)
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
                outcome.Message, durationMs, now, logger);

            await RefreshQueueAsync(db, fresh.QueueCode ?? "", now, logger);
            await RefreshTaskAsync(db, fresh.TaskCode ?? "", now, logger);
        }

        /// <summary>重算队列计数与状态</summary>
        public static async Task RefreshQueueAsync(IDbOrm db, string queueCode, DateTime now, ILogger? logger = null)
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
        public static async Task RefreshTaskAsync(IDbOrm db, string taskCode, DateTime now, ILogger? logger = null)
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
                    null, now, logger);
            }
        }

        public static int BackoffSeconds(int retryCount)
        {
            var secs = 5 * (int)Math.Pow(3, Math.Max(0, retryCount - 1));
            if (secs > 300) secs = 300;
            return secs + Random.Shared.Next(0, 5);
        }

        /// <summary>写任务日志（⛔ 日志失败绝不影响主流程）</summary>
        public static async Task SafeLogAsync(
            IDbOrm db, CertExpertTaskQueueItem item, string action, string level,
            string? message, int? durationMs, DateTime now, ILogger? logger = null)
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
                logger?.LogWarning(ex, "[ExpertQueue] 写日志失败 action={Action}", action);
            }
        }
    }
}
