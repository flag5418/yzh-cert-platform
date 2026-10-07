using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace CertPlatform.Auditor.Services.Ent.Normalize
{
    /// <summary>
    ///     企业资料规范化队列执行器（<c>TaskType = "enterprise_normalize"</c>）。
    ///
    ///     <para><b>职责边界（写死）</b>：本类<b>只做</b>「载荷反序列化 → 调编排器 → 包结果」。
    ///     ⛔ 不写任何填充逻辑 —— 全部在 <see cref="DocumentFillOrchestrator.FillOneAsync"/>。
    ///     这样「一个文件 = 一个任务」的语义只在一个地方定义（<c>55</c> §3.1）。</para>
    ///
    ///     <para><b>★ 单例约束</b>：<c>QueueManager</c> 是单例且构造注入
    ///     <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c> ⇒ <b>所有执行器必须是单例</b>。
    ///     而本类依赖 <see cref="DocumentFillOrchestrator"/>（Scoped）⇒ 由
    ///     <see cref="EnterpriseNormalizeExecutorAdapter"/> 单例壳包装（每次 <c>CreateScope</c>）。</para>
    ///
    ///     <para>★ <see cref="TaskTypeName"/> 抽 <c>const</c> 共用 —— <c>QueueManager</c> 按
    ///     <c>TaskType</c> 建字典分发，两处字面量不一致 = 任务<b>静默分发不到</b>（不报错）。</para>
    /// </summary>
    public class EnterpriseNormalizeExecutor : IYzhTaskExecutor
    {
        /// <summary>★ 任务类型常量 —— 由 <see cref="EnterpriseNormalizeExecutorAdapter"/> <b>复用</b>。</summary>
        public const string TaskTypeName = "enterprise_normalize";

        private readonly DocumentFillOrchestrator _orchestrator;
        private readonly ILogger<EnterpriseNormalizeExecutor> _logger;

        public EnterpriseNormalizeExecutor(
            DocumentFillOrchestrator orchestrator,
            ILogger<EnterpriseNormalizeExecutor> logger)
        {
            _orchestrator = orchestrator;
            _logger = logger;
        }

        public string TaskType => TaskTypeName;

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken ct)
        {
            NormalizeTaskPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<NormalizeTaskPayload>(task.Payload ?? "{}");
            }
            catch (JsonException ex)
            {
                return new TaskExecutionResult { Success = false, Message = $"任务载荷不是合法 JSON：{ex.Message}", Retryable = false };
            }

            if (payload == null
                || string.IsNullOrWhiteSpace(payload.EnterpriseCode)
                || string.IsNullOrWhiteSpace(payload.StandardFileCode))
            {
                return new TaskExecutionResult
                {
                    Success = false,
                    Message = "任务参数无效：缺少 EnterpriseCode 或 StandardFileCode",
                    Retryable = false,
                };
            }

            _logger.LogInformation("[EntNorm] 开始规范化任务：Enterprise={Ent}, Std={Std}, File={File}",
                payload.EnterpriseCode, payload.StandardCode, payload.StandardFileCode);

            var result = await _orchestrator.FillOneAsync(new FillOneRequest
            {
                OrgCode = payload.OrgCode,
                EnterpriseCode = payload.EnterpriseCode,
                StandardCode = payload.StandardCode,
                StageCode = payload.StageCode,
                StandardFileCode = payload.StandardFileCode,
                QueueCode = task.QueueCode,
                QueueTaskCode = task.Code,
                OperatorCode = payload.OperatorCode,
            }, ct);

            return new TaskExecutionResult
            {
                Success = result.Success,
                Message = result.Message ?? result.Status,
                // 只有真异常（failed）才可重试；「跳过」是设计内行为，重试无意义
                Retryable = string.Equals(result.Status, "failed", StringComparison.Ordinal),
            };
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
            => Task.CompletedTask;

        /// <summary>
        ///     队列载荷 —— <b>一个文件 = 一个任务</b>（<c>55</c> §3.1）。
        ///     <para>范围展开（企业 / 阶段 / 标准 / 文件夹 → 文件清单）由调用方（Controller）负责，
        ///     ⛔ 执行器不做范围判断。</para>
        /// </summary>
        public sealed class NormalizeTaskPayload
        {
            public string OrgCode { get; set; } = string.Empty;
            public string EnterpriseCode { get; set; } = string.Empty;
            public string StandardCode { get; set; } = string.Empty;
            public string StageCode { get; set; } = string.Empty;

            /// <summary>★ 目标标准文件 → <c>cert_standard_directory_file.Code</c>（标准域行）</summary>
            public string StandardFileCode { get; set; } = string.Empty;

            /// <summary>触发人 Code</summary>
            public string? OperatorCode { get; set; }
        }
    }
}
