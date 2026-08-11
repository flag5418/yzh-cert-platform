/*
 * Office 文档转换后台服务
 * 使用 HostedService 实现后台任务处理
 */
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VOL.Core.EFDbContext;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// Office 文档转换后台服务
    /// </summary>
    public class ConvertHostedService : BackgroundService
    {
        private readonly ILogger<ConvertHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        
        // 轮询间隔（秒）
        private readonly int _pollingIntervalSeconds;
        
        public ConvertHostedService(
            ILogger<ConvertHostedService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _pollingIntervalSeconds = configuration.GetValue<int>("OfficeConvert:PollingIntervalSeconds", 5);
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ConvertHostedService] 后台服务已启动");
            
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
                
                // 等待下一轮
                await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
            }
            
            _logger.LogInformation("[ConvertHostedService] 后台服务已停止");
        }
        
        /// <summary>
        /// 处理待处理的任务
        /// </summary>
        private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var convertService = scope.ServiceProvider.GetRequiredService<OfficeConvertService>();
                
                // 获取下一个待处理的任务
                var job = await convertService.GetNextPendingJobAsync();
                
                if (job == null)
                {
                    return; // 没有待处理的任务
                }
                
                _logger.LogInformation($"[ConvertHostedService] 开始处理任务: {job.FileCode}, 类型: {job.ConvertType}");
                
                try
                {
                    await convertService.ExecuteConvertAsync(job, stoppingToken);
                    _logger.LogInformation($"[ConvertHostedService] 任务处理完成: {job.FileCode}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[ConvertHostedService] 任务处理失败: {job.FileCode}");
                    // 任务失败已在 ExecuteConvertAsync 中记录，这里只记录日志
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConvertHostedService] ProcessPendingJobsAsync 发生异常");
            }
        }
    }
}
