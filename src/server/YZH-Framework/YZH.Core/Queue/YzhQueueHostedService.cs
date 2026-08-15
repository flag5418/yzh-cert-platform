using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Queue
{
    /// <summary>
    /// yzh 队列后台 Worker：持续领取 pending 任务并交给 YzhQueueManager 执行
    /// </summary>
    public class YzhQueueHostedService : BackgroundService
    {
        private readonly ILogger<YzhQueueHostedService> _logger;
        private readonly YzhQueueManager _queueManager;
        private readonly string _workerId;

        public YzhQueueHostedService(
            ILogger<YzhQueueHostedService> logger,
            YzhQueueManager queueManager)
        {
            _logger = logger;
            _queueManager = queueManager;
            _workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..40];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"[YzhQueueHostedService] 后台服务已启动 (workerId={_workerId})");

            // 启动时立即回收遗留的 processing 任务（上次进程退出/重启时未完成的任务）
            // 避免：上一轮 Worker 被杀后任务孤儿化，需等整段租约（LeaseMinutes）过期才被回收，
            //      导致队列进度卡住长时间不结束。启动即回收 = 重启后秒级恢复。
            try
            {
                var reaped = await _queueManager.ReapStaleTasksOnStartupAsync();
                _logger.LogInformation($"[YzhQueueHostedService] 启动回收遗留任务: {reaped} 个");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[YzhQueueHostedService] 启动回收遗留任务失败");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingTasksAsync(stoppingToken);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[YzhQueueHostedService] 处理任务时发生错误");
                    await Task.Delay(3000, stoppingToken);
                }
            }
            _logger.LogInformation("[YzhQueueHostedService] 后台服务已停止");
        }

        /// <summary>
        /// 领取并执行任务（最多并发 maxConcurrent 个）
        /// </summary>
        private async Task ProcessPendingTasksAsync(CancellationToken stoppingToken)
        {
            var task = await _queueManager.GetNextPendingTaskAsync(_workerId);
            if (task == null)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _queueManager.ExecuteTaskAsync(task, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[YzhQueueHostedService] 任务执行失败: {task.Id}");
                }
            }, stoppingToken);
        }
    }
}
