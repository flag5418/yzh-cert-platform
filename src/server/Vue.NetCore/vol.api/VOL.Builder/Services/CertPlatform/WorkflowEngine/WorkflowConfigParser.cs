using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.Builder.Services.CertPlatform.WorkflowEngine
{
    /// <summary>
    /// 工作流配置解析器 — 将 rule_json 字符串解析为可执行的工作流模型
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §十（后端执行引擎解析规则）</para>
    /// <para>职责：</para>
    /// <para>1. JSON 反序列化 → WorkflowConfig（nodes + edges）</para>
    /// <para>2. 构建邻接表（nodeId → 出边列表）</para>
    /// <para>3. 构建入边表（nodeId → 入边列表）</para>
    /// <para>4. 找到 start 节点</para>
    /// <para>5. 枚举所有路径（DFS，branch 按 success/failure 分叉）</para>
    /// <para>6. 拓扑校验（无环、可达、branch 必须有两条出边）</para>
    /// </summary>
    public class WorkflowConfigParser
    {
        private readonly ILogger<WorkflowConfigParser> _logger;

        // JSON 反序列化选项：camelCase，不区分大小写
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public WorkflowConfigParser(ILogger<WorkflowConfigParser> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 解析 rule_json 字符串 → 可执行的工作流模型
        /// </summary>
        /// <param name="ruleJson">rule_json 字符串</param>
        /// <returns>解析后的 ParsedWorkflow（含拓扑分析和路径枚举结果）</returns>
        public ParsedWorkflow Parse(string ruleJson)
        {
            if (string.IsNullOrWhiteSpace(ruleJson))
                throw new ArgumentException("rule_json 不能为空", nameof(ruleJson));

            _logger.LogInformation("[WorkflowConfigParser] 开始解析 rule_json, 长度={Length}", ruleJson.Length);

            // 1. JSON 反序列化
            var config = JsonSerializer.Deserialize<WorkflowConfig>(ruleJson, JsonOptions);
            if (config == null || config.Nodes == null || config.Nodes.Count == 0)
                throw new InvalidOperationException("rule_json 解析失败：nodes 为空");

            _logger.LogInformation("[WorkflowConfigParser] 反序列化完成: {NodeCount} 个节点, {EdgeCount} 条边",
                config.Nodes.Count, config.Edges?.Count ?? 0);

            // 2. 构建节点映射
            var nodeMap = config.Nodes.ToDictionary(n => n.NodeId, n => n);

            // 3. 构建邻接表 + 入边表
            var adjacency = new Dictionary<string, List<WorkflowEdgeConfig>>();
            var inEdges = new Dictionary<string, List<WorkflowEdgeConfig>>();

            foreach (var node in config.Nodes)
            {
                adjacency[node.NodeId] = new List<WorkflowEdgeConfig>();
                inEdges[node.NodeId] = new List<WorkflowEdgeConfig>();
            }

            if (config.Edges != null)
            {
                foreach (var edge in config.Edges)
                {
                    if (!adjacency.ContainsKey(edge.Source))
                        throw new InvalidOperationException($"边源节点不存在: {edge.Source}");
                    if (!inEdges.ContainsKey(edge.Target))
                        throw new InvalidOperationException($"边目标节点不存在: {edge.Target}");

                    adjacency[edge.Source].Add(edge);
                    inEdges[edge.Target].Add(edge);
                }
            }

            // 4. 拓扑校验
            ValidateTopology(config, nodeMap, adjacency, inEdges);

            // 5. 找到 start 节点
            var startNode = config.Nodes.FirstOrDefault(n =>
                string.Equals(n.NodeType, "start", StringComparison.OrdinalIgnoreCase));

            if (startNode == null)
                throw new InvalidOperationException("工作流缺少 start 节点");

            _logger.LogInformation("[WorkflowConfigParser] Start 节点: {NodeId}", startNode.NodeId);

            // 6. 枚举所有路径（DFS）
            var paths = EnumeratePaths(startNode, adjacency, nodeMap);

            _logger.LogInformation("[WorkflowConfigParser] 路径枚举完成: {PathCount} 条路径", paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                var pathNodeIds = string.Join(" → ", paths[i].Select(n => n.NodeId));
                _logger.LogInformation("[WorkflowConfigParser] 路径 {Index}: {Path}", i, pathNodeIds);
            }

            return new ParsedWorkflow
            {
                Config = config,
                StartNode = startNode,
                NodeMap = nodeMap,
                Adjacency = adjacency,
                InEdges = inEdges,
                Paths = paths
            };
        }

        // ── 拓扑校验 ──

        /// <summary>
        /// 拓扑校验：无环、可达、branch 必须有两条出边
        /// </summary>
        private void ValidateTopology(
            WorkflowConfig config,
            Dictionary<string, WorkflowNodeConfig> nodeMap,
            Dictionary<string, List<WorkflowEdgeConfig>> adjacency,
            Dictionary<string, List<WorkflowEdgeConfig>> inEdges)
        {
            // 校验 1：每个节点至少有一条入边（start 除外）
            foreach (var node in config.Nodes)
            {
                if (string.Equals(node.NodeType, "start", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (inEdges[node.NodeId].Count == 0)
                    throw new InvalidOperationException($"节点 {node.NodeId} 没有入边（除 start 外所有节点必须有入边）");
            }

            // 校验 2：每个节点至少有一条出边（end 除外）
            foreach (var node in config.Nodes)
            {
                if (string.Equals(node.NodeType, "end", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (adjacency[node.NodeId].Count == 0)
                    throw new InvalidOperationException($"节点 {node.NodeId} 没有出边（除 end 外所有节点必须有出边）");
            }

            // 校验 3：branch 节点必须有 2 条出边：一条 success，一条 failure
            foreach (var node in config.Nodes)
            {
                if (!string.Equals(node.NodeType, "branch", StringComparison.OrdinalIgnoreCase))
                    continue;

                var outEdges = adjacency[node.NodeId];
                var successEdge = outEdges.FirstOrDefault(e =>
                    string.Equals(e.SourceHandle, "success", StringComparison.OrdinalIgnoreCase));
                var failureEdge = outEdges.FirstOrDefault(e =>
                    string.Equals(e.SourceHandle, "failure", StringComparison.OrdinalIgnoreCase));

                if (successEdge == null || failureEdge == null)
                    throw new InvalidOperationException(
                        $"branch 节点 {node.NodeId} 必须有 success 和 failure 两条出边");
            }

            // 校验 4：无环（DFS 检测）
            var startNode = config.Nodes.FirstOrDefault(n =>
                string.Equals(n.NodeType, "start", StringComparison.OrdinalIgnoreCase));
            if (startNode != null)
            {
                var visited = new HashSet<string>();
                var recursionStack = new HashSet<string>();
                DetectCycle(startNode.NodeId, adjacency, visited, recursionStack);
            }

            _logger.LogInformation("[WorkflowConfigParser] 拓扑校验通过");
        }

        /// <summary>
        /// DFS 检测环
        /// </summary>
        private void DetectCycle(
            string nodeId,
            Dictionary<string, List<WorkflowEdgeConfig>> adjacency,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            if (recursionStack.Contains(nodeId))
                throw new InvalidOperationException($"检测到环：节点 {nodeId} 在当前递归路径中重复出现");

            if (visited.Contains(nodeId))
                return;

            visited.Add(nodeId);
            recursionStack.Add(nodeId);

            if (adjacency.TryGetValue(nodeId, out var edges))
            {
                foreach (var edge in edges)
                {
                    DetectCycle(edge.Target, adjacency, visited, recursionStack);
                }
            }

            recursionStack.Remove(nodeId);
        }

        // ── 路径枚举 ──

        /// <summary>
        /// 从 start 节点开始，DFS 枚举所有到 end 的路径
        /// <para>branch 节点按 success/failure 分叉，普通节点走唯一出边</para>
        /// <para>文档：V3 §3.4 路径枚举算法</para>
        /// </summary>
        private List<List<WorkflowNodeConfig>> EnumeratePaths(
            WorkflowNodeConfig startNode,
            Dictionary<string, List<WorkflowEdgeConfig>> adjacency,
            Dictionary<string, WorkflowNodeConfig> nodeMap)
        {
            var paths = new List<List<WorkflowNodeConfig>>();
            var currentPath = new List<WorkflowNodeConfig>();
            var visitedInPath = new HashSet<string>(); // 防止同一节点在同一路径中被重复访问

            Dfs(startNode, currentPath, visitedInPath, paths, adjacency, nodeMap);

            return paths;
        }

        /// <summary>
        /// DFS 递归遍历
        /// </summary>
        private void Dfs(
            WorkflowNodeConfig currentNode,
            List<WorkflowNodeConfig> currentPath,
            HashSet<string> visitedInPath,
            List<List<WorkflowNodeConfig>> paths,
            Dictionary<string, List<WorkflowEdgeConfig>> adjacency,
            Dictionary<string, WorkflowNodeConfig> nodeMap)
        {
            // 防止同一路径中节点重复（环保护）
            if (visitedInPath.Contains(currentNode.NodeId))
                return;

            currentPath.Add(currentNode);
            visitedInPath.Add(currentNode.NodeId);

            // 到达 end 节点 → 路径完成
            if (string.Equals(currentNode.NodeType, "end", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(new List<WorkflowNodeConfig>(currentPath));
                currentPath.RemoveAt(currentPath.Count - 1);
                visitedInPath.Remove(currentNode.NodeId);
                return;
            }

            // 获取出边
            if (!adjacency.TryGetValue(currentNode.NodeId, out var outEdges) || outEdges.Count == 0)
            {
                // 没有出边且不是 end 节点 → 路径不完整（理论上拓扑校验已拦住）
                currentPath.RemoveAt(currentPath.Count - 1);
                visitedInPath.Remove(currentNode.NodeId);
                return;
            }

            if (string.Equals(currentNode.NodeType, "branch", StringComparison.OrdinalIgnoreCase))
            {
                // branch 节点：必须走 success 和 failure 两条分支
                var successEdge = outEdges.FirstOrDefault(e =>
                    string.Equals(e.SourceHandle, "success", StringComparison.OrdinalIgnoreCase));
                var failureEdge = outEdges.FirstOrDefault(e =>
                    string.Equals(e.SourceHandle, "failure", StringComparison.OrdinalIgnoreCase));

                if (successEdge != null && nodeMap.TryGetValue(successEdge.Target, out var successTarget))
                    Dfs(successTarget, currentPath, visitedInPath, paths, adjacency, nodeMap);

                if (failureEdge != null && nodeMap.TryGetValue(failureEdge.Target, out var failureTarget))
                    Dfs(failureTarget, currentPath, visitedInPath, paths, adjacency, nodeMap);
            }
            else
            {
                // 普通节点：走所有出边（通常只有一条）
                foreach (var edge in outEdges)
                {
                    if (nodeMap.TryGetValue(edge.Target, out var targetNode))
                        Dfs(targetNode, currentPath, visitedInPath, paths, adjacency, nodeMap);
                }
            }

            // 回溯
            currentPath.RemoveAt(currentPath.Count - 1);
            visitedInPath.Remove(currentNode.NodeId);
        }
    }
}
