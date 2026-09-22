using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 单节点 / AI 节点测试结果（服务层内部返回，供控制器构造 API 响应）
    /// <para>不是对外契约 —— 对外契约仍是 <see cref="NodeTestResponse"/> / <see cref="AiNodeTestResponse"/>，
    /// 保持前端零破坏。</para>
    /// </summary>
    public class NodeScopeRunOutcome
    {
        /// <summary>任务编码（已落库，可用于查 wf_node_execution）</summary>
        public string TaskCode { get; set; } = "";

        /// <summary>执行项编码</summary>
        public string ItemCode { get; set; } = "";

        /// <summary>目标节点是否执行成功</summary>
        public bool Success { get; set; }

        /// <summary>失败原因</summary>
        public string? Error { get; set; }

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>目标节点输出</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>AI 节点测试专用：打平后的输出池（nodeId / title → 输出），供控制器构造 Debug 信息</summary>
        public Dictionary<string, object> FlattenedOutputs { get; set; } = new();

        /// <summary>AI 节点测试专用：前端传入的原始 mockOutputs（nodeId → 输出）</summary>
        public Dictionary<string, Dictionary<string, object>> MockOutputs { get; set; } = new();
    }

    /// <summary>
    /// WfExecutionTaskService — 单节点 / AI 节点测试（TestScope = NODE / AI_NODE）
    ///
    /// <para><b>2026-09-22 阶段二：测试入口统一</b></para>
    /// <para>原实现（<c>WorkflowTestController.TestNode</c> / <c>TestAiNode</c>）直接调
    /// <c>NodeExecutor</c> / <c>AiNodeExecutor</c>，只打 ILogger，<b>完全不落库</b>（缺口 G6）——
    /// 于是「点一下节点测试」这件事在系统里不留任何痕迹：没有 wf_execution_task、
    /// 没有 wf_node_execution、没有可机器解析的执行日志，也就无法回溯「上次测的是什么、结果如何」。</para>
    ///
    /// <para>现改为与整流测试共用同一条管线：</para>
    /// <list type="number">
    ///   <item>建 <c>wf_execution_task</c>（TestScope=NODE / AI_NODE）+ <c>wf_execution_task_item</c></item>
    ///   <item>预热任务级缓存</item>
    ///   <item>执行节点（真实 taskCode/itemCode 进入日志，可与 DB 行对齐）</item>
    ///   <item>复用 <see cref="SaveNodeExecutionsAsync"/> 落 <c>wf_node_execution</c></item>
    ///   <item>复用 <see cref="FinishRunAsync"/> 收口 + 清缓存</item>
    /// </list>
    ///
    /// <para><b>伪路径设计</b>：单节点测试的 PathResults 只有一条路径 ——</para>
    /// <list type="bullet">
    ///   <item>TestScope=NODE：<c>[目标节点]</c></item>
    ///   <item>TestScope=AI_NODE：<c>[上游节点…, 目标 AI 节点]</c>（上游由 ruleJson 反向 BFS 得到）</item>
    /// </list>
    /// <para>路径虽短，但 <see cref="PathResult.NodeResults"/> / 落库行 / 日志 Tag 与整流测试<b>完全同构</b>，
    /// 因此前端 <c>ExecutionResultPanel</c> 无需为「节点测试」写分支判断。</para>
    ///
    /// <para><b>不走 WorkflowConfigParser.Parse</b>：单节点测试前端只传目标节点，
    /// AI 节点测试传的 ruleJson 也可能拓扑不完整，走解析器会被拓扑校验挡掉。
    /// 这里用 <see cref="BuildSyntheticWorkflow"/> 直接合成 NodeMap（落库与缓存预热只依赖 NodeMap）。</para>
    /// </summary>
    public partial class WfExecutionTaskService
    {
        // ════════════════════════════════════════════════════════════
        // 入口 1：任意单节点测试（TestScope=NODE）
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 执行单个节点并落库（前端 NC/报告配置页「测试节点」按钮）
        /// <para>连线输入在测试时按常量处理（无需上游节点），故 sharedOutputs 传空。</para>
        /// </summary>
        public async Task<NodeScopeRunOutcome> RunSingleNodeAsync(
            NodeTestRequest request,
            CancellationToken ct = default)
        {
            var nodeConfig = new WorkflowNodeConfig
            {
                NodeId = request.NodeId ?? "test_node",
                NodeType = request.NodeType ?? "skill",
                Title = request.Title ?? "测试节点",
                SkillCode = request.SkillCode,
                Config = request.Config ?? new Dictionary<string, object>(),
                Inputs = request.Inputs ?? new Dictionary<string, string>(),
                InputTypes = request.InputTypes ?? new Dictionary<string, string>(),
                InputPorts = request.InputPorts ?? new List<PortConfig>(),
                OutputPorts = request.OutputPorts ?? new List<PortConfig>()
            };

            var ruleCode = request.RuleCode ?? "";
            var enterpriseCode = ResolveEnterpriseCode(request.EnterpriseCode);
            var phaseCode = request.PhaseCode ?? "";
            var standardCode = request.StandardCode ?? "";

            var taskCode = Guid.NewGuid().ToString("N");
            var itemCode = Guid.NewGuid().ToString("N");

            _wfLogger.TaskCreate(taskCode, "TEST", ruleCode, enterpriseCode);

            var parsed = BuildSyntheticWorkflow(new[] { nodeConfig });
            var cacheKeys = await BeginRunAsync(
                taskCode, itemCode, "TEST", "NODE",
                BuildSnapshotJson(new[] { nodeConfig }),
                ruleCode, enterpriseCode, phaseCode, parsed, ct);

            var contextParams = BuildContextParams(enterpriseCode, phaseCode, standardCode);

            _wfLogger.ItemStart(taskCode, itemCode, ruleCode);
            _wfLogger.PathEnum(taskCode, itemCode, 1);
            _wfLogger.PathStart(taskCode, itemCode, 0, 1);

            var sw = Stopwatch.StartNew();

            // NodeExecutor 内部 try/catch 全吞，恒返回 NodeExecutionResult（不抛异常）
            var result = await _nodeExecutor.ExecuteAsync(
                nodeConfig, taskCode, itemCode,
                new Dictionary<string, object>(),   // sharedOutputs：单节点测试无上游
                contextParams, ct);

            sw.Stop();

            var itemResult = await PersistNodeScopeAsync(
                taskCode, itemCode, parsed,
                new List<(WorkflowNodeConfig, NodeExecutionResult)> { (nodeConfig, result) },
                sw.ElapsedMilliseconds, cacheKeys, ct);

            return new NodeScopeRunOutcome
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Success = result.Success,
                Error = result.Error,
                DurationMs = result.DurationMs,
                Output = result.Output,
                FlattenedOutputs = new Dictionary<string, object>(),
                MockOutputs = new Dictionary<string, Dictionary<string, object>>()
            };
        }

        // ════════════════════════════════════════════════════════════
        // 入口 2：AI 节点测试（TestScope=AI_NODE）
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 执行单个 AI 节点并落库（前端 AI 节点「测试」按钮）
        /// <para>AI 节点需要完整工作流上下文（上游节点输出）：</para>
        /// <list type="bullet">
        ///   <item>前端已传 <c>MockOutputs</c> → 直接用（不打平上游执行）</item>
        ///   <item>MockOutputs 为空且有 <c>RuleJson</c> → 从目标节点反向 BFS 出上游链，按拓扑顺序真实执行</item>
        /// </list>
        /// <para>落库路径 = <c>[实际执行过的上游节点…, 目标 AI 节点]</c>，
        /// 于是「AI 节点测试顺手跑了哪些上游节点」也一并留痕（原实现只在日志里，DB 无记录）。</para>
        /// </summary>
        public async Task<NodeScopeRunOutcome> RunAiNodeAsync(
            AiNodeTestRequest request,
            CancellationToken ct = default)
        {
            var nodeConfig = new WorkflowNodeConfig
            {
                NodeId = request.NodeId ?? "test_ai_node",
                NodeType = "ai_node",
                Title = request.Title ?? "AI 测试节点",
                Config = request.Config ?? new Dictionary<string, object>(),
                Inputs = request.Inputs ?? new Dictionary<string, string>(),
                InputTypes = request.InputTypes ?? new Dictionary<string, string>(),
                InputPorts = request.InputPorts ?? new List<PortConfig>(),
                OutputPorts = request.OutputPorts ?? new List<PortConfig>()
            };

            var ruleCode = request.RuleCode ?? "";
            var enterpriseCode = ResolveEnterpriseCode(
                request.EnterpriseCode ?? ReadContextParam(request, "EnterpriseCode", "enterpriseCode"));
            var phaseCode = request.PhaseCode ?? ReadContextParam(request, "PhaseCode", "phaseCode") ?? "";
            var standardCode = request.StandardCode ?? ReadContextParam(request, "StandardCode", "standardCode") ?? "";

            var mockOutputs = request.WorkflowContext?.MockOutputs
                ?? new Dictionary<string, Dictionary<string, object>>();
            var contextParams = request.WorkflowContext?.ContextParams
                ?? new Dictionary<string, object>();

            // 1. 打平 mockOutputs（同时以 nodeId 与 title 作为 key，供 {{节点标题.端口}} 引用）
            var flattenedOutputs = new Dictionary<string, object>();
            foreach (var (nodeKey, nodeOutput) in mockOutputs)
            {
                flattenedOutputs[nodeKey] = nodeOutput.TryGetValue("result", out var res)
                    ? res
                    : nodeOutput;
            }

            // 2. 解析上游执行计划（纯计算，不碰 DB；要在 BeginRunAsync 之前完成，因为 NodeMap 决定落库行）
            var upstreamPlan = new List<WorkflowNodeConfig>();

            if (mockOutputs.Count == 0 && !string.IsNullOrEmpty(request.WorkflowContext?.RuleJson))
            {
                upstreamPlan = ResolveUpstreamPlan(request.WorkflowContext.RuleJson, nodeConfig.NodeId);
            }

            // 3. 合成 ParsedWorkflow：NodeMap 必须覆盖「所有会落库的节点」（上游 + 目标）
            var persistedNodes = new List<WorkflowNodeConfig>(upstreamPlan) { nodeConfig };
            var parsed = BuildSyntheticWorkflow(persistedNodes);

            var taskCode = Guid.NewGuid().ToString("N");
            var itemCode = Guid.NewGuid().ToString("N");

            _wfLogger.TaskCreate(taskCode, "TEST", ruleCode, enterpriseCode);

            var cacheKeys = await BeginRunAsync(
                taskCode, itemCode, "TEST", "AI_NODE",
                BuildSnapshotJson(persistedNodes),
                ruleCode, enterpriseCode, phaseCode, parsed, ct);

            _wfLogger.ItemStart(taskCode, itemCode, ruleCode);
            _wfLogger.PathEnum(taskCode, itemCode, 1);
            _wfLogger.PathStart(taskCode, itemCode, 0, persistedNodes.Count);

            var executed = new List<(WorkflowNodeConfig, NodeExecutionResult)>();

            // 4. 先注入 contextParams（模拟 start 节点），与整流测试语义一致
            foreach (var (k, v) in contextParams)
                flattenedOutputs[k] = v;

            var sw = Stopwatch.StartNew();

            // 5. 按拓扑顺序执行上游节点（start/end/branch/ai_node 不执行，与旧实现一致）
            foreach (var upNode in upstreamPlan)
            {
                ct.ThrowIfCancellationRequested();

                var upResult = await _nodeExecutor.ExecuteAsync(
                    upNode, taskCode, itemCode, flattenedOutputs, contextParams, ct);

                executed.Add((upNode, upResult));

                if (upResult.Success)
                {
                    flattenedOutputs[upNode.NodeId] = upResult.Output;
                    if (!string.IsNullOrEmpty(upNode.Title))
                        flattenedOutputs[upNode.Title] = upResult.Output;
                }
                else
                {
                    _logger.LogWarning(
                        "[WorkflowEngine] AI 节点测试：上游节点 {NodeId} 执行失败: {Error}",
                        upNode.NodeId, upResult.Error);
                }
            }

            // 6. 执行目标 AI 节点
            //    ⚠️ 这里直接调 AiNodeExecutor.ExecuteWithMockAsync（它内部不走 NodeExecutor，
            //       因此不会自动产生 NODE_* 日志，需在此手工补齐，否则 AI 节点测试的日志链有断口）
            _wfLogger.NodeStart(taskCode, itemCode, nodeConfig.NodeId, "ai_node", nodeConfig.Title);
            _wfLogger.NodeExec(taskCode, itemCode, nodeConfig.NodeId, "invoke_llm");

            var aiStartedAt = DateTime.Now;
            var aiSw = Stopwatch.StartNew();

            // ExecuteWithMockAsync 内部 try/catch 全吞，恒返回结果（不抛异常），但不会写 StartedAt/CompletedAt
            var aiResult = await _aiNodeExecutor.ExecuteWithMockAsync(
                nodeConfig, flattenedOutputs, contextParams, precomputedParams: null, ct);

            aiSw.Stop();
            aiResult.StartedAt = aiStartedAt;
            aiResult.CompletedAt = DateTime.Now;

            if (aiResult.Success)
            {
                _wfLogger.NodeOutput(taskCode, itemCode, nodeConfig.NodeId,
                    JsonSerializer.Serialize(aiResult.Output), (int)aiSw.ElapsedMilliseconds);
                _wfLogger.NodeDone(taskCode, itemCode, nodeConfig.NodeId, (int)aiSw.ElapsedMilliseconds);
            }
            else
            {
                _wfLogger.NodeFail(taskCode, itemCode, nodeConfig.NodeId,
                    aiResult.Error ?? "AI 节点执行失败", (int)aiSw.ElapsedMilliseconds);
            }

            executed.Add((nodeConfig, aiResult));
            sw.Stop();

            await PersistNodeScopeAsync(taskCode, itemCode, parsed, executed, sw.ElapsedMilliseconds, cacheKeys, ct);

            return new NodeScopeRunOutcome
            {
                TaskCode = taskCode,
                ItemCode = itemCode,
                Success = aiResult.Success,
                Error = aiResult.Error,
                DurationMs = aiResult.DurationMs,
                Output = aiResult.Output,
                FlattenedOutputs = flattenedOutputs,
                MockOutputs = mockOutputs
            };
        }

        // ════════════════════════════════════════════════════════════
        // 内部辅助
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 落库 + 收口（单节点 / AI 节点共用），返回聚合后的 ItemExecutionResult
        /// <para>把「节点执行序列」翻译成一条伪路径，从而复用整流测试的落库与日志路径。</para>
        /// </summary>
        private async Task<ItemExecutionResult> PersistNodeScopeAsync(
            string taskCode,
            string itemCode,
            ParsedWorkflow parsed,
            List<(WorkflowNodeConfig Node, NodeExecutionResult Result)> executed,
            long elapsedMs,
            List<string> cacheKeys,
            CancellationToken ct)
        {
            var pathStartedAt = executed.Count > 0 ? executed[0].Result.StartedAt : DateTime.Now;
            var pathCompletedAt = executed.Count > 0 ? executed[^1].Result.CompletedAt : DateTime.Now;
            var failed = executed.FirstOrDefault(e => !e.Result.Success);
            var allSuccess = executed.Count > 0 && executed.All(e => e.Result.Success);
            var targetOutput = executed.Count > 0 ? executed[^1].Result.Output : new Dictionary<string, object>();

            var pathResult = new PathResult
            {
                PathIndex = 0,
                Status = allSuccess ? "completed" : "failed",
                FailedAtNodeId = failed.Node?.NodeId,
                Error = failed.Result?.Error,
                Output = targetOutput,
                NodeIds = executed.Select(e => e.Node.NodeId).ToList(),
                NodeResults = executed
                    .Select(e => NodeExecutionRecord.From(e.Node.NodeId, e.Result, isReused: false))
                    .ToList(),
                DurationMs = (int)elapsedMs,
                StartedAt = pathStartedAt,
                CompletedAt = pathCompletedAt
            };

            // 与 WorkflowInterpreter.AggregateNcResult 保持同构（含 executedPaths/successPaths/failedPaths），
            // 使前端对两种测试的结果解析逻辑完全一致
            var ncResult = new Dictionary<string, object>
            {
                ["success"] = allSuccess,
                ["error"] = allSuccess ? null! : (failed.Result?.Error ?? "节点执行失败"),
                ["result"] = allSuccess
                    ? targetOutput.GetValueOrDefault("result") ?? targetOutput
                    : null!,
                ["executedPaths"] = new[] { 0 },
                ["successPaths"] = allSuccess ? new[] { 0 } : Array.Empty<int>(),
                ["failedPaths"] = allSuccess ? Array.Empty<int>() : new[] { 0 }
            };

            var itemResult = new ItemExecutionResult
            {
                Success = allSuccess,
                IsSuccess = allSuccess,
                Error = ncResult["error"] as string,
                PathResults = new List<PathResult> { pathResult },
                DurationMs = (int)elapsedMs,
                NcResult = ncResult
            };

            _wfLogger.PathDone(taskCode, itemCode, 0, pathResult.Status);
            if (pathResult.Status == "failed")
                _wfLogger.PathFail(taskCode, itemCode, 0, pathResult.FailedAtNodeId, pathResult.Error);

            // 四层落库：路径（本表，PathIndex=0 的伪路径）→ 节点
            await SavePathExecutionsAsync(taskCode, itemCode, itemResult, ct);
            await SaveNodeExecutionsAsync(taskCode, itemCode, parsed, itemResult, ct);

            _wfLogger.ItemDone(taskCode, itemCode, allSuccess, (int)elapsedMs);

            await FinishRunAsync(taskCode, itemCode, itemResult, elapsedMs, cacheKeys, ct);

            return itemResult;
        }

        /// <summary>
        /// 从 ruleJson 反向 BFS 出目标节点的上游链，并按拓扑顺序返回「需要真实执行」的节点
        /// <para>start / end / branch / ai_node 不执行（与旧 <c>WorkflowTestController.TestAiNode</c> 行为一致）：
        /// start 由 contextParams 注入代替，end/branch 在单节点测试里无意义，
        /// ai_node 不递归执行（只测目标那一个）。</para>
        /// <para>解析失败时返回空列表（退回空上下文），不阻断目标节点测试 —— 与旧实现一致。</para>
        /// </summary>
        private List<WorkflowNodeConfig> ResolveUpstreamPlan(string ruleJson, string targetNodeId)
        {
            var plan = new List<WorkflowNodeConfig>();

            WorkflowConfig? wfConfig;
            try
            {
                // 跳过拓扑校验：测试期工作流可能不完整（与旧实现一致）
                wfConfig = JsonSerializer.Deserialize<WorkflowConfig>(
                    ruleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[WorkflowEngine] AI 节点测试：ruleJson 解析失败，跳过上游执行（目标节点仍会执行）");
                return plan;
            }

            if (wfConfig?.Nodes == null || wfConfig.Nodes.Count == 0)
                return plan;

            var allMap = wfConfig.Nodes.ToDictionary(n => n.NodeId, n => n);

            // 入边表
            var inEdges = wfConfig.Nodes.ToDictionary(
                n => n.NodeId, _ => new List<WorkflowEdgeConfig>());
            foreach (var edge in wfConfig.Edges ?? new List<WorkflowEdgeConfig>())
            {
                if (inEdges.TryGetValue(edge.Target, out var list))
                    list.Add(edge);
            }

            // 反向 BFS：目标节点 → 所有上游（返回顺序为拓扑序）
            var upstreamIds = FindUpstreamNodes(inEdges, targetNodeId);

            foreach (var upId in upstreamIds)
            {
                if (!allMap.TryGetValue(upId, out var upNode))
                    continue;

                var nt = upNode.NodeType?.ToLowerInvariant();
                if (nt is "start" or "end" or "branch" or "ai_node")
                    continue;

                // 按入边补齐 inputs（确保上游数据可用），与旧实现一致
                if (inEdges.TryGetValue(upId, out var upInEdges) && upInEdges.Count > 0)
                {
                    upNode.Inputs ??= new Dictionary<string, string>();
                    upNode.InputTypes ??= new Dictionary<string, string>();
                    foreach (var e in upInEdges)
                    {
                        var portName = !string.IsNullOrEmpty(e.TargetHandle)
                            ? e.TargetHandle
                            : (upNode.InputPorts?.FirstOrDefault()?.Name ?? "result");
                        if (!upNode.Inputs.ContainsKey(portName))
                        {
                            upNode.Inputs[portName] = e.Source;
                            if (!upNode.InputTypes.ContainsKey(portName))
                                upNode.InputTypes[portName] = "link";
                        }
                    }
                }

                plan.Add(upNode);
            }

            return plan;
        }

        /// <summary>
        /// 从指定节点开始反向 BFS，找到所有上游节点（按拓扑排序）
        /// <para>迁移自旧 <c>WorkflowTestController.FindUpstreamNodes</c>，逻辑零改动。</para>
        /// </summary>
        private static List<string> FindUpstreamNodes(
            Dictionary<string, List<WorkflowEdgeConfig>> inEdges,
            string targetNodeId)
        {
            var result = new List<string>();
            var visited = new HashSet<string>();
            var queue = new Queue<string>();

            queue.Enqueue(targetNodeId);
            visited.Add(targetNodeId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();

                if (!inEdges.TryGetValue(currentId, out var edges))
                    continue;

                foreach (var edge in edges)
                {
                    if (visited.Contains(edge.Source))
                        continue;

                    visited.Add(edge.Source);
                    result.Add(edge.Source);
                    queue.Enqueue(edge.Source);
                }
            }

            result.Reverse();
            return result;
        }

        /// <summary>构造节点级测试的上下文参数（与整流测试的键名逐字一致：camelCase）</summary>
        private static Dictionary<string, object> BuildContextParams(
            string enterpriseCode, string phaseCode, string standardCode)
            => new()
            {
                ["enterpriseCode"] = enterpriseCode,
                ["phaseCode"] = phaseCode,
                ["standardCode"] = standardCode
            };

        /// <summary>企业编码缺省值：单节点测试默认打标准企业（与 NodeExecutor 内部兜底一致）</summary>
        private static string ResolveEnterpriseCode(string? enterpriseCode)
            => string.IsNullOrWhiteSpace(enterpriseCode) ? "YZH-STD-ENT" : enterpriseCode!;

        /// <summary>从 AI 节点的 WorkflowContext.ContextParams 读一个键（兼容 PascalCase / camelCase）</summary>
        private static string? ReadContextParam(AiNodeTestRequest request, params string[] keys)
        {
            var ctx = request.WorkflowContext?.ContextParams;
            if (ctx == null) return null;

            foreach (var key in keys)
            {
                if (ctx.TryGetValue(key, out var v) && v != null)
                    return v.ToString();
            }
            return null;
        }

        /// <summary>把节点列表序列化为 wf_execution_task.ConfigSnapshot（快照，便于事后还原「测的是什么」）</summary>
        private static string BuildSnapshotJson(IEnumerable<WorkflowNodeConfig> nodes)
        {
            var config = new WorkflowConfig
            {
                Version = 1,
                WorkflowType = "validation",
                Nodes = nodes.ToList(),
                Edges = new List<WorkflowEdgeConfig>()
            };
            return JsonSerializer.Serialize(config);
        }
    }
}
