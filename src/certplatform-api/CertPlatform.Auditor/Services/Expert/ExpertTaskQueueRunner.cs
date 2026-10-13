using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

            await ExpertQueueFinalizer.SafeLogAsync(db, item, ExpertTaskConst.LogAction.QueueProgress,
                ExpertTaskConst.LogLevel.Info,
                $"开始执行：{item.ItemType} / {item.ItemCode}", null, now, _logger);
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
                    it.NextRetryAt = now.AddSeconds(ExpertQueueFinalizer.BackoffSeconds(it.RetryCount));
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

                await ExpertQueueFinalizer.SafeLogAsync(db, it, ExpertTaskConst.LogAction.ItemExtractRetry,
                    ExpertTaskConst.LogLevel.Warn, "租约过期回收：" + it.ErrorMessage, null, now, _logger);
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
                await ExpertQueueFinalizer.FinalizeAsync(db, item, outcome, durationMs, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpertQueue] 收口失败 item={Code}", item.Code);
            }
        }
    }
}
