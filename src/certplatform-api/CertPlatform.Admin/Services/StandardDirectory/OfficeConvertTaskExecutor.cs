
using System.Text.Json;
using CertPlatform.Shared.Domain.Scope;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;
using YzhQueue = YZH.Core.Stand.Models.Queue.YzhQueue;

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
                await EnqueueEnterpriseExtractAsync(payload, task.QueueCode);
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
    /// <summary>
    ///     ★ <b>派生队列继承父队列的创建人</b>（2026-10-11）。
    ///
    ///     <para>「转换完成 → 自动提取」是**后台派生的第二条队列**：执行器没有 HttpContext，
    ///     不显式继承就只能落 NULL = 系统发起 ⇒ 这条队列的终态通知（站内信 + SignalR）
    ///     <b>没有收件人、静默跳过</b>（实测存量 110+45 条即为此）。</para>
    ///
    ///     <para>⛔ 取不到就是取不到（返回空）—— ⛔ 不要编造用户；但必须落日志，不静默。</para>
    /// </summary>
    private async Task<string> ResolveParentCreatorAsync(string? parentQueueCode)
    {
        if (string.IsNullOrWhiteSpace(parentQueueCode)) return string.Empty;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
            var parent = (await db.GetOneAsync<YzhQueue>(x => x.QueueCode == parentQueueCode)).Data;
            // ★ 契约：CreateBy = 用户 Code（可直接作为推送分组键）
            return parent?.CreateBy ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileConvert→Extract] 取父队列创建人失败，派生队列将无收件人: {QueueCode}", parentQueueCode);
            return string.Empty;
        }
    }

    private async Task EnqueueEnterpriseExtractAsync(FileConvertPayload payload, string? parentQueueCode)
    {
        // ★ 2026-10-13 归属模型 V2：只对「真实企业行」入队（EnterpriseCode 非空）。
        //   ⛔ 原「机构常量被误当企业码」防御已取消 —— yzh-std-env 常量本身已废除。
        if (!OrgScope.IsEnterpriseRow(payload.EnterpriseCode))
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
        var creatorCode = await ResolveParentCreatorAsync(parentQueueCode);
        var req = new QueueManager.CreateQueueRequest
        {
            QueueType = "doc_extract",
            QueueName = $"企业资料提取 - {payload.FileName}",
            ScopeKey = payload.EnterpriseCode,
            SourceType = "enterprise_extract",
            // yzh_queue.uk_source 唯一：同一文件多轮转换→提取必须逐次唯一
            SourceId = $"{fileCode}@{DateTime.Now:yyyyMMddHHmmss}",
            // ★ CreateBy 存用户 Code（契约）：继承触发转换的那个人，让派生队列也能通知到人
            UserCode = creatorCode,
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
