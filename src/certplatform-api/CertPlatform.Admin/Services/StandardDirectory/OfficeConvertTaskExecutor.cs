
using System.Text.Json;
using CertPlatform.Shared.Constants;
using CertPlatform.Admin.Entities.Dir;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;

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
            // 大小写不敏感：队列 payload 由 camelCase 序列化（fileCode），旧数据是 PascalCase（FileCode），统一兼容
            var payload = JsonSerializer.Deserialize<FileConvertPayload>(task.Payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null)
                return new TaskExecutionResult { Success = false, Message = "Payload 解析失败", Retryable = false };

            using var scope = _serviceProvider.CreateScope();
            var convertService = scope.ServiceProvider.GetRequiredService<OfficeConvertService>();
            var ok = await convertService.ConvertAsync(payload);
            if (ok)
                await EnqueueEnterpriseExtractAsync(payload);
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

    // G-2d 提取链接线：企业域文件转换成功后自动追加 doc_extract 任务（模板域/裸转换不入队）
    // ⚠️ QueueManager 构造注入 IEnumerable<IYzhTaskExecutor>（含本类）⇒ 构造器注入会形成 DI 循环，
    //    必须运行时从根容器懒解析。
    private async Task EnqueueEnterpriseExtractAsync(FileConvertPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.EnterpriseCode) || payload.EnterpriseCode == YzhVirtualEnterprise.Code)
            return;

        // ★ S2 图 3 分流：未要求自动提取 ⇒ 只转换不提取（状态留 none，原因落 ExtractMessage）
        if (!payload.AutoExtract)
        {
            await MarkAutoExtractSkippedAsync(payload.Code);
            _logger.LogInformation("[FileConvert→Extract] AutoExtract=false，跳过提取入队: {FileCode}", payload.Code);
            return;
        }

        var queueManager = _serviceProvider.GetRequiredService<QueueManager>();
        var fileCode = payload.Code;
        var req = new QueueManager.CreateQueueRequest
        {
            QueueType = "doc_extract",
            QueueName = $"企业资料提取 - {payload.FileName}",
            ScopeKey = payload.EnterpriseCode,
            SourceType = "enterprise_extract",
            // yzh_queue.uk_source 唯一：同一文件多轮转换→提取必须逐次唯一
            SourceId = $"{fileCode}@{DateTime.Now:yyyyMMddHHmmss}",
            ResourceLocks = new List<QueueManager.ResourceLockItem>
            {
                new() { ResourceTable = QueueManager.RESOURCE_FILE, ResourceCode = fileCode, ResourceName = payload.FileName }
            },
            Tasks = new List<QueueManager.TaskItem>
            {
                new() { TaskType = "doc_extract", Payload = JsonSerializer.Serialize(new { code = fileCode, enterpriseCode = payload.EnterpriseCode, stageCode = payload.StageCode, fileName = payload.FileName }) }
            }
        };
        var (qok, qerr, _, _) = await queueManager.CreateQueueAsync(req);
        if (!qok)
            _logger.LogWarning("[FileConvert→Extract] 提取队列创建失败: {FileCode} {Reason}", fileCode, qerr);
    }

    /// <summary>
    /// ★ S2：AutoExtract=false 时的落痕 —— 状态保持 3 态里的 <c>none</c>（未提取），
    /// 只把原因写进 <c>ExtractMessage</c>，让页面能看到「转换完成，未要求自动提取」。
    /// </summary>
    private async Task MarkAutoExtractSkippedAsync(string fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode)) return;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
            var file = (await db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
                x => x.Code == fileCode)).Data;
            if (file == null) return;

            file.ExtractStatus = "none";
            file.ExtractMessage = "转换完成，未要求自动提取";
            file.UpdateTime = DateTime.Now;
            await db.UpdateAsync(file,
                nameof(StandardDirectoryFile.ExtractStatus),
                nameof(StandardDirectoryFile.ExtractMessage),
                nameof(StandardDirectoryFile.UpdateTime));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileConvert→Extract] 写未自动提取标记失败: {FileCode}", fileCode);
        }
    }

    public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
    {
        _logger.LogInformation("文件转换任务状态变更: {TaskCode} → {Status} ({Message})",
            task.Code, newStatus, message);
        return Task.CompletedTask;
    }
}
