
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 文件转换任务执行器
/// 实现 IYzhTaskExecutor，TaskType = "file_convert"
/// 注意：QueueManager 是单例，因此本类也必须是单例
/// 内部通过 IServiceProvider.CreateScope() 获取 Scoped 的 OfficeConvertService
/// </summary>
public class OfficeConvertTaskExecutor : IYzhTaskExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OfficeConvertTaskExecutor> _logger;

    public OfficeConvertTaskExecutor(
        IServiceProvider serviceProvider,
        ILogger<OfficeConvertTaskExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string TaskType => "file_convert";

    public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(task.Payload))
            return new TaskExecutionResult { Success = false, Message = "Payload 为空", Retryable = false };

        try
        {
            var payload = JsonSerializer.Deserialize<FileConvertPayload>(task.Payload);
            if (payload == null)
                return new TaskExecutionResult { Success = false, Message = "Payload 解析失败", Retryable = false };

            using var scope = _serviceProvider.CreateScope();
            var convertService = scope.ServiceProvider.GetRequiredService<OfficeConvertService>();
            var ok = await convertService.ConvertAsync(payload);
            return new TaskExecutionResult
            {
                Success = ok,
                Message = ok ? "转换成功" : "转换失败",
                Retryable = !ok
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行文件转换任务失败: {TaskCode}", task.Code);
            return new TaskExecutionResult { Success = false, Message = ex.Message, Retryable = true };
        }
    }

    public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
    {
        _logger.LogInformation("文件转换任务状态变更: {TaskCode} → {Status} ({Message})",
            task.Code, newStatus, message);
        return Task.CompletedTask;
    }
}
