
using System.Text.Json;
using CertPlatform.Shared.Domain.Scope;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

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
        // ★ 2026-10-14：取消转换队列时清掉「批量提取缓冲」—— 否则 PendingExtractBatches 静态字典
        //   会留下僵尸条目（取消路径不会走 OnTaskStateChangedAsync 的 Flush 分支）
        if (queue.QueueType == "file_convert")
            OfficeConvertTaskExecutor.ClearPendingExtractBatch(queue.QueueCode);

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

            // ★ 1.B.6d-3：取数收口 CoreResourceQuery —— 按 TaskId 取全量文件（含禁用行），
            //   再在内存按「IsValid==0 + 转换中间态」过滤。
            // ⚠️ 修复既有缺陷：原 `GetListAsync(pred 含 x.IsValid == 0)` 走默认过滤（追加 IsValid=1）
            //    ⇒ 谓词自相矛盾**恒返回 0 行**（队列取消的失败回填从未生效）。
            //    实测证据：`cert_standard_directory_file` 23 行、禁用 0 行 ⇒ 缺陷休眠、零现网影响。
            var core = scope.ServiceProvider.GetRequiredService<ICoreResourceQuery>();
            var files = (await core.GetDirectoryFilesByTaskIdAsync(taskId, includeDisabled: true))
                .Where(x => x.IsValid == 0
                    && (x.ConvertStatus == "pending" || x.ConvertStatus == "converting"))
                .ToList();

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
