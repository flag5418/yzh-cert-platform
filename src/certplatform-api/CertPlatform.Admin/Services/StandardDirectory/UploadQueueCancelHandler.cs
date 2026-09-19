
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 上传队列取消处理器
/// 实现 IYzhQueueCancelHandler：取消文件转换队列时清理上传数据
/// 注意：QueueManager 是单例，因此本类也必须是单例
/// 内部通过 IServiceProvider.CreateScope() 获取 Scoped 的 IDbOrm
/// </summary>
public class UploadQueueCancelHandler : IYzhQueueCancelHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UploadQueueCancelHandler> _logger;

    public UploadQueueCancelHandler(
        IServiceProvider serviceProvider,
        ILogger<UploadQueueCancelHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task OnQueueCancelledAsync(YzhQueue queue)
    {
        if (queue.QueueType != "file_convert" || queue.SourceType != "upload_task")
            return;

        var taskId = queue.SourceId;
        if (string.IsNullOrEmpty(taskId)) return;

        _logger.LogInformation("处理上传队列取消: QueueCode={QueueCode}, TaskId={TaskId}",
            queue.QueueCode, taskId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();

            var files = (await db.GetListAsync<StandardDirectoryFile>(
                x => x.TaskId == taskId && x.IsValid == 0
                    && (x.ConvertStatus == "pending" || x.ConvertStatus == "converting"))).Data ?? new();

            foreach (var file in files)
            {
                file.IsValid = 1;
                file.ConvertStatus = "failed";
                file.ConvertMessage = "队列已取消";
                await db.UpdateAsync(file);
            }

            _logger.LogInformation("上传队列取消处理完成: {TaskId}, 恢复 {Count} 个文件",
                taskId, files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理上传队列取消失败: {QueueCode}", queue.QueueCode);
        }
    }
}
