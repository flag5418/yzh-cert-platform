using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace YZH.Core.DataBase.Services;

/// <summary>
/// yzh 队列后台 Worker：持续领取 pending 任务并交给 QueueManager 执行
/// </summary>
public class QueueHostedService : BackgroundService
{
    private readonly ILogger<QueueHostedService> _logger;
    private readonly QueueManager _queueManager;
    private readonly string _workerId;

    public QueueHostedService(ILogger<QueueHostedService> logger, QueueManager queueManager)
    {
        _logger = logger;
        _queueManager = queueManager;
        _workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..40];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[QueueHostedService] 后台服务已启动 (workerId={WorkerId})", _workerId);

        // 启动时立即回收遗留的 processing 任务
        try
        {
            var reaped = await _queueManager.ReapStaleTasksOnStartupAsync();
            _logger.LogInformation("[QueueHostedService] 启动回收遗留任务: {Count} 个", reaped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[QueueHostedService] 启动回收遗留任务失败");
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
                _logger.LogError(ex, "[QueueHostedService] 处理任务时发生错误");
                await Task.Delay(3000, stoppingToken);
            }
        }
        _logger.LogInformation("[QueueHostedService] 后台服务已停止");
    }

    private async Task ProcessPendingTasksAsync(CancellationToken stoppingToken)
    {
        var task = await _queueManager.GetNextPendingTaskAsync(_workerId);
        if (task == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _queueManager.ExecuteTaskAsync(task, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QueueHostedService] 任务执行失败: {TaskId}", task.Id);
            }
        }, stoppingToken);
    }
}
