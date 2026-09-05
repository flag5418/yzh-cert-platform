using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cert.Platform.Services.Admin.Platform.WorkflowEngine
{
    /// <summary>
    /// 工作流执行日志工具 — 统一的日志格式化输出
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §八（详细日志输出规范）</para>
    /// <para>格式：[{TIMESTAMP}] [{LEVEL}] [{TAG}] {KEY}={VALUE}, {KEY}={VALUE}, ...</para>
    /// <para>使用方式：通过构造函数注入，各引擎模块调用对应方法</para>
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

        public void CacheWarmupStart(string taskCode, int nodeCount)
            => _logger.LogInformation(
                "[CACHE_WARMUP_START] taskCode={TaskCode}, nodeCount={NodeCount}",
                taskCode, nodeCount);

        public void CacheWarmupDone(string taskCode, int cachedKeys, int durationMs)
            => _logger.LogInformation(
                "[CACHE_WARMUP_DONE] taskCode={TaskCode}, cachedKeys={Count}, durationMs={DurationMs}",
                taskCode, cachedKeys, durationMs);

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

        public void PathStart(string taskCode, string itemCode, int pathIndex, int nodeCount)
            => _logger.LogInformation(
                "[PATH_START] taskCode={TaskCode}, itemCode={ItemCode}, pathIndex={PathIndex}, nodeCount={NodeCount}",
                taskCode, itemCode, pathIndex, nodeCount);

        public void PathDone(string taskCode, string itemCode, int pathIndex, string status)
            => _logger.LogInformation(
                "[PATH_DONE] taskCode={TaskCode}, itemCode={ItemCode}, pathIndex={PathIndex}, status={Status}",
                taskCode, itemCode, pathIndex, status);

        public void PathFail(string taskCode, string itemCode, int pathIndex, string failedAt, string error)
            => _logger.LogWarning(
                "[PATH_FAIL] taskCode={TaskCode}, itemCode={ItemCode}, pathIndex={PathIndex}, failedAt={FailedAt}, error={Error}",
                taskCode, itemCode, pathIndex, failedAt, error);

        public void ItemDone(string taskCode, string itemCode, bool isSuccess, int durationMs)
            => _logger.LogInformation(
                "[ITEM_DONE] taskCode={TaskCode}, itemCode={ItemCode}, isSuccess={IsSuccess}, durationMs={DurationMs}",
                taskCode, itemCode, isSuccess, durationMs);

        // ── 节点执行日志 ──

        public void NodeStart(string taskCode, string itemCode, string nodeId, string nodeType, string title)
            => _logger.LogInformation(
                "[NODE_START] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, nodeType={NodeType}, title={Title}",
                taskCode, itemCode, nodeId, nodeType, title);

        public void NodeInputs(string taskCode, string itemCode, string nodeId, object inputs)
            => _logger.LogInformation(
                "[NODE_INPUTS] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, inputs={Inputs}",
                taskCode, itemCode, nodeId, JsonSerializer.Serialize(inputs));

        public void NodeOutput(string taskCode, string itemCode, string nodeId, object output, int durationMs)
            => _logger.LogInformation(
                "[NODE_OUTPUT] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, output={Output}, durationMs={DurationMs}",
                taskCode, itemCode, nodeId, JsonSerializer.Serialize(output), durationMs);

        public void NodeDone(string taskCode, string itemCode, string nodeId, int durationMs)
            => _logger.LogInformation(
                "[NODE_DONE] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, status=completed, durationMs={DurationMs}",
                taskCode, itemCode, nodeId, durationMs);

        public void NodeFail(string taskCode, string itemCode, string nodeId, string error, int durationMs)
            => _logger.LogError(
                "[NODE_FAIL] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, error={Error}, durationMs={DurationMs}",
                taskCode, itemCode, nodeId, error, durationMs);

        public void NodeReuse(string taskCode, string itemCode, string nodeId, string reusedFrom)
            => _logger.LogInformation(
                "[NODE_REUSE] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, reusedFrom={ReusedFrom}",
                taskCode, itemCode, nodeId, reusedFrom);

        // ── 节点类型特定日志 ──

        public void StartInject(string enterpriseCode, string phaseCode, string standardCode)
            => _logger.LogInformation(
                "[START_INJECT] enterpriseCode={Enterprise}, phaseCode={Phase}, standardCode={Standard}",
                enterpriseCode, phaseCode, standardCode);

        public void DocFieldQuery(string ruleCode, string fieldCode, string docType)
            => _logger.LogInformation(
                "[DOCFIELD_QUERY] ruleCode={RuleCode}, fieldCode={FieldCode}, docType={DocType}",
                ruleCode, fieldCode, docType);

        public void DocFieldResult(string value, double confidence, string source)
            => _logger.LogInformation(
                "[DOCFIELD_RESULT] value={Value}, confidence={Confidence}, source={Source}",
                value, confidence, source);

        public void DocTableQuery(string ruleCode, string tableCode, string docType)
            => _logger.LogInformation(
                "[DOCTABLE_QUERY] ruleCode={RuleCode}, tableCode={TableCode}, docType={DocType}",
                ruleCode, tableCode, docType);

        public void DocTableResult(int rowCount, double confidence, string source)
            => _logger.LogInformation(
                "[DOCTABLE_RESULT] rowCount={RowCount}, confidence={Confidence}, source={Source}",
                rowCount, confidence, source);

        public void SkillExec(string skillCode, string method)
            => _logger.LogInformation(
                "[SKILL_EXEC] skillCode={SkillCode}, method={Method}",
                skillCode, method);

        public void SkillResult(bool success, object result)
            => _logger.LogInformation(
                "[SKILL_RESULT] success={Success}, result={Result}",
                success, JsonSerializer.Serialize(result));

        public void BranchDecision(bool condition, string decision)
            => _logger.LogInformation(
                "[BRANCH_DECISION] condition={Condition}, decision={Decision}",
                condition, decision);

        public void EndCollect(string title, object upstreamOutput)
            => _logger.LogInformation(
                "[END_COLLECT] title={Title}, upstreamOutput={Output}",
                title, JsonSerializer.Serialize(upstreamOutput));

        // ── 缓存操作日志 ──

        public void CacheSet(string key, int valueLength)
            => _logger.LogDebug("[CACHE_SET] key={Key}, valueLength={Length}", key, valueLength);

        public void CacheHit(string key)
            => _logger.LogDebug("[CACHE_HIT] key={Key}", key);

        public void CacheMiss(string key)
            => _logger.LogDebug("[CACHE_MISS] key={Key}", key);

        public void CacheDel(string key)
            => _logger.LogDebug("[CACHE_DEL] key={Key}", key);
    }
}
