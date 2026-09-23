using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// 工作流解释器 — 引擎核心，负责路径驱动、节点串行阻塞执行、跨路径复用、NC 结果聚合
    /// <para>移植自：旧 WorkflowInterpreter.cs（317 行，逻辑零改动，仅 namespace 调整）</para>
    /// <para>职责：</para>
    /// <para>1. 接收 ParsedWorkflow（已解析的拓扑+路径），驱动执行</para>
    /// <para>2. 路径内节点串行阻塞：一个节点完成后再执行下一个</para>
    /// <para>3. 路径间可并行（本版本先串行，后续可改为并行）</para>
    /// <para>4. 跨路径复用：同一 node_id 已执行 → 从结果池读取，不重跑</para>
    /// <para>5. 节点失败 → 当前路径终止，继续下一路径</para>
    /// <para>6. 所有路径终结后聚合 NC 结果</para>
    ///
    /// <para>2026-09-22 修复（G1–G5）：原实现只把节点 ID 列表塞进 <see cref="PathResult.NodeIds"/>，
    /// per-node 的<b>输出/耗时/时序/复用标记</b>在路径聚合时被丢弃，
    /// 导致 wf_node_execution 只能靠路径级 Status 反推 —— 所有节点同状态、耗时恒 0、
    /// 只有路径最后一个节点有输出。现改为逐节点记录 <see cref="NodeExecutionRecord"/>。</para>
    /// <para>2026-09-22 修复（G11）：日志全部改走 <see cref="WorkflowLogger"/>，不再裸用 ILogger 拼字符串。</para>
    /// </summary>
    public class WorkflowInterpreter
    {
        private readonly NodeExecutor _nodeExecutor;
        private readonly WorkflowLogger _wfLogger;

        public WorkflowInterpreter(
            NodeExecutor nodeExecutor,
            WorkflowLogger wfLogger)
        {
            _nodeExecutor = nodeExecutor;
            _wfLogger = wfLogger;
        }

        /// <summary>
        /// 执行一个 Item（一个 NC 检查项的所有路径）
        /// </summary>
        /// <param name="parsed">解析后的工作流模型</param>
        /// <param name="taskCode">任务编码</param>
        /// <param name="itemCode">执行项编码</param>
        /// <param name="ruleCode">规则编码（仅用于日志，不参与执行）</param>
        /// <param name="contextParams">start 节点注入的上下文</param>
        /// <param name="ct">取消令牌</param>
        public async Task<ItemExecutionResult> ExecuteItemAsync(
            ParsedWorkflow parsed,
            string taskCode,
            string itemCode,
            string ruleCode,
            Dictionary<string, object> contextParams,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();

            _wfLogger.ItemStart(taskCode, itemCode, ruleCode);
            _wfLogger.PathEnum(taskCode, itemCode, parsed.Paths.Count);

            // 共享输出池：nodeId → 输出（跨路径复用的核心）
            // 所有路径共享同一份已执行节点的输出
            var sharedOutputs = new Dictionary<string, object>();

            // 已执行节点记录：nodeId → NodeExecutionResult（用于跨路径复用判断）
            var executedNodes = new Dictionary<string, NodeExecutionResult>();

            var pathResults = new List<PathResult>();

            // 逐路径执行（当前版本串行，后续可改为并行）
            for (int pathIndex = 0; pathIndex < parsed.Paths.Count; pathIndex++)
            {
                ct.ThrowIfCancellationRequested();

                var path = parsed.Paths[pathIndex];
                var pathResult = await ExecutePathAsync(
                    path, pathIndex, taskCode, itemCode,
                    sharedOutputs, executedNodes, contextParams, ct);

                pathResults.Add(pathResult);

                _wfLogger.PathDone(taskCode, itemCode, pathIndex, pathResult.Status);

                if (pathResult.Status == "failed")
                {
                    _wfLogger.PathFail(taskCode, itemCode, pathIndex, pathResult.FailedAtNodeId, pathResult.Error);
                }
            }

            // 聚合 NC 结果
            var ncResult = AggregateNcResult(pathResults);
            var isSuccess = DetermineItemSuccess(pathResults);

            sw.Stop();

            _wfLogger.ItemDone(taskCode, itemCode, isSuccess, (int)sw.ElapsedMilliseconds);

            return new ItemExecutionResult
            {
                Success = ncResult.GetValueOrDefault("success") as bool? ?? false,
                Error = ncResult.GetValueOrDefault("error") as string,
                IsSuccess = isSuccess,
                PathResults = pathResults,
                DurationMs = (int)sw.ElapsedMilliseconds,
                NcResult = ncResult
            };
        }

        // ── 路径执行（串行阻塞） ──

        /// <summary>
        /// 执行单条路径（串行阻塞）
        /// </summary>
        private async Task<PathResult> ExecutePathAsync(
            List<WorkflowNodeConfig> path,
            int pathIndex,
            string taskCode,
            string itemCode,
            Dictionary<string, object> sharedOutputs,
            Dictionary<string, NodeExecutionResult> executedNodes,
            Dictionary<string, object> contextParams,
            CancellationToken ct)
        {
            var nodeIds = path.Select(n => n.NodeId).ToList();

            _wfLogger.PathStart(taskCode, itemCode, pathIndex, path.Count);

            var pathStartedAt = DateTime.Now;
            var pathSw = Stopwatch.StartNew();

            var pathResult = new PathResult
            {
                PathIndex = pathIndex,
                Status = "executing",
                NodeIds = nodeIds,
                StartedAt = pathStartedAt
            };

            // 逐节点串行执行
            foreach (var node in path)
            {
                ct.ThrowIfCancellationRequested();

                // 跨路径复用检查
                if (executedNodes.TryGetValue(node.NodeId, out var existingResult))
                {
                    _wfLogger.NodeReuse(taskCode, itemCode, node.NodeId, node.NodeId);

                    // 复用也要留下明细：IsReused=true，输出/耗时/时序取自原始执行那次
                    pathResult.NodeResults.Add(
                        NodeExecutionRecord.From(node.NodeId, existingResult, isReused: true));

                    // 已执行过的节点，直接使用结果（不重跑）
                    // 失败的节点也需要记录，让路径终止
                    if (!existingResult.Success)
                    {
                        return FinishPath(pathResult, "failed", node.NodeId, existingResult.Error, pathSw);
                    }

                    // branch 节点复用时不影响路径（路径已在枚举时确定）
                    continue;
                }

                // 执行节点
                var result = await _nodeExecutor.ExecuteAsync(
                    node, taskCode, itemCode, sharedOutputs, contextParams, ct);

                // 记录到已执行池
                executedNodes[node.NodeId] = result;

                // 记录 per-node 明细（真执行，IsReused=false）——中间节点的输出在此保留，不再丢失
                pathResult.NodeResults.Add(
                    NodeExecutionRecord.From(node.NodeId, result, isReused: false));

                // 节点失败 → 当前路径终止
                if (!result.Success)
                {
                    return FinishPath(pathResult, "failed", node.NodeId, result.Error, pathSw);
                }

                // 节点成功 → 输出写入共享池（同时写入 nodeId 和 title 作为 key）
                // AI 节点通过 {{节点标题.端口}} 引用上游输出时，需要 title 作为 key
                sharedOutputs[node.NodeId] = result.Output;
                if (!string.IsNullOrEmpty(node.Title))
                    sharedOutputs[node.Title] = result.Output;
            }

            // 路径中所有节点执行成功
            var finished = FinishPath(pathResult, "completed", null, null, pathSw);

            // 路径最终输出 = 最后一个节点（通常是 end）的输出
            var lastNode = path.Last();
            if (executedNodes.TryGetValue(lastNode.NodeId, out var lastResult) && lastResult.Success)
            {
                finished.Output = lastResult.Output;
            }

            return finished;
        }

        /// <summary>
        /// 收口路径结果：写入终态、失败点、耗时与时序（成功/失败两条出口共用，避免时序漏写）
        /// </summary>
        private static PathResult FinishPath(
            PathResult pathResult, string status, string? failedAtNodeId, string? error, Stopwatch pathSw)
        {
            pathSw.Stop();
            pathResult.Status = status;
            pathResult.FailedAtNodeId = failedAtNodeId;
            pathResult.Error = error;
            pathResult.DurationMs = (int)pathSw.ElapsedMilliseconds;
            pathResult.CompletedAt = DateTime.Now;
            return pathResult;
        }

        // ── NC 结果聚合 ──

        /// <summary>
        /// 聚合所有路径的结果为 NC 最终结果
        /// <para>规则：</para>
        /// <para>  有任何路径到达 end 且成功 → NC 通过</para>
        /// <para>  所有路径都失败 → NC 失败</para>
        /// <para>  结果提取：优先取 result 字段；如果 result 是多 key 字典，尝试从中提取 result/compare_result 字段</para>
        /// </summary>
        private Dictionary<string, object> AggregateNcResult(List<PathResult> pathResults)
        {
            var successPaths = pathResults
                .Where(r => r.Status == "completed")
                .ToList();

            var failedPaths = pathResults
                .Where(r => r.Status == "failed")
                .ToList();

            if (successPaths.Count > 0)
            {
                // 取第一条成功路径的输出
                var firstSuccess = successPaths[0];
                var rawResult = firstSuccess.Output?.GetValueOrDefault("result") ?? firstSuccess.Output;

                // 智能提取：如果 result 是多 key 字典（来自上游节点的完整输出），从中提取 compare_result/result 单值
                var result = ExtractSingleResult(rawResult);

                return new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["error"] = null!,
                    ["result"] = result ?? new { },
                    ["executedPaths"] = pathResults.Select(p => p.PathIndex).ToArray(),
                    ["successPaths"] = successPaths.Select(p => p.PathIndex).ToArray(),
                    ["failedPaths"] = failedPaths.Select(p => p.PathIndex).ToArray()
                };
            }

            if (failedPaths.Count > 0)
            {
                var errors = failedPaths
                    .Where(f => !string.IsNullOrEmpty(f.Error))
                    .Select(f => $"路径{f.PathIndex}@{f.FailedAtNodeId}: {f.Error}")
                    .ToList();

                var errorMsg = errors.Count > 0
                    ? string.Join("; ", errors)
                    : "所有路径均失败";

                return new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = errorMsg,
                    ["result"] = null!,
                    ["executedPaths"] = pathResults.Select(p => p.PathIndex).ToArray(),
                    ["successPaths"] = Array.Empty<int>(),
                    ["failedPaths"] = failedPaths.Select(p => p.PathIndex).ToArray()
                };
            }

            // 所有路径都走完了但没有有效结果
            return new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = "所有路径均无有效结果，工作流配置可能不合理",
                ["result"] = null!,
                ["executedPaths"] = pathResults.Select(p => p.PathIndex).ToArray(),
                ["successPaths"] = Array.Empty<int>(),
                ["failedPaths"] = Array.Empty<int>()
            };
        }

        /// <summary>
        /// 判断 Item 的业务成功标志
        /// <para>规则：至少一条路径到达 end 且成功 → true</para>
        /// </summary>
        private static bool DetermineItemSuccess(List<PathResult> pathResults)
        {
            return pathResults.Any(r => r.Status == "completed");
        }

        /// <summary>
        /// 从结果对象中提取单值结果
        /// <para>规则：</para>
        /// <para>  非字典 → 直接返回</para>
        /// <para>  单 key 字典 → 返回其 value</para>
        /// <para>  多 key 字典（上游节点完整输出）→ 优先返回 result 字段，其次 compare_result 字段</para>
        /// </summary>
        private static object? ExtractSingleResult(object? rawResult)
        {
            if (rawResult is not Dictionary<string, object> dict)
                return rawResult;

            // 单 key → 直接返回其 value
            if (dict.Count == 1)
                return dict.Values.First();

            // 多 key → 优先取 result，其次 compare_result
            if (dict.TryGetValue("result", out var resultValue))
                return resultValue;

            if (dict.TryGetValue("compare_result", out var compareResultValue))
                return compareResultValue;

            // 没有已知 key → 返回整个字典
            return dict;
        }
    }
}
