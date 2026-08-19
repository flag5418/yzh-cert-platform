using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.Workflow.Models;

namespace YZH.Core.Workflow
{
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly ISkillRegistry _registry;
        private readonly ILogger<WorkflowEngine> _logger;

        public WorkflowEngine(ISkillRegistry registry, ILogger<WorkflowEngine> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        public async Task<WorkflowRunResult> RunAsync(string workflowConfigJson, WorkflowContext context, CancellationToken ct = default)
        {
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            opts.Converters.Add(new BranchOpConverter());
var wf = JsonSerializer.Deserialize<WorkflowConfig>(workflowConfigJson, opts)
                     ?? throw new WorkflowExecutionException("workflow_config 解析失败");
            var sw = Stopwatch.StartNew();
            var outputs = new Dictionary<string, IDictionary<string, object>>();

            var allEdges = new List<WorkflowEdge>(wf.Edges);
            foreach (var b in wf.Branches ?? new())
                allEdges.AddRange(LinearBranchEdges(b));

            var order = TopoSort(wf.AllNodes(), allEdges);

            foreach (var nodeId in order)
            {
                ct.ThrowIfCancellationRequested();
                var (node, isBranch, branch) = wf.FindNode(nodeId);
                if (isBranch && branch != null)
                {
                    if (!outputs.TryGetValue(branch.From, out var fromOut))
                    { await WriteSkipped(node, context, ct, "branch_from 节点未执行"); continue; }
                    var condResult = MatchCondition(branch.Condition, fromOut!);
                    if (!condResult)
                    { await WriteSkipped(node, context, ct, "condition 未命中"); continue; }
                }

                var result = await ExecuteNodeAsync(node, outputs, context, ct);
                outputs[nodeId] = result.Outputs;
                if (!result.Success)
                    return new WorkflowRunResult { Success = false, FailedNodeId = nodeId, Error = result.Error, DurationMs = sw.ElapsedMilliseconds };
            }

            sw.Stop();
            int? pt = null, ct2 = null;
            foreach (var nid in order.Reverse())
            {
                if (outputs.TryGetValue(nid, out var outDict)
                    && outDict.TryGetValue("prompt_tokens", out var p)
                    && outDict.TryGetValue("completion_tokens", out var c))
                {
                    pt = Convert.ToInt32(p);
                    ct2 = Convert.ToInt32(c);
                    break;
                }
            }
            return new WorkflowRunResult { Success = true, NodeOutputs = outputs, DurationMs = sw.ElapsedMilliseconds, PromptTokens = pt, CompletionTokens = ct2 };
        }

        private static async Task WriteSkipped(WorkflowNode node, WorkflowContext context, CancellationToken ct, string reason)
        {
            if (context.LogStore == null) return;
            await context.LogStore.WriteAsync(new ExecutionLogEntry
            {
                WorkflowCode = context.WorkflowInstanceId,
                BusinessType = context.BusinessType,
                BusinessCode = context.BusinessCode,
                BusinessId = context.BusinessId,
                NodeId = node.NodeId,
                SkillCode = node.SkillCode,
                Status = "skipped",
                ErrorMsg = reason,
                StartedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            }, ct);
        }

        private async Task<SkillResult> ExecuteNodeAsync(WorkflowNode node, Dictionary<string, IDictionary<string, object>> outputs,
            WorkflowContext context, CancellationToken ct)
        {
            var inputs = ResolveInputs(node.Inputs, outputs, context.Inputs);
            var skillCtx = new SkillContext
            {
                Inputs = inputs,
                WorkflowInstanceId = context.WorkflowInstanceId,
                NodeId = node.NodeId,
                Logger = _logger
            };
            var nodeSw = Stopwatch.StartNew();
            SkillResult result;
            try
            {
                result = await _registry.ExecuteAsync(node.SkillCode, skillCtx, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = new SkillResult { Success = false, Error = ex.Message };
            }
            nodeSw.Stop();
            result.DurationMs = nodeSw.ElapsedMilliseconds;

            if (context.LogStore != null)
            {
                await context.LogStore.WriteAsync(new ExecutionLogEntry
                {
                    WorkflowCode = context.WorkflowInstanceId,
                    BusinessType = context.BusinessType,
                    BusinessCode = context.BusinessCode,
                    BusinessId = context.BusinessId,
                    NodeId = node.NodeId,
                    SkillCode = node.SkillCode,
                    InputDataJson = JsonSerializer.Serialize(inputs).Truncate(16 * 1024),
                    OutputDataJson = JsonSerializer.Serialize(result.Outputs).Truncate(64 * 1024),
                    Status = result.Success ? "success" : "failed",
                    ErrorMsg = result.Error,
                    DurationMs = result.DurationMs,
                    StartedAt = DateTime.Now,
                    CompletedAt = DateTime.Now
                }, ct);
            }
            return result;
        }

        private static IReadOnlyList<string> TopoSort(IReadOnlyList<WorkflowNode> nodes, IReadOnlyList<WorkflowEdge> edges)
        {
            var inDegree = new Dictionary<string, int>();
            var adj = new Dictionary<string, List<string>>();
            foreach (var n in nodes)
            {
                inDegree[n.NodeId] = 0;
                adj[n.NodeId] = new List<string>();
            }
            foreach (var e in edges)
            {
                if (!inDegree.ContainsKey(e.From)) inDegree[e.From] = 0;
                if (!inDegree.ContainsKey(e.To)) inDegree[e.To] = 0;
                if (!adj.ContainsKey(e.From)) adj[e.From] = new List<string>();
                inDegree[e.To]++;
                adj[e.From].Add(e.To);
            }
            var queue = new Queue<string>();
            foreach (var kv in inDegree)
                if (kv.Value == 0) queue.Enqueue(kv.Key);
            var result = new List<string>();
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                result.Add(curr);
                if (adj.TryGetValue(curr, out var neighbors))
                    foreach (var next in neighbors)
                    {
                        inDegree[next]--;
                        if (inDegree[next] == 0) queue.Enqueue(next);
                    }
            }
            if (result.Count != nodes.Count)
            {
                System.Console.WriteLine("[TopoSort] nodes.Count=" + nodes.Count + " result.Count=" + result.Count);
                foreach (var n in nodes) System.Console.WriteLine("[TopoSort] node=" + n.NodeId);
                foreach (var e in edges) System.Console.WriteLine("[TopoSort] edge=" + e.From + "->" + e.To);
                throw new WorkflowExecutionException("工作流存在环，无法拓扑排序");
            }
            return result;
        }

        private static Dictionary<string, object> ResolveInputs(Dictionary<string, object> inputs,
            Dictionary<string, IDictionary<string, object>> outputs, IDictionary<string, object> workflowInputs)
        {
            var resolved = new Dictionary<string, object>();
            foreach (var (key, val) in inputs)
                resolved[key] = ResolveValue(val, outputs, workflowInputs);
            return resolved;
        }

        /// <summary>
        /// 递归解析参数值：标量模板替换 + 数组/对象内元素模板替换（支撑 assemble 的 parts 常量+变量混合按序拼接）。
        /// 数字/布尔/日期原样保留（不破坏类型），字符串与 JSON 字符串元素做 {{}} 替换。
        /// </summary>
        private static object ResolveValue(object? val, Dictionary<string, IDictionary<string, object>> outputs,
            IDictionary<string, object> workflowInputs)
        {
            switch (val)
            {
                case string s:
                    return ResolveTemplate(s, outputs, workflowInputs);
                case JsonElement je:
                    return je.ValueKind switch
                    {
                        JsonValueKind.String => ResolveTemplate(je.GetString() ?? string.Empty, outputs, workflowInputs),
                        JsonValueKind.Array => je.EnumerateArray()
                            .Select(e => ResolveValue(e.Clone(), outputs, workflowInputs)).ToList(),
                        JsonValueKind.Object => je.EnumerateObject()
                            .ToDictionary(p => p.Name, p => ResolveValue(p.Value.Clone(), outputs, workflowInputs)),
                        _ => je // number/boolean/null 原样保留
                    };
                case System.Collections.IEnumerable enumerable when val is not string:
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                        list.Add(ResolveValue(item, outputs, workflowInputs));
                    return list;
                }
                default:
                    return val ?? string.Empty;
            }
        }

