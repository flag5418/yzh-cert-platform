using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            var task = new WfExecutionTask
            {
                Code = taskCode,
                TaskType = request.TaskType,
                TaskStatus = "executing",
                ConfigSnapshot = request.ConfigJson ?? "",
                RuleCode = request.RuleCode ?? "",
                EnterpriseCode = request.EnterpriseCode ?? "",
                PhaseCode = request.PhaseCode ?? "",
                StartedAt = now,
                CreateTime = now,
                IsDeleted = false
            };
            await _db.Client.Insertable(task).ExecuteCommandAsync();

            var taskItem = new WfExecutionTaskItem
            {
                Code = itemCode,
                TaskCode = taskCode,
                RuleCode = request.RuleCode ?? "",
                ItemType = request.TaskType,
                ItemStatus = "executing",
                StartedAt = now,
                CreateTime = now,
                IsDeleted = false
            };
            await _db.Client.Insertable(taskItem).ExecuteCommandAsync();

            // 3. 预热缓存
            var (cachedCount, cacheKeys) = await _cacheService.WarmUpAsync(
                taskCode, parsed, request.EnterpriseCode ?? "", ct);
            var cacheKeysJson = JsonSerializer.Serialize(cacheKeys);
            await _db.Client.Updateable<WfExecutionTask>()
                .SetColumns(x => x.CacheKeys == cacheKeysJson)
                .Where(x => x.Code == taskCode)
                .ExecuteCommandAsync();
            await _db.Client.Updateable<WfExecutionTaskItem>()
                .SetColumns(x => x.CacheKeys == cacheKeysJson)
                .Where(x => x.Code == itemCode)
                .ExecuteCommandAsync();

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

            await _db.Client.Updateable<WfExecutionTaskItem>()
                .SetColumns(x => new WfExecutionTaskItem
                {
                    ItemStatus = itemStatus,
                    IsSuccess = itemResult.IsSuccess ? 1 : 0,
                    ResultSummary = resultSummary,
                    ErrorMessage = itemResult.Error ?? "",
                    CompletedAt = DateTime.Now,
                    DurationMs = (int)sw.ElapsedMilliseconds
                })
                .Where(x => x.Code == itemCode)
                .ExecuteCommandAsync();

            await _db.Client.Updateable<WfExecutionTask>()
                .SetColumns(x => new WfExecutionTask
                {
                    TaskStatus = itemStatus,
                    ResultSummary = resultSummary,
                    ErrorMessage = itemResult.Error ?? "",
                    CompletedAt = DateTime.Now,
                    DurationMs = (int)sw.ElapsedMilliseconds
                })
                .Where(x => x.Code == taskCode)
                .ExecuteCommandAsync();

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

                    var nodeExec = new WfNodeExecution
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        TaskCode = taskCode,
                        ItemCode = itemCode,
                        NodeId = nodeId,
                        NodeType = node.NodeType,
                        NodeTitle = node.Title,
                        SkillCode = node.SkillCode ?? "",
                        ExecStatus = nodeExecStatus,
                        OutputJson = outputJson,
                        ErrorMessage = errorMessage,
                        StartedAt = now,
                        CompletedAt = now,
                        ExecutionTimeMs = 0,
                        IsReused = 0,
                        CreateTime = now,
                        IsDeleted = false
                    };
                    await _db.Client.Insertable(nodeExec).ExecuteCommandAsync();
                }
            }
        }
    }
}
