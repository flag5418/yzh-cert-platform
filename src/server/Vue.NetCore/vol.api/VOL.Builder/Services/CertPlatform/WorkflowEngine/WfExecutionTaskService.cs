using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VOL.Builder.Services.CertPlatform.WorkflowEngine.Models;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.Builder.Services.CertPlatform.WorkflowEngine
{
    /// <summary>
    /// 工作流执行任务服务 — 编排层，串联所有引擎模块
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §四 §十一</para>
    /// <para>职责：</para>
    /// <para>1. 创建执行任务（wf_execution_task + wf_execution_task_item）</para>
    /// <para>2. 预热缓存</para>
    /// <para>3. 驱动 WorkflowInterpreter 执行</para>
    /// <para>4. 写入节点执行记录（wf_node_execution）</para>
    /// <para>5. 聚合 NC 结果</para>
    /// <para>6. 清理缓存</para>
    /// <para>两条路径：TEST（同步执行）/ NC_CHECK（异步入队，当前版本也同步）</para>
    /// </summary>
    public class WfExecutionTaskService
    {
        private readonly WorkflowConfigParser _parser;
        private readonly WorkflowInterpreter _interpreter;
        private readonly TaskCacheService _cacheService;
        private readonly WorkflowLogger _wfLogger;
        private readonly ILogger<WfExecutionTaskService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public WfExecutionTaskService(
            WorkflowConfigParser parser,
            WorkflowInterpreter interpreter,
            TaskCacheService cacheService,
            WorkflowLogger wfLogger,
            IServiceProvider serviceProvider,
            ILogger<WfExecutionTaskService> logger)
        {
            _parser = parser;
            _interpreter = interpreter;
            _cacheService = cacheService;
            _wfLogger = wfLogger;
            _serviceProvider = serviceProvider;
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
                parsed = _parser.Parse(request.ConfigJson);
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

            // 2. 创建数据库记录
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VOLContext>();

            var task = new WfExecutionTask
            {
                Code = taskCode,
                TaskType = request.TaskType,
                TaskStatus = "executing",
                ConfigSnapshot = request.ConfigJson,
                RuleCode = request.RuleCode ?? "",
                EnterpriseCode = request.EnterpriseCode ?? "",
                PhaseCode = request.PhaseCode ?? "",
                StartedAt = DateTime.Now
            };
            db.Set<WfExecutionTask>().Add(task);

            var item = new WfExecutionTaskItem
            {
                Code = itemCode,
                TaskCode = taskCode,
                RuleCode = request.RuleCode ?? "",
                ItemType = request.TaskType,
                ItemStatus = "executing",
                StartedAt = DateTime.Now
            };
            db.Set<WfExecutionTaskItem>().Add(item);
            await db.SaveChangesAsync(ct);

            // 3. 预热缓存
            var (cachedCount, cacheKeys) = await _cacheService.WarmUpAsync(
                taskCode, parsed, request.EnterpriseCode ?? "", ct);
            task.CacheKeys = JsonSerializer.Serialize(cacheKeys);
            item.CacheKeys = JsonSerializer.Serialize(cacheKeys);
            await db.SaveChangesAsync(ct);

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
            await SaveNodeExecutionsAsync(db, taskCode, itemCode, parsed, itemResult, ct);

            // 7. 更新 Item 和 Task 结果
            item.ItemStatus = itemResult.Success ? "completed" : "failed";
            item.IsSuccess = itemResult.IsSuccess ? 1 : 0;
            item.ResultSummary = JsonSerializer.Serialize(itemResult.NcResult);
            item.ErrorMessage = itemResult.Error;
            item.CompletedAt = DateTime.Now;
            item.DurationMs = (int)sw.ElapsedMilliseconds;

            task.TaskStatus = itemResult.Success ? "completed" : "failed";
            task.ResultSummary = JsonSerializer.Serialize(itemResult.NcResult);
            task.ErrorMessage = itemResult.Error;
            task.CompletedAt = DateTime.Now;
            task.DurationMs = (int)sw.ElapsedMilliseconds;

            await db.SaveChangesAsync(ct);

            _wfLogger.TaskDone(taskCode, task.TaskStatus, (int)sw.ElapsedMilliseconds);

            // 8. 清理缓存
            var cleaned = await _cacheService.CleanUpAsync(taskCode, ct);
            _wfLogger.CacheCleanup(taskCode, cleaned);

            // 9. 构造返回
            return new TaskExecutionResponse
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Status = task.TaskStatus,
                IsSuccess = itemResult.IsSuccess,
                NcResult = itemResult.NcResult,
                PathResults = itemResult.PathResults,
                DurationMs = (int)sw.ElapsedMilliseconds
            };
        }

        /// <summary>
        /// 保存节点执行记录到 wf_node_execution 表
        /// </summary>
        private async Task SaveNodeExecutionsAsync(
            VOLContext db,
            string taskCode,
            string itemCode,
            ParsedWorkflow parsed,
            ItemExecutionResult itemResult,
            CancellationToken ct)
        {
            // TODO: 当前版本节点执行记录在 NodeExecutor 内通过日志输出
            // 后续迭代：将 NodeExecutor 中的执行记录回传，写入 wf_node_execution 表
            // 当前阶段：仅写 end 节点的结果摘要

            // 用 HashSet 跟踪已保存的节点 ID，避免多路径共享节点时违反唯一键约束
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

                    var nodeExec = new WfNodeExecution
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        TaskCode = taskCode,
                        ItemCode = itemCode,
                        NodeId = nodeId,
                        NodeType = node.NodeType,
                        NodeTitle = node.Title,
                        SkillCode = node.SkillCode,
                        ExecStatus = pathResult.Status == "completed" ? "completed" : "failed",
                        StartedAt = DateTime.Now,
                        CompletedAt = DateTime.Now
                    };

                    // 如果是失败节点，记录错误
                    if (pathResult.FailedAtNodeId == nodeId)
                    {
                        nodeExec.ErrorMessage = pathResult.Error;
                    }

                    // 如果是路径最后一个节点，记录输出
                    if (nodeId == pathResult.NodeIds.Last() && pathResult.Output != null)
                    {
                        nodeExec.OutputJson = JsonSerializer.Serialize(pathResult.Output);
                    }

                    db.Set<WfNodeExecution>().Add(nodeExec);
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }

    // ── 请求/响应模型 ──

    /// <summary>
    /// 任务执行请求
    /// </summary>
    public class TaskExecutionRequest
    {
        /// <summary>任务类型：TEST | NC_CHECK | REPORT_GENERATE</summary>
        public string TaskType { get; set; } = "TEST";

        /// <summary>规则编码</summary>
        public string RuleCode { get; set; }

        /// <summary>企业编码</summary>
        public string EnterpriseCode { get; set; }

        /// <summary>标准编码</summary>
        public string StandardCode { get; set; }

        /// <summary>阶段编码</summary>
        public string PhaseCode { get; set; }

        /// <summary>工作流配置 JSON（rule_json）</summary>
        public string ConfigJson { get; set; }
    }

    /// <summary>
    /// 任务执行响应
    /// </summary>
    public class TaskExecutionResponse
    {
        /// <summary>任务编码</summary>
        public string TaskCode { get; set; }

        /// <summary>执行项编码</summary>
        public string ItemCode { get; set; }

        /// <summary>执行状态</summary>
        public string Status { get; set; }

        /// <summary>业务成功标志</summary>
        public bool IsSuccess { get; set; }

        /// <summary>NC 结果</summary>
        public Dictionary<string, object> NcResult { get; set; } = new();

        /// <summary>各路径执行结果</summary>
        public List<PathResult> PathResults { get; set; } = new();

        /// <summary>总执行耗时(ms)</summary>
        public int DurationMs { get; set; }
    }
}
