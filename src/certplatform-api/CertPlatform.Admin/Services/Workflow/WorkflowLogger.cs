using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 工作流执行日志工具 — 统一的日志格式化输出
    /// <para>移植自：旧 WorkflowLogger.cs（零改动，仅 namespace 调整）</para>
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §八（详细日志输出规范）</para>
    /// </summary>
    public class WorkflowLogger
    {
        private readonly ILogger<WorkflowLogger> _logger;

        public WorkflowLogger(ILogger<WorkflowLogger> logger)
        {
            _logger = logger;
        }

        // ── 任务生命周期日志 ──

        public void TaskCreate(string taskCode, string taskType, string ruleCode, string enterprise)
            => _logger.LogInformation(
                "[TASK_CREATE] taskCode={TaskCode}, taskType={TaskType}, ruleCode={RuleCode}, enterprise={Enterprise}",
                taskCode, taskType, ruleCode, enterprise);

        public void TaskStart(string taskCode, int itemCount)
            => _logger.LogInformation(
                "[TASK_START] taskCode={TaskCode}, itemCount={ItemCount}",
                taskCode, itemCount);

        public void TaskDone(string taskCode, string status, int durationMs)
            => _logger.LogInformation(
                "[TASK_DONE] taskCode={TaskCode}, status={Status}, durationMs={DurationMs}",
                taskCode, status, durationMs);

        public void CacheCleanup(string taskCode, int cleanedKeys)
            => _logger.LogInformation(
                "[CACHE_CLEANUP] taskCode={TaskCode}, cleanedKeys={Count}",
                taskCode, cleanedKeys);

        // ── Item 执行日志 ──

        public void ItemStart(string taskCode, string itemCode, string ruleCode)
            => _logger.LogInformation(
                "[ITEM_START] taskCode={TaskCode}, itemCode={ItemCode}, ruleCode={RuleCode}",
                taskCode, itemCode, ruleCode);

        public void PathEnum(string taskCode, string itemCode, int pathCount)
            => _logger.LogInformation(
                "[PATH_ENUM] taskCode={TaskCode}, itemCode={ItemCode}, pathCount={PathCount}",
                taskCode, itemCode, pathCount);

        public void ItemDone(string taskCode, string itemCode, bool isSuccess, int durationMs)
            => _logger.LogInformation(
                "[ITEM_DONE] taskCode={TaskCode}, itemCode={ItemCode}, isSuccess={IsSuccess}, durationMs={DurationMs}",
                taskCode, itemCode, isSuccess, durationMs);
    }
}