        private static object ResolveTemplate(string str, Dictionary<string, IDictionary<string, object>> outputs,
            IDictionary<string, object> workflowInputs)
        {
            if (!str.Contains("{{")) return str;
            return System.Text.RegularExpressions.Regex.Replace(str, @"\{\{(.*?)\}\}", m =>
            {
                var refPart = m.Groups[1].Value.Trim();
                if (refPart.StartsWith("input.")) // {{input.document_content}} → 工作流输入
                {
                    var key = refPart.Substring("input.".Length);
                    if (workflowInputs != null && workflowInputs.TryGetValue(key, out var v))
                        return (v ?? string.Empty).ToString() ?? string.Empty;
                    return string.Empty;
                }
                if (refPart.StartsWith("n")) // {{n1.output}} or {{n1.port}}
                {
                    var parts = refPart.Split('.');
                    var nodeId = parts[0];
                    var port = parts.Length > 1 ? parts[1] : "output";
                    if (outputs.TryGetValue(nodeId, out var nodeOut))
                        return nodeOut.TryGetValue(port, out var v) ? (v ?? string.Empty).ToString() : string.Empty;
                    return string.Empty;
                }
                return m.Value;
            });
        }

        private static bool MatchCondition(BranchCondition? condition, IDictionary<string, object> fromOutput)
        {
            if (condition == null) return true;
            var field = condition.Field == "output" ? "" : condition.Field;
            object left = field == ""
                ? (fromOutput.TryGetValue("output", out var o) ? o : fromOutput.Values.FirstOrDefault())
                : (fromOutput.TryGetValue(field, out var v) ? v : null);
            var result = condition.Op switch
            {
                BranchOp.Truthy => IsTruthy(left),
                BranchOp.Equals => EqualsValue(left, condition.Value),
                BranchOp.NotEquals => !EqualsValue(left, condition.Value),
                BranchOp.Gt => CompareNumeric(left, condition.Value!) > 0,
                BranchOp.Gte => CompareNumeric(left, condition.Value!) >= 0,
                BranchOp.Lt => CompareNumeric(left, condition.Value!) < 0,
                BranchOp.Lte => CompareNumeric(left, condition.Value!) <= 0,
                _ => false
            };
return result;
        }

