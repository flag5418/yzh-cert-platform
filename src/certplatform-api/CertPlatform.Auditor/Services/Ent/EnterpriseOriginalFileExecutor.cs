using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 企业原始资料<b>文件级编排器</b>执行器（TaskType = <c>enterprise_original_file</c>，2026-10-06 队列重构）。
    ///
    /// <para><b>职责</b>：一个文件 = 一个队列 = 一个任务，任务内<b>串行</b>跑完两段——</para>
    /// <list type="number">
    ///   <item><b>转换段</b>：委托 <see cref="EnterpriseOriginalIngestExecutor"/>
    ///         （格式归一 + PDF 预览 + Markdown 提取，全部能力原样复用）。</item>
    ///   <item><b>分析段</b>：委托 <see cref="EnterpriseOriginalAnalyzeExecutor"/>
    ///         （逐标准 <c>doc_group</c> + <c>doc_content</c> + 画像 upsert，M6 多标准语义原样复用）。</item>
    /// </list>
    ///
    /// <para><b>★ 为什么是「编排器」而不是重写一遍</b>：两个子执行器的转换/分析能力（含列级写回、
    /// 状态枚举铁律、M6 逐标准画像、M8-2 分组切片）已经过实测验证，本类只负责<b>顺序与失败传播</b>，
    /// ⛔ 不复制任何一段业务逻辑 ⇒ 子执行器修 bug 时这里自动受益。</para>
    ///
    /// <para><b>★ 为什么必须串行（2026-10-06 结论）</b>：<c>QueueHostedService</c> 每秒领 1 个任务
    /// fire-and-forget，<b>队列内 / 队列间任务都没有顺序保证</b> —— 旧「ingest 转换完再补建 analyze 队列」
    /// 的批次方案在并发下 analyze 必然抢跑（实测读到空 <c>MarkdownPath</c>，3 份全部失败）。
    /// 串行到同一个任务里，转换产物对分析段天然可见，无须任何跨队列协调。</para>
    ///
    /// <para><b>★ 单例 + 构造注入两个子执行器</b>：子执行器是单例（<c>IYzhTaskExecutor</c> 由
    /// <c>QueueManager</c> 构造注入 <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c>）；本类只依赖它们的
    /// <b>具体类型</b>、不依赖执行器集合 ⇒ 不构成 DI 环（与 <c>OfficeConvertTaskExecutor</c> 要
    /// 懒解析的情形不同）。每个子调用内部各自 <c>CreateScope</c>，本类不持有 scoped 资源。</para>
    ///
    /// <para><b>⚠️ 失败传播</b>：转换段失败 ⇒ 整任务失败（分析段不跑，产物不可信）；
    /// 分析段失败 ⇒ 整任务失败（可重试 ⇒ 下次连转换一起重跑，幂等：状态列全部是<b>覆盖写</b>）。
    /// ⛔ 不吞任何一段的错误 —— 旧批次方案「分组失败只 LogWarning、页面显示成功」是 M8 的根因。</para>
    /// </summary>
    public class EnterpriseOriginalFileExecutor : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnterpriseOriginalFileExecutor> _logger;
        private readonly EnterpriseOriginalIngestExecutor _ingest;
        private readonly EnterpriseOriginalAnalyzeExecutor _analyze;

        public EnterpriseOriginalFileExecutor(
            IServiceProvider serviceProvider,
            ILogger<EnterpriseOriginalFileExecutor> logger,
            EnterpriseOriginalIngestExecutor ingest,
            EnterpriseOriginalAnalyzeExecutor analyze)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _ingest = ingest;
            _analyze = analyze;
        }

        public string TaskType => EnterpriseOriginalQueue.TaskTypeFile;

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(task.Payload))
                return new TaskExecutionResult { Success = false, Message = "Payload 为空", Retryable = false };

            EnterpriseOriginalFilePayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<EnterpriseOriginalFilePayload>(task.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return new TaskExecutionResult { Success = false, Message = $"Payload 解析失败：{ex.Message}", Retryable = false };
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Code))
                return new TaskExecutionResult { Success = false, Message = "Payload 缺少 Code", Retryable = false };

            // ★ 执行时读行（⛔ 不信入队时的快照）：策略/存储路径可能在排队期间被人工改过
            EnterpriseOriginalFile row;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
                var loaded = (await db.GetOneIgnoreValidAsync<EnterpriseOriginalFile>(x => x.Code == payload.Code)).Data;
                if (loaded == null)
                    return new TaskExecutionResult { Success = false, Message = "文件行不存在", Retryable = false };
                row = loaded;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[文件编排] 读取文件行失败: {Code}", payload.Code);
                return new TaskExecutionResult { Success = false, Message = $"读取文件行失败：{ex.Message}", Retryable = true };
            }

            // ──── ① 转换段（复用 ingest 执行器：能力/状态写回一行不改）────
            var ingestResult = await _ingest.ExecuteAsync(
                new YzhQueueTask
                {
                    Code = task.Code,
                    QueueCode = task.QueueCode,
                    TaskType = EnterpriseOriginalQueue.TaskTypeIngest,
                    Payload = JsonSerializer.Serialize(new EnterpriseOriginalIngestPayload
                    {
                        Code = row.Code,
                        EnterpriseCode = row.EnterpriseCode,
                        StageCode = row.StageCode,
                        FileName = row.FileName,
                        SourcePath = row.StoragePath,
                        FileType = row.FileType,
                        AnalyzePolicy = row.AnalyzePolicy,
                        BatchCode = payload.BatchCode,
                    }),
                },
                cancellationToken);

            if (!ingestResult.Success)
            {
                _logger.LogWarning("[文件编排] 转换段失败，跳过分析段: {Code} {Reason}", row.Code, ingestResult.Message);
                return ingestResult;
            }

            // ★ 段间取消检查：同文件重传/取消会 CancelQueueAsync 打断本任务（转换单元已落库，
            //   分析段不必再跑）；超时 cts 同理 ⇒ QueueManager 按 isCancel 收尾、可重试。
            cancellationToken.ThrowIfCancellationRequested();

            if (payload.SkipAnalyze)
                return new TaskExecutionResult { Success = true, Message = $"{ingestResult.Message}；已按要求跳过语义分析" };

            // skip / ignore 策略：转换段已把行标为 skipped，分析段没有必要再跑一遍
            if (row.AnalyzePolicy is EnterpriseOriginalService.Policy.Skip
                or EnterpriseOriginalService.Policy.Ignore)
                return new TaskExecutionResult { Success = true, Message = $"{ingestResult.Message}；策略为 {row.AnalyzePolicy}，不做语义分析" };

            // ──── ② 分析段（复用 analyze 执行器：M6 逐标准 / M8-2 分组切片一行不改）────
            var analyzeResult = await _analyze.ExecuteAsync(
                new YzhQueueTask
                {
                    Code = task.Code,
                    QueueCode = task.QueueCode,
                    TaskType = EnterpriseOriginalQueue.TaskTypeAnalyze,
                    Payload = JsonSerializer.Serialize(new EnterpriseOriginalAnalyzePayload
                    {
                        EnterpriseCode = row.EnterpriseCode,
                        StageCode = row.StageCode,
                        // 单文件批次：BatchCode 仅作日志/业务引用（分组调用的 businessRef 用它）
                        BatchCode = string.IsNullOrWhiteSpace(payload.BatchCode)
                            ? $"file:{row.Code}" : payload.BatchCode,
                        FileCodes = new List<string> { row.Code },
                        StandardCodes = payload.StandardCodes ?? new List<string>(),
                    }),
                },
                cancellationToken);

            if (!analyzeResult.Success)
            {
                _logger.LogWarning("[文件编排] 分析段失败: {Code} {Reason}", row.Code, analyzeResult.Message);
                return new TaskExecutionResult
                {
                    Success = false,
                    Message = $"转换成功但语义分析失败：{analyzeResult.Message}",
                    Retryable = analyzeResult.Retryable,
                };
            }

            _logger.LogInformation("[文件编排] 完成: {Code} | {Convert} | {Analyze}", row.Code, ingestResult.Message, analyzeResult.Message);
            return new TaskExecutionResult
            {
                Success = true,
                Message = $"{ingestResult.Message}；{analyzeResult.Message}",
                Retryable = false,
            };
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            _logger.LogInformation("[文件编排] 任务状态变更: {TaskCode} → {Status} ({Message})",
                task.Code, newStatus, message);
            return Task.CompletedTask;
        }
    }
}
