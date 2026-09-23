using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 工作流执行日志工具 — 统一的日志格式化输出
    /// <para>移植自：旧 WorkflowLogger.cs（零改动，仅 namespace 调整）</para>
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §八（详细日志输出规范）</para>
    ///
    /// <para>⚠️ 本类是全引擎日志格式的<b>唯一出口</b>。统一格式：</para>
    /// <para><c>[{TIMESTAMP}] [{LEVEL}] [{TAG}] k=v, k=v, ...</c></para>
    /// <para>TAG 与字段名由上述文档 §8.2 / §8.3.2 定义，属<b>可机器解析的契约</b>，
    /// 不得随意增删字段或改名；新增字段只能追加在行尾（解析器按 <c>, </c> 切分、按首个 <c>=</c> 分键值）。</para>
    ///
    /// <para>2026-09-22 补齐（G11）：原实现只有 task/item 级 8 个方法，
    /// 路径级（PATH_START/PATH_DONE/PATH_FAIL）与节点级（NODE_*）全缺，
    /// 迫使 WorkflowInterpreter / NodeExecutor 各自用裸 ILogger 拼字符串，
    /// 造成格式漂移（如 NODE_REUSE 缺 reusedFrom、DOCFIELD_QUERY 缺 docType）。</para>
    /// </summary>
    public class WorkflowLogger
    {
        private readonly ILogger<WorkflowLogger> _logger;

        public WorkflowLogger(ILogger<WorkflowLogger> logger)
        {
            _logger = logger;
        }

        // ══════════════ 任务生命周期日志（§8.2.1） ══════════════

        public void TaskCreate(string taskCode, string taskType, string ruleCode, string enterprise)
            => _logger.LogInformation(
                "[TASK_CREATE] taskCode={TaskCode}, taskType={TaskType}, ruleCode={RuleCode}, enterprise={Enterprise}",
                taskCode, taskType, ruleCode, enterprise);

        public void CacheWarmupStart(string taskCode, int nodeCount)
            => _logger.LogInformation(
                "[CACHE_WARMUP_START] taskCode={TaskCode}, nodeCount={NodeCount}",
                taskCode, nodeCount);

        public void CacheWarmupDone(string taskCode, int cachedKeys, long durationMs)
            => _logger.LogInformation(
                "[CACHE_WARMUP_DONE] taskCode={TaskCode}, cachedKeys={CachedKeys}, durationMs={DurationMs}",
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

        // ══════════════ Item 执行日志（§8.2.2） ══════════════

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

        // ══════════════ 路径执行日志（§8.2.2） ══════════════

        public void PathStart(string taskCode, string itemCode, int pathIndex, int nodeCount)
            => _logger.LogInformation(
                "[PATH_START] taskCode={TaskCode}, itemCode={ItemCode}, pathIndex={PathIndex}, nodeCount={NodeCount}",
                taskCode, itemCode, pathIndex, nodeCount);

        public void PathDone(string taskCode, string itemCode, int pathIndex, string status)
            => _logger.LogInformation(
                "[PATH_DONE] taskCode={TaskCode}, itemCode={ItemCode}, pathIndex={PathIndex}, status={Status}",
                taskCode, itemCode, pathIndex, status);

        public void PathFail(string taskCode, string itemCode, int pathIndex, string? failedAtNodeId, string? error)
            => _logger.LogWarning(
                "[PATH_FAIL] taskCode={TaskCode}, itemCode={ItemCode}, pathIndex={PathIndex}, failedAt={FailedAt}, error={Error}",
                taskCode, itemCode, pathIndex, failedAtNodeId, error);

        // ══════════════ 节点执行日志（核心，§8.2.3） ══════════════

        public void NodeStart(string taskCode, string itemCode, string nodeId, string nodeType, string? title)
            => _logger.LogInformation(
                "[NODE_START] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, nodeType={NodeType}, title={Title}",
                taskCode, itemCode, nodeId, nodeType, title);

        public void NodeInputs(string taskCode, string itemCode, string nodeId, string inputsJson)
            => _logger.LogInformation(
                "[NODE_INPUTS] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, inputs={Inputs}",
                taskCode, itemCode, nodeId, inputsJson);

        public void NodeExec(string taskCode, string itemCode, string nodeId, string action)
            => _logger.LogInformation(
                "[NODE_EXEC] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, action={Action}",
                taskCode, itemCode, nodeId, action);

        public void NodeOutput(string taskCode, string itemCode, string nodeId, string outputJson, int durationMs)
            => _logger.LogInformation(
                "[NODE_OUTPUT] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, output={Output}, durationMs={DurationMs}",
                taskCode, itemCode, nodeId, outputJson, durationMs);

        public void NodeDone(string taskCode, string itemCode, string nodeId, int durationMs)
            => _logger.LogInformation(
                "[NODE_DONE] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, status=completed, durationMs={DurationMs}",
                taskCode, itemCode, nodeId, durationMs);

        public void NodeFail(string taskCode, string itemCode, string nodeId, string? error, int durationMs)
            => _logger.LogError(
                "[NODE_FAIL] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, error={Error}, durationMs={DurationMs}",
                taskCode, itemCode, nodeId, error, durationMs);

        /// <param name="reusedFrom">结果来源节点 ID（跨路径复用时通常等于 nodeId 本身）</param>
        public void NodeReuse(string taskCode, string itemCode, string nodeId, string reusedFrom)
            => _logger.LogInformation(
                "[NODE_REUSE] taskCode={TaskCode}, itemCode={ItemCode}, nodeId={NodeId}, reusedFrom={ReusedFrom}",
                taskCode, itemCode, nodeId, reusedFrom);

        // ══════════════ 节点类型特定日志（§8.2.4） ══════════════
        //
        // 注：DocFieldQuery / DocTableQuery 在文档定义的三字段（ruleCode/fieldCode|tableCode/docType）之后
        //     追加 enterpriseCode —— 该值决定实际取数来源（标准企业样本 vs 企业真实文档），
        //     是排查"取到样本数据还是真实数据"的关键线索，追加在行尾不影响机器解析。

        public void StartInject(string? enterpriseCode, string? phaseCode, string? standardCode)
            => _logger.LogInformation(
                "[START_INJECT] enterpriseCode={EnterpriseCode}, phaseCode={PhaseCode}, standardCode={StandardCode}",
                enterpriseCode, phaseCode, standardCode);

        public void DocFieldQuery(string ruleCode, string fieldCode, string docType, string enterpriseCode)
            => _logger.LogInformation(
                "[DOCFIELD_QUERY] ruleCode={RuleCode}, fieldCode={FieldCode}, docType={DocType}, enterpriseCode={EnterpriseCode}",
                ruleCode, fieldCode, docType, enterpriseCode);

        public void DocFieldResult(object? value, double confidence, string source)
            => _logger.LogInformation(
                "[DOCFIELD_RESULT] value={Value}, confidence={Confidence}, source={Source}",
                value, confidence, source);

        public void DocTableQuery(string ruleCode, string tableCode, string docType, string enterpriseCode)
            => _logger.LogInformation(
                "[DOCTABLE_QUERY] ruleCode={RuleCode}, tableCode={TableCode}, docType={DocType}, enterpriseCode={EnterpriseCode}",
                ruleCode, tableCode, docType, enterpriseCode);

        public void DocTableResult(int rowCount, double confidence, string source)
            => _logger.LogInformation(
                "[DOCTABLE_RESULT] rowCount={RowCount}, confidence={Confidence}, source={Source}",
                rowCount, confidence, source);

        public void SkillExec(string skillCode, string method)
            => _logger.LogInformation(
                "[SKILL_EXEC] skillCode={SkillCode}, method={Method}",
                skillCode, method);

        public void SkillResult(bool success, object? outputs)
            => _logger.LogInformation(
                "[SKILL_RESULT] success={Success}, result={Result}",
                success, JsonSerializer.Serialize(outputs));

        public void BranchDecision(bool condition, string decision)
            => _logger.LogInformation(
                "[BRANCH_DECISION] condition={Condition}, decision={Decision}",
                condition, decision);

        public void EndCollect(string? title, object? upstreamOutput)
            => _logger.LogInformation(
                "[END_COLLECT] title={Title}, upstreamOutput={UpstreamOutput}",
                title, upstreamOutput != null ? JsonSerializer.Serialize(upstreamOutput) : "null");
    }
}