        private static bool IsTruthy(object? val) => val switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            double d => d != 0,
            float f => f != 0,
            int i => i != 0,
            long l => l != 0,
            _ => true
        };

        private static bool EqualsValue(object? left, object? right) =>
            Convert.ToString(left) == Convert.ToString(right);

        private static double CompareNumeric(object? left, object? right)
        {
            var l = Convert.ToDouble(left ?? 0);
            var r = Convert.ToDouble(right ?? 0);
            return l - r;
        }

        private static List<WorkflowEdge> LinearBranchEdges(BranchConfig branch)
        {
            var edges = new List<WorkflowEdge>();
            if (!string.IsNullOrEmpty(branch.From) && branch.Then.Count > 0)
                edges.Add(new WorkflowEdge { From = branch.From, To = branch.Then[0].NodeId });
            for (var i = 0; i < branch.Then.Count - 1; i++)
                edges.Add(new WorkflowEdge { From = branch.Then[i].NodeId, To = branch.Then[i + 1].NodeId });
            return edges;
        }
    }

    internal static class StringExtensions
    {
        public static string Truncate(this string s, int maxBytes)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? string.Empty);
            return bytes.Length <= maxBytes ? s ?? string.Empty : System.Text.Encoding.UTF8.GetString(bytes, 0, maxBytes) + "...";
        }
    }
}
