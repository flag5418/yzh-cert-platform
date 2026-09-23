using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SqlSugar;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Shared.Entities.Wf;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 工作流执行任务服务 — 编排层，串联所有引擎模块
    /// <para>移植自：旧 WfExecutionTaskService.cs（EF VOLContext → IDbOrm 原生 SQL，阶段0 决策）</para>
    /// <para>职责：</para>
    /// <para>1. 创建执行任务（wf_execution_task + wf_execution_task_item）</para>
    /// <para>2. 预热缓存</para>
    /// <para>3. 驱动 WorkflowInterpreter 执行</para>
    /// <para>4. 写入路径执行记录（wf_path_execution）与节点执行记录（wf_node_execution）</para>
    /// <para>5. 聚合 NC 结果</para>
    /// <para>6. 清理缓存</para>
    ///
    /// <para><b>三种测试入口（2026-09-22 阶段二统一）</b>：</para>
    /// <list type="bullet">
    ///   <item><see cref="CreateAndRunAsync"/> —— TestScope=FULL，整流完整测试（test/run）</item>
    ///   <item><see cref="RunSingleNodeAsync"/> —— TestScope=NODE，任意单节点测试（test/node）</item>
    ///   <item><see cref="RunAiNodeAsync"/> —— TestScope=AI_NODE，AI 节点测试（test/ai-node）</item>
    /// </list>
    /// <para>三者共用 <see cref="BeginRunAsync"/> / <see cref="SaveNodeExecutionsAsync"/> /
    /// <see cref="FinishRunAsync"/>，因此落库结构与日志链完全同构 —— 前端可用同一套渲染，
    /// 「任意节点测试」也成为一次真实、可回溯的执行。</para>
    ///
    /// <para>本类为 partial：单节点/AI 节点测试的实现见 <c>WfExecutionTaskService.NodeScope.cs</c>。</para>
    /// </summary>
    public partial class WfExecutionTaskService
    {
        private readonly WorkflowConfigParser _parser;
        private readonly WorkflowInterpreter _interpreter;
        private readonly TaskCacheService _cacheService;
        private readonly WorkflowLogger _wfLogger;
        private readonly IDbOrm _db;
        private readonly ILogger<WfExecutionTaskService> _logger;

        // 单节点 / AI 节点测试用（见 WfExecutionTaskService.NodeScope.cs）
        private readonly NodeExecutor _nodeExecutor;
        private readonly AiNodeExecutor _aiNodeExecutor;

        /// <summary>
        /// 输出 JSON 落库上限（64KB）。超过则落「截断信封」而非完整内容。
        /// <para>动机：docTable 之类节点可能产出 MB 级输出，完整落库会撑大行宽并让
        /// 前端测试历史/明细渲染卡死；64KB 足以覆盖正常业务输出。</para>
        /// </summary>
        private const int OutputJsonMaxBytes = 64 * 1024;

        /// <summary>截断信封里保留的预览字符数</summary>
        private const int OutputJsonPreviewChars = 4096;

        public WfExecutionTaskService(
            WorkflowConfigParser parser,
            WorkflowInterpreter interpreter,
            TaskCacheService cacheService,
            WorkflowLogger wfLogger,
            NodeExecutor nodeExecutor,
            AiNodeExecutor aiNodeExecutor,
            IDbOrm db,
            ILogger<WfExecutionTaskService> logger)
        {
            _parser = parser;
            _interpreter = interpreter;
            _cacheService = cacheService;
            _wfLogger = wfLogger;
            _nodeExecutor = nodeExecutor;
            _aiNodeExecutor = aiNodeExecutor;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 创建并执行工作流任务（同步模式，用于 TEST / FULL 整流测试）
        /// </summary>
        public async Task<TaskExecutionResponse> CreateAndRunAsync(TaskExecutionRequest request, CancellationToken ct = default)
        {
            var taskCode = Guid.NewGuid().ToString("N");
            var itemCode = Guid.NewGuid().ToString("N");
            var testScope = NormalizeTestScope(request.TestScope);

            _wfLogger.TaskCreate(taskCode, request.TaskType, request.RuleCode ?? "", request.EnterpriseCode ?? "");

            // 1. 解析配置（捕获解析/拓扑校验异常，返回友好错误）
            ParsedWorkflow parsed;
            try
            {
                parsed = _parser.Parse(request.ConfigJson ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WorkflowEngine] 工作流配置解析失败: {Error}", ex.Message);
                return new TaskExecutionResponse
                {
                    TaskCode = taskCode,
                    ItemCode = itemCode,
                    Status = "failed",
                    IsSuccess = false,
                    NcResult = new Dictionary<string, object>
                    {
                        ["success"] = false,
                        ["error"] = $"工作流配置校验失败: {ex.Message}",
                        ["result"] = null!
                    },
                    DurationMs = 0
                };
            }

            // 2. 创建数据库记录 + 预热缓存
            var cacheKeys = await BeginRunAsync(
                taskCode, itemCode,
                request.TaskType, testScope,
                request.ConfigJson ?? "",
                request.RuleCode ?? "", request.EnterpriseCode ?? "", request.PhaseCode ?? "",
                parsed, ct);

            // 3. 构造上下文参数
            var contextParams = new Dictionary<string, object>
            {
                ["enterpriseCode"] = request.EnterpriseCode ?? "",
                ["phaseCode"] = request.PhaseCode ?? "",
                ["standardCode"] = request.StandardCode ?? ""
            };

            // 4. 执行
            var sw = Stopwatch.StartNew();
            var itemResult = await _interpreter.ExecuteItemAsync(
                parsed, taskCode, itemCode, request.RuleCode ?? "", contextParams, ct);
            sw.Stop();

            // 5. 写入路径执行记录（wf_path_execution）+ 节点执行记录（wf_node_execution）
            await SavePathExecutionsAsync(taskCode, itemCode, itemResult, ct);
            await SaveNodeExecutionsAsync(taskCode, itemCode, parsed, itemResult, ct);

            // 6. 收口（更新 Task/Item + 清缓存）
            await FinishRunAsync(taskCode, itemCode, itemResult, sw.ElapsedMilliseconds, cacheKeys, ct);

            // 7. 构造返回（字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名，PascalCase）
            return new TaskExecutionResponse
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Status = itemResult.Success ? "completed" : "failed",
                IsSuccess = itemResult.IsSuccess,
                NcResult = itemResult.NcResult,
                PathResults = itemResult.PathResults.Select(p => new TaskPathResult
                {
                    PathIndex = p.PathIndex,
                    Status = p.Status,
                    FailedAtNodeId = p.FailedAtNodeId,
                    Error = p.Error,
                    Output = p.Output,
                    NodeIds = p.NodeIds,
                    NodeResults = p.NodeResults,
                    DurationMs = p.DurationMs,
                    StartedAt = p.StartedAt,
                    CompletedAt = p.CompletedAt
                }).ToList(),
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        // ════════════════════════════════════════════════════════════
        // 三种入口共用段（2026-09-22 阶段二抽出）
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 归一化测试范围：null / 空 / 未知值 → FULL（兼容存量前端不传该字段）
        /// </summary>
        private static string NormalizeTestScope(string? testScope)
        {
            if (string.IsNullOrWhiteSpace(testScope)) return "FULL";
            var s = testScope.Trim().ToUpperInvariant();
            return s is "FULL" or "NODE" or "AI_NODE" ? s : "FULL";
        }

        /// <summary>
        /// 创建任务与执行项记录，并预热任务级缓存（三种测试入口共用）
        /// <para>调用方负责先生成 taskCode/itemCode（解析失败时要提前返回，届时还没有 DB 行），
        /// 并在本方法之前调用一次 <c>_wfLogger.TaskCreate</c>。</para>
        /// </summary>
        /// <returns>预热产生的缓存键列表，供 <see cref="FinishRunAsync"/> 精确清理</returns>
        private async Task<List<string>> BeginRunAsync(
            string taskCode,
            string itemCode,
            string taskType,
            string testScope,
            string configSnapshot,
            string ruleCode,
            string enterpriseCode,
            string phaseCode,
            ParsedWorkflow parsed,
            CancellationToken ct)
        {
            var now = DateTime.Now;

            var task = new WfExecutionTask
            {
                Code = taskCode,
                TaskType = taskType,
                TestScope = testScope,
                TaskStatus = "executing",
                ConfigSnapshot = configSnapshot,
                RuleCode = ruleCode,
                EnterpriseCode = enterpriseCode,
                PhaseCode = phaseCode,
                StartedAt = now,
                CreateTime = now,
                IsDeleted = false
            };
            await _db.Client.Insertable(task).ExecuteCommandAsync();

            var taskItem = new WfExecutionTaskItem
            {
                Code = itemCode,
                TaskCode = taskCode,
                RuleCode = ruleCode,
                // 保持既有语义：ItemType = TaskType（TEST）。测试范围的区分由 wf_execution_task.TestScope 承担，
                // 且 ItemType 目前无任何读取方，不额外派生新格式。
                ItemType = taskType,
                ItemStatus = "executing",
                StartedAt = now,
                CreateTime = now,
                IsDeleted = false
            };
            await _db.Client.Insertable(taskItem).ExecuteCommandAsync();

            // 预热缓存（WarmUpAsync 只用 parsed.NodeMap，故单节点测试传合成的 ParsedWorkflow 亦可）
            var (_, cacheKeys) = await _cacheService.WarmUpAsync(taskCode, parsed, enterpriseCode, ct);
            var cacheKeysJson = JsonSerializer.Serialize(cacheKeys);
            await _db.Client.Updateable<WfExecutionTask>()
                .SetColumns(x => x.CacheKeys == cacheKeysJson)
                .Where(x => x.Code == taskCode)
                .ExecuteCommandAsync();
            await _db.Client.Updateable<WfExecutionTaskItem>()
                .SetColumns(x => x.CacheKeys == cacheKeysJson)
                .Where(x => x.Code == itemCode)
                .ExecuteCommandAsync();

            _wfLogger.TaskStart(taskCode, 1);

            return cacheKeys;
        }

        /// <summary>
        /// 收口：更新 Item/Task 状态与结果摘要，打 TASK_DONE，清理缓存（三种测试入口共用）
        /// </summary>
        private async Task FinishRunAsync(
            string taskCode,
            string itemCode,
            ItemExecutionResult itemResult,
            long elapsedMs,
            List<string> cacheKeys,
            CancellationToken ct)
        {
            var status = itemResult.Success ? "completed" : "failed";
            var resultSummary = JsonSerializer.Serialize(itemResult.NcResult);
            var completedAt = DateTime.Now;
            var durationMs = (int)elapsedMs;

            await _db.Client.Updateable<WfExecutionTaskItem>()
                .SetColumns(x => new WfExecutionTaskItem
                {
                    ItemStatus = status,
                    IsSuccess = itemResult.IsSuccess ? 1 : 0,
                    ResultSummary = resultSummary,
                    ErrorMessage = itemResult.Error ?? "",
                    CompletedAt = completedAt,
                    DurationMs = durationMs
                })
                .Where(x => x.Code == itemCode)
                .ExecuteCommandAsync();

            await _db.Client.Updateable<WfExecutionTask>()
                .SetColumns(x => new WfExecutionTask
                {
                    TaskStatus = status,
                    ResultSummary = resultSummary,
                    ErrorMessage = itemResult.Error ?? "",
                    CompletedAt = completedAt,
                    DurationMs = durationMs
                })
                .Where(x => x.Code == taskCode)
                .ExecuteCommandAsync();

            _wfLogger.TaskDone(taskCode, status, durationMs);

            // 清理缓存（CACHE_CLEANUP 的唯一出口 —— TaskCacheService 不再自行打这条日志，避免重复）
            var cleaned = await _cacheService.CleanUpAsync(taskCode, cacheKeys, ct);
            _wfLogger.CacheCleanup(taskCode, cleaned);
        }

        /// <summary>
        /// 由执行顺序的节点列表合成一个 <see cref="ParsedWorkflow"/>。
        /// <para>单节点 / AI 节点测试没有完整 rule_json（前端只传目标节点，AI 节点测试传的 ruleJson 也可能拓扑不完整），
        /// 不能走 <c>WorkflowConfigParser.Parse</c> 的拓扑校验。这里只填 <c>NodeMap</c>，
        /// 因为落库（<see cref="SaveNodeExecutionsAsync"/>）与缓存预热（<c>TaskCacheService.WarmUpAsync</c>）
        /// 都只依赖 NodeMap 来解析 NodeType/Title/SkillCode。</para>
        /// </summary>
        private static ParsedWorkflow BuildSyntheticWorkflow(IEnumerable<WorkflowNodeConfig> nodes)
        {
            var map = new Dictionary<string, WorkflowNodeConfig>();
            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(node.NodeId))
                    map[node.NodeId] = node;
            }

            var list = map.Values.ToList();
            return new ParsedWorkflow
            {
                Config = new WorkflowConfig { Nodes = list },
                NodeMap = map,
                StartNode = list.FirstOrDefault() ?? new WorkflowNodeConfig(),
                Adjacency = map.Keys.ToDictionary(k => k, _ => new List<WorkflowEdgeConfig>()),
                InEdges = map.Keys.ToDictionary(k => k, _ => new List<WorkflowEdgeConfig>()),
                Paths = list.Count > 0 ? new List<List<WorkflowNodeConfig>> { list } : new List<List<WorkflowNodeConfig>>()
            };
        }

        /// <summary>
        /// 保存路径执行记录到 wf_path_execution 表（阶段三新增，四层模型第三层）
        ///
        /// <para><b>为什么必须独立成表</b>：<c>wf_node_execution</c> 的唯一键是
        /// <c>(TaskCode, ItemCode, NodeId)</c>，同一节点被多条路径共享时只能落一行。
        /// <see cref="SaveNodeExecutionsAsync"/> 的去重策略是「优先保留真执行那次」，
        /// 于是<b>跨路径复用的事实被抹平</b>——DB 里 <c>IsReused</c> 恒为 0。
        /// 本方法按路径维度逐条落库，路径内的节点序列、失败点、最终输出、耗时原样保留，
        /// 「哪条路径走了哪些节点、复用了几次」在 DB 层第一次可查。</para>
        ///
        /// <para>与 <see cref="SaveNodeExecutionsAsync"/> 的分工：本表是路径的<b>全量</b>事实（一路径一行），
        /// 那张表是节点的<b>去重</b>事实（一节点一行）。两者按 (TaskCode, ItemCode) 可关联。</para>
        /// </summary>
        private async Task SavePathExecutionsAsync(
            string taskCode,
            string itemCode,
            ItemExecutionResult itemResult,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var rows = 0;
            var failedRows = 0;

            foreach (var path in itemResult.PathResults)
            {
                var row = new WfPathExecution
                {
                    Code = Guid.NewGuid().ToString("N"),
                    TaskCode = taskCode,
                    ItemCode = itemCode,
                    PathIndex = path.PathIndex,
                    Status = string.IsNullOrEmpty(path.Status) ? "pending" : path.Status,
                    NodeIds = path.NodeIds is { Count: > 0 }
                        ? JsonSerializer.Serialize(path.NodeIds)
                        : null,
                    ReusedCount = path.NodeResults.Count(r => r.IsReused),
                    FailedAtNodeId = string.IsNullOrEmpty(path.FailedAtNodeId) ? null : path.FailedAtNodeId,
                    ErrorMessage = Truncate(path.Error, 2000),
                    OutputJson = SerializeOutput(path.Output),
                    DurationMs = path.DurationMs,
                    // PathResult.StartedAt/CompletedAt 是 DateTime（非可空），default 表示引擎未写时序，落 NULL
                    StartedAt = path.StartedAt == default ? null : path.StartedAt,
                    CompletedAt = path.CompletedAt == default ? null : path.CompletedAt,
                    CreateTime = DateTime.Now,
                    IsDeleted = false
                };

                await _db.Client.Insertable(row).ExecuteCommandAsync();
                rows++;
                if (row.Status == "failed") failedRows++;
            }

            _logger.LogInformation(
                "[WorkflowEngine] 路径执行记录落库: taskCode={TaskCode}, itemCode={ItemCode}, pathCount={PathCount}, failedCount={FailedCount}",
                taskCode, itemCode, rows, failedRows);
        }

        /// <summary>
        /// 序列化输出字典为 JSON 字符串，超过 <see cref="OutputJsonMaxBytes"/> 时降级为「截断信封」。
        ///
        /// <para>输出列是 MySQL <c>json</c> 类型，直接按字符截断会产生非法 JSON 导致插入失败，
        /// 因此超长时改为落一个<b>合法 JSON</b>：保留原始字节数与一段预览，
        /// 让排查者一眼看出「这里被截断了、完整内容有多大」。</para>
        /// <para>未超长时输出与截断逻辑引入前<b>逐字节一致</b>，存量数据行为不变。</para>
        /// </summary>
        private static string? SerializeOutput(Dictionary<string, object>? output)
        {
            if (output is null || output.Count == 0) return null;

            var json = JsonSerializer.Serialize(output);
            if (Encoding.UTF8.GetByteCount(json) <= OutputJsonMaxBytes) return json;

            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["_truncated"] = true,
                ["_originalBytes"] = Encoding.UTF8.GetByteCount(json),
                ["preview"] = Truncate(json, OutputJsonPreviewChars)
            });
        }

        /// <summary>按字符数截断（用于 varchar 列与预览串）</summary>
        private static string? Truncate(string? value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars) return value;
            return value.Substring(0, maxChars);
        }

        /// <summary>
        /// 保存节点执行记录到 wf_node_execution 表
        ///
        /// <para><b>2026-09-22 重写（G1–G5）</b>：原实现遍历 <c>PathResult.NodeIds</c>，
        /// 拿路径级状态反推节点状态，造成 5 个失真：</para>
        /// <list type="bullet">
        ///   <item>中间节点无 OutputJson（只写路径最后一个节点）</item>
        ///   <item>失败路径中失败点<b>之前</b>已成功的节点也被标 failed</item>
        ///   <item>ExecutionTimeMs 硬编码 0</item>
        ///   <item>IsReused 硬编码 0</item>
        ///   <item>StartedAt == CompletedAt（同一个 now），无法还原执行顺序</item>
        /// </list>
        /// <para>现改为以 <see cref="NodeExecutionRecord"/> 为唯一事实来源，逐节点如实落库。</para>
        ///
        /// <para><b>去重语义</b>：同一节点在多条路径共享（start/compare/branch 等）时只能有一行
        /// （唯一键 TaskCode+ItemCode+NodeId）。跨路径复用时，同一节点会同时存在
        /// 「真执行记录（IsReused=false）」与「复用记录（IsReused=true）」，此处<b>优先保留真执行那次</b>，
        /// 因为真实耗时/时序/输出只在那一次产生。</para>
        /// </summary>
        private async Task SaveNodeExecutionsAsync(
            string taskCode,
            string itemCode,
            ParsedWorkflow parsed,
            ItemExecutionResult itemResult,
            CancellationToken ct)
        {
            // 按节点归并：每个 NodeId 取一条记录 —— 优先真执行，其次复用；
            // 最后按 StartedAt 升序写入，让表内行序即为可还原的执行顺序
            var records = itemResult.PathResults
                .SelectMany(p => p.NodeResults)
                .Where(r => !string.IsNullOrEmpty(r.NodeId))
                .GroupBy(r => r.NodeId)
                .Select(g => g.FirstOrDefault(r => !r.IsReused) ?? g.First())
                .OrderBy(r => r.StartedAt)
                .ToList();

            foreach (var record in records)
            {
                if (!parsed.NodeMap.TryGetValue(record.NodeId, out var node))
                    continue;

                var outputJson = SerializeOutput(record.Output);

                var nodeExec = new WfNodeExecution
                {
                    Code = Guid.NewGuid().ToString("N"),
                    TaskCode = taskCode,
                    ItemCode = itemCode,
                    NodeId = record.NodeId,
                    NodeType = node.NodeType,
                    NodeTitle = node.Title,
                    SkillCode = node.SkillCode ?? "",
                    // 逐节点真实状态：失败路径中失败点之前的节点同样是 completed
                    ExecStatus = record.Success ? "completed" : "failed",
                    OutputJson = outputJson,
                    ErrorMessage = Truncate(record.Error, 1000),
                    StartedAt = record.StartedAt,
                    CompletedAt = record.CompletedAt,
                    ExecutionTimeMs = record.DurationMs,
                    PromptTokens = record.PromptTokens,
                    CompletionTokens = record.CompletionTokens,
                    LlmDurationMs = record.LlmDurationMs,
                    IsReused = record.IsReused ? 1 : 0,
                    CreateTime = DateTime.Now,
                    IsDeleted = false
                };
                await _db.Client.Insertable(nodeExec).ExecuteCommandAsync();
            }

            _logger.LogInformation(
                "[WorkflowEngine] 节点执行记录落库: taskCode={TaskCode}, itemCode={ItemCode}, nodeCount={NodeCount}, reusedCount={ReusedCount}",
                taskCode, itemCode, records.Count, records.Count(r => r.IsReused));
        }
    }
}
