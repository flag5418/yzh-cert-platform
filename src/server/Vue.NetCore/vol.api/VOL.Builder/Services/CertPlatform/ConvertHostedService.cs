/*
 * Office 文档转换后台服务
 * 使用 ConvertQueueManager 实现并发控制 + 超时 + 通知
 */
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VOL.Builder.Services.CertPlatform
{
    public class ConvertHostedService : BackgroundService
    {
        private readonly ILogger<ConvertHostedService> _logger;
        private readonly ConvertQueueManager _queueManager;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _pollingIntervalSeconds;

        public ConvertHostedService(
            ILogger<ConvertHostedService> logger,
            ConvertQueueManager queueManager,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _queueManager = queueManager;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _pollingIntervalSeconds = configuration.GetValue<int>("OfficeConvert:PollingIntervalSeconds", 3);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ConvertHostedService] 后台服务已启动");

            // 初始加载配置
            _queueManager.ReloadConfig();

            // 定期重新加载配置（每 60 秒）
            var configReloadTimer = new Timer(_ => _queueManager.ReloadConfig(), null,
                TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ConvertHostedService] 处理任务时发生错误");
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
            }

            configReloadTimer?.Dispose();
            _logger.LogInformation("[ConvertHostedService] 后台服务已停止");
        }

        private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
        {
            try
            {
                var workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 50);

                var job = await _queueManager.GetNextPendingJobAsync(workerId);

                if (job == null)
                    return;

                _logger.LogInformation($"[ConvertHostedService] 开始处理: {job.FileCode}");

                // 并行执行（不等待，让多个 Worker 同时处理）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _queueManager.ExecuteJobAsync(job, stoppingToken);
                        _logger.LogInformation($"[ConvertHostedService] 完成: {job.FileCode}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[ConvertHostedService] 失败: {job.FileCode}");
                    }
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConvertHostedService] ProcessPendingJobsAsync 异常");
            }
        }
    }
}
