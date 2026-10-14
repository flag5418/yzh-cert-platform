
using System.Collections.Concurrent;
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

    /// <summary>
    ///     转换批次 → 待提取文件缓冲（★ 批量提取，2026-10-14）。
    ///
    ///     <para>同一转换队列的多个 file_convert 任务并发跑完时，**不再一文件一提取队列**
    ///     （那会产生 N 条「企业资料提取 - xx.doc 已完成」终态通知 = 弹窗轰炸）；
    ///     而是先缓冲到父队列维度，待父转换队列**全部任务终态**后，合并成**一条**
    ///     多任务提取队列 —— 终态只通知一次：「企业资料提取 - N 个文件」。</para>
    /// </summary>
    private static readonly ConcurrentDictionary<string, List<FileConvertPayload>> PendingExtractBatches = new();

    /// <summary>取消/重跑父转换队列时清掉缓冲，避免僵尸条目泄漏（公开给 CancelQueue 路径调用）</summary>
    public static void ClearPendingExtractBatch(string? parentQueueCode)
    {
        if (!string.IsNullOrWhiteSpace(parentQueueCode))
            PendingExtractBatches.TryRemove(parentQueueCode, out _);
    }

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
                await TryEnqueueBatchExtractAsync(payload, task);
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

    /// <summary>
    ///     ★ <b>批量提取入队</b>（2026-10-14，替代「一文件一提取队列」）：
    ///     先把成功转换且要求提取的文件缓冲到父转换队列维度；
    ///     待父队列除当前任务外**再无 pending/processing** 时，合并成一条多任务提取队列。
    ///
    ///     <para>并发安全：先 Accumulate 再判兄弟任务 —— 两个 worker 同时收尾时，
    ///     后完成者会看到前者的任务已置终态（ExecuteTaskAsync 在 ExecuteAsync 返回后才 UPDATE），
    ///     由后完成者统一 Flush，不会丢缓冲。</para>
    /// </summary>
    private async Task TryEnqueueBatchExtractAsync(FileConvertPayload payload, YzhQueueTask task)
    {
        if (!OrgScope.IsEnterpriseRow(payload.EnterpriseCode))
            return;

        // ★ S2 图 3 分流：未要求自动提取 ⇒ 只转换不提取（状态留 none，原因落 ExtractMessage）
        if (!payload.AutoExtract)
        {
            await MarkAutoExtractSkippedAsync(payload.Code);
            _logger.LogInformation("[FileConvert→Extract] AutoExtract=false，跳过提取入队: {FileCode}", payload.Code);
            return;
        }

        var parentQueueCode = task.QueueCode;
        if (string.IsNullOrWhiteSpace(parentQueueCode))
        {
            // 无父队列（异常路径）：退化为单文件提取，保证链路不断
            await CreateExtractQueueAsync(new List<FileConvertPayload> { payload }, null);
            return;
        }

        var batch = PendingExtractBatches.GetOrAdd(parentQueueCode, _ => new List<FileConvertPayload>());
        lock (batch)
        {
            batch.Add(payload);
        }

        // 兄弟任务是否还有未终态的（排除当前任务 —— 其状态在 ExecuteAsync 返回后才 UPDATE）
        var remaining = await CountNonTerminalSiblingsAsync(parentQueueCode, task.Code);
        if (remaining > 0)
        {
            _logger.LogInformation(
                "[FileConvert→Extract] 父队列 {QueueCode} 仍有 {Remaining} 个转换任务未终态，提取入队暂缓（已缓冲 {File}）",
                parentQueueCode, remaining, payload.Code);
            return;
        }

        // 当前是最后一个收尾的转换任务：Flush 全部缓冲
        List<FileConvertPayload> toEnqueue;
        lock (batch)
        {
            toEnqueue = new List<FileConvertPayload>(batch);
            batch.Clear();
        }
        PendingExtractBatches.TryRemove(parentQueueCode, out _);

        if (toEnqueue.Count == 0) return;
        await CreateExtractQueueAsync(toEnqueue, parentQueueCode);
    }

    /// <summary>父转换队列中，除 <paramref name="excludeTaskCode"/> 外仍处于 pending/processing 的任务数</summary>
    private async Task<int> CountNonTerminalSiblingsAsync(string parentQueueCode, string excludeTaskCode)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
            var tasks = await db.GetListAsync<YzhQueueTask>(t => t.QueueCode == parentQueueCode);
            return (tasks.Data ?? new List<YzhQueueTask>())
                .Count(t => t.Code != excludeTaskCode && (t.Status == "pending" || t.Status == "processing"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FileConvert→Extract] 统计兄弟任务失败，按已全部完成处理: {QueueCode}", parentQueueCode);
            return 0;
        }
    }

    /// <summary>把一批已转换文件合并成**一条**多任务提取队列（终态只通知一次）</summary>
    private async Task CreateExtractQueueAsync(List<FileConvertPayload> files, string? parentQueueCode)
    {
        if (files.Count == 0) return;

        var queueManager = _serviceProvider.GetRequiredService<QueueManager>();
        var creatorCode = await ResolveParentCreatorAsync(parentQueueCode);
        var first = files[0];
        var queueName = files.Count == 1
            ? $"企业资料提取 - {first.FileName}"
            : $"企业资料提取 - {files.Count} 个文件";

        var req = new QueueManager.CreateQueueRequest
        {
            QueueType = "doc_extract",
            QueueName = queueName,
            // ScopeKey 取首个文件的企业码：资源锁仍逐文件持有；互斥以文件锁为准
            ScopeKey = first.EnterpriseCode,
            SourceType = "enterprise_extract",
            // yzh_queue.uk_source 唯一：SourceId 带毫秒时间戳 —— 父队列重跑再入提取也不撞唯一键
            SourceId = string.IsNullOrWhiteSpace(parentQueueCode)
                ? $"extract-batch@{DateTime.Now:yyyyMMddHHmmssfff}"
                : $"{parentQueueCode}@{DateTime.Now:yyyyMMddHHmmssfff}",
            // ★ CreateBy 存用户 Code（契约）：继承触发转换的那个人，让派生队列也能通知到人
            UserCode = creatorCode,
            ResourceLocks = files.Select(f => new QueueManager.ResourceLockItem
            {
                ResourceTable = QueueManager.RESOURCE_FILE,
                ResourceCode = f.Code,
                ResourceName = f.FileName
            }).ToList(),
            Tasks = files.Select(f => new QueueManager.TaskItem
            {
                TaskType = "doc_extract",
                Payload = JsonSerializer.Serialize(new
                {
                    code = f.Code,
                    enterpriseCode = f.EnterpriseCode,
                    stageCode = f.StageCode,
                    fileName = f.FileName
                })
            }).ToList()
        };
        var (qok, qerr, qcode, qcount) = await queueManager.CreateQueueAsync(req);
        if (!qok)
            _logger.LogWarning("[FileConvert→Extract] 批量提取队列创建失败（{Count} 个文件）: {Reason}", files.Count, qerr);
        else
            _logger.LogInformation(
                "[FileConvert→Extract] 批量提取队列已创建: {QueueCode}（{Count} 个文件，父队列 {Parent}）",
                qcode, qcount, parentQueueCode ?? "-");
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

    public async Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
    {
        _logger.LogInformation("文件转换任务状态变更: {TaskCode} → {Status} ({Message})",
            task.Code, newStatus, message);

        // ★ 失败/取消收尾：成功路径已在 TryEnqueueBatchExtractAsync Flush；
        //   若最后一个终态是失败/取消，兄弟里已成功的文件仍缓冲着 —— 此处补一次 Flush
        //   （CountNonTerminalSiblingsAsync 见全终态即触发合并入队）
        if (newStatus is "failed" or "cancelled" && !string.IsNullOrWhiteSpace(task.QueueCode))
        {
            try
            {
                var remaining = await CountNonTerminalSiblingsAsync(task.QueueCode, task.Code);
                if (remaining > 0) return;

                if (PendingExtractBatches.TryRemove(task.QueueCode, out var batch) && batch.Count > 0)
                {
                    List<FileConvertPayload> toEnqueue;
                    lock (batch)
                    {
                        toEnqueue = new List<FileConvertPayload>(batch);
                        batch.Clear();
                    }
                    if (toEnqueue.Count > 0)
                        await CreateExtractQueueAsync(toEnqueue, task.QueueCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FileConvert→Extract] 失败收尾 Flush 提取缓冲失败: {QueueCode}", task.QueueCode);
            }
        }
    }
}
