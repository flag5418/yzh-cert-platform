using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow.Models;
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
    /// <para>4. 写入节点执行记录（wf_node_execution）</para>
    /// <para>5. 聚合 NC 结果</para>
    /// <para>6. 清理缓存</para>
    /// <para>两条路径：TEST（同步执行）/ NC_CHECK（正式执行，决策 D-6 本期不开放入口）</para>
    /// </summary>
    public class WfExecutionTaskService
    {
        private readonly WorkflowConfigParser _parser;
        private readonly WorkflowInterpreter _interpreter;
        private readonly TaskCacheService _cacheService;
        private readonly WorkflowLogger _wfLogger;
        private readonly IDbOrm _db;
        private readonly ILogger<WfExecutionTaskService> _logger;

        public WfExecutionTaskService(
            WorkflowConfigParser parser,
            WorkflowInterpreter interpreter,
            TaskCacheService cacheService,
            WorkflowLogger wfLogger,
            IDbOrm db,
            ILogger<WfExecutionTaskService> logger)
        {
            _parser = parser;
            _interpreter = interpreter;
            _cacheService = cacheService;
            _wfLogger = wfLogger;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 创建并执行工作流任务（同步模式，用于 TEST）
        /// </summary>
        public async Task<TaskExecutionResponse> CreateAndRunAsync(TaskExecutionRequest request, CancellationToken ct = default)
        {
            var taskCode = Guid.NewGuid().ToString("N");
            var itemCode = Guid.NewGuid().ToString("N");

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

            // 2. 创建数据库记录（原生 SQL 参数化写入，列名对照 all_tables_ddl.sql）
            var now = DateTime.Now;
            await _db.SqlExecuteAsync(
                "INSERT INTO wf_execution_task (code, TaskType, TaskStatus, ConfigSnapshot, RuleCode, EnterpriseCode, PhaseCode, StartedAt, CreateTime, IsDeleted) " +
                "VALUES (@code, @taskType, 'executing', @configSnapshot, @ruleCode, @enterpriseCode, @phaseCode, @startedAt, @now, 0)",
                new
                {
                    code = taskCode,
                    taskType = request.TaskType,
                    configSnapshot = request.ConfigJson,
                    ruleCode = request.RuleCode ?? "",
                    enterpriseCode = request.EnterpriseCode ?? "",
                    phaseCode = request.PhaseCode ?? "",
                    startedAt = now,
                    now
                });

            await _db.SqlExecuteAsync(
                "INSERT INTO wf_execution_task_item (code, TaskCode, RuleCode, ItemType, ItemStatus, StartedAt, CreateTime, IsDeleted) " +
                "VALUES (@code, @taskCode, @ruleCode, @itemType, 'executing', @startedAt, @now, 0)",
                new
                {
                    code = itemCode,
                    taskCode,
                    ruleCode = request.RuleCode ?? "",
                    itemType = request.TaskType,
                    startedAt = now,
                    now
                });

            // 3. 预热缓存
            var (cachedCount, cacheKeys) = await _cacheService.WarmUpAsync(
                taskCode, parsed, request.EnterpriseCode ?? "", ct);
            var cacheKeysJson = JsonSerializer.Serialize(cacheKeys);
            await _db.SqlExecuteAsync(
                "UPDATE wf_execution_task SET CacheKeys = @cacheKeys WHERE code = @code",
                new { cacheKeys = cacheKeysJson, code = taskCode });
            await _db.SqlExecuteAsync(
                "UPDATE wf_execution_task_item SET CacheKeys = @cacheKeys WHERE code = @code",
                new { cacheKeys = cacheKeysJson, code = itemCode });

            // 4. 构造上下文参数
            var contextParams = new Dictionary<string, object>
            {
                ["enterpriseCode"] = request.EnterpriseCode ?? "",
                ["phaseCode"] = request.PhaseCode ?? "",
                ["standardCode"] = request.StandardCode ?? ""
            };

            _wfLogger.TaskStart(taskCode, 1);

            // 5. 执行
            var sw = Stopwatch.StartNew();
            var itemResult = await _interpreter.ExecuteItemAsync(
                parsed, taskCode, itemCode, contextParams, ct);
            sw.Stop();

            // 6. 写入节点执行记录
            await SaveNodeExecutionsAsync(taskCode, itemCode, parsed, itemResult, ct);

            // 7. 更新 Item 和 Task 结果
            var itemStatus = itemResult.Success ? "completed" : "failed";
            var resultSummary = JsonSerializer.Serialize(itemResult.NcResult);

            await _db.SqlExecuteAsync(
                "UPDATE wf_execution_task_item SET ItemStatus = @status, IsSuccess = @isSuccess, ResultSummary = @resultSummary, " +
                "ErrorMessage = @errorMessage, CompletedAt = @completedAt, DurationMs = @durationMs WHERE code = @code",
                new
                {
                    status = itemStatus,
                    isSuccess = sbyte.Parse(itemResult.IsSuccess ? "1" : "0"),
                    resultSummary,
                    errorMessage = itemResult.Error,
                    completedAt = DateTime.Now,
                    durationMs = (int)sw.ElapsedMilliseconds,
                    code = itemCode
                });

            await _db.SqlExecuteAsync(
                "UPDATE wf_execution_task SET TaskStatus = @status, ResultSummary = @resultSummary, " +
                "ErrorMessage = @errorMessage, CompletedAt = @completedAt, DurationMs = @durationMs WHERE code = @code",
                new
                {
                    status = itemStatus,
                    resultSummary,
                    errorMessage = itemResult.Error,
                    completedAt = DateTime.Now,
                    durationMs = (int)sw.ElapsedMilliseconds,
                    code = taskCode
                });

            _wfLogger.TaskDone(taskCode, itemStatus, (int)sw.ElapsedMilliseconds);

            // 8. 清理缓存
            var cleaned = await _cacheService.CleanUpAsync(taskCode, cacheKeys, ct);
            _wfLogger.CacheCleanup(taskCode, cleaned);

            // 9. 构造返回（camelCase JSON 契约由 TaskExecutionResponse 的 JsonPropertyName 保障）
            return new TaskExecutionResponse
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Status = itemStatus,
                IsSuccess = itemResult.IsSuccess,
                NcResult = itemResult.NcResult,
                PathResults = itemResult.PathResults.Select(p => new TaskPathResult
                {
                    pathIndex = p.PathIndex,
                    status = p.Status,
                    failedAtNodeId = p.FailedAtNodeId,
                    error = p.Error,
                    output = p.Output,
                    nodeIds = p.NodeIds
                }).ToList(),
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        /// <summary>
        /// 保存节点执行记录到 wf_node_execution 表
        /// <para>用 HashSet 跟踪已保存的节点 ID，避免多路径共享节点时违反唯一键约束（旧逻辑保留）</para>
        /// </summary>
        private async Task SaveNodeExecutionsAsync(
            string taskCode,
            string itemCode,
            ParsedWorkflow parsed,
            ItemExecutionResult itemResult,
            CancellationToken ct)
        {
            var savedNodeIds = new HashSet<string>();

            foreach (var pathResult in itemResult.PathResults)
            {
                foreach (var nodeId in pathResult.NodeIds)
                {
                    if (!parsed.NodeMap.TryGetValue(nodeId, out var node))
                        continue;

                    // 同一节点可能在多条路径中共享（如 start/compare/branch），只保存一次
                    if (!savedNodeIds.Add(nodeId))
                        continue;

                    var nodeExecStatus = pathResult.Status == "completed" ? "completed" : "failed";
                    var now = DateTime.Now;

                    // 如果是路径最后一个节点，记录输出
                    string? outputJson = null;
                    if (nodeId == pathResult.NodeIds.LastOrDefault() && pathResult.Output != null && pathResult.Output.Count > 0)
                    {
                        outputJson = JsonSerializer.Serialize(pathResult.Output);
                    }

                    // 如果是失败节点，记录错误
                    var errorMessage = pathResult.FailedAtNodeId == nodeId ? pathResult.Error : null;

                    await _db.SqlExecuteAsync(
                        "INSERT INTO wf_node_execution (code, TaskCode, ItemCode, NodeId, NodeType, NodeTitle, SkillCode, " +
                        "ExecStatus, OutputJson, ErrorMessage, StartedAt, CompletedAt, ExecutionTimeMs, IsReused, CreateTime, IsDeleted) " +
                        "VALUES (@code, @taskCode, @itemCode, @nodeId, @nodeType, @nodeTitle, @skillCode, " +
                        "@execStatus, @outputJson, @errorMessage, @startedAt, @completedAt, @executionTimeMs, 0, @now, 0)",
                        new
                        {
                            code = Guid.NewGuid().ToString("N"),
                            taskCode,
                            itemCode,
                            nodeId,
                            nodeType = node.NodeType,
                            nodeTitle = node.Title,
                            skillCode = node.SkillCode,
                            execStatus = nodeExecStatus,
                            outputJson,
                            errorMessage,
                            startedAt = now,
                            completedAt = now,
                            executionTimeMs = 0,
                            now
                        });
                }
            }
        }
    }
}
