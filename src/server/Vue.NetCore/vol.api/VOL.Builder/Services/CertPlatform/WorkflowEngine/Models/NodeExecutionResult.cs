using System.Collections.Generic;

namespace VOL.Builder.Services.CertPlatform.WorkflowEngine.Models
{
    /// <summary>
    /// 节点执行结果模型 — 单个节点执行后的标准输出
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §十（V2 §8.3）</para>
    /// <para>统一格式：{ success, error, result, durationMs }</para>
    /// </summary>
    public class NodeExecutionResult
    {
        /// <summary>该节点是否成功执行</summary>
        public bool Success { get; set; } = true;

        /// <summary>失败原因（成功时为 null）</summary>
        public string Error { get; set; }

        /// <summary>节点输出（所有输出端口的值，key=端口名，value=输出值）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>是否复用了历史结果</summary>
        public bool IsReused { get; set; }

        /// <summary>快速创建成功结果</summary>
        public static NodeExecutionResult Ok(Dictionary<string, object> output, int durationMs = 0)
            => new() { Success = true, Output = output ?? new(), DurationMs = durationMs };

    /// <summary>快速创建失败结果 </summary>
    public static NodeExecutionResult Fail(string error, int durationMs = 0)
        => new() { Success = false, Error = error, DurationMs = durationMs };

    /// <summary>快速创建失败结果（带输出字典，可携带 errorCode 等结构化信息）</summary>
    public static NodeExecutionResult Fail(string error, int durationMs, Dictionary<string, object> output)
        => new() { Success = false, Error = error, DurationMs = durationMs, Output = output ?? new() };
}

    /// <summary>
    /// 路径执行结果 — 一条路径（start → ... → end）执行完成后的状态
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §4.2 §10.5</para>
    /// </summary>
    public class PathResult
    {
        /// <summary>路径索引（从 0 开始）</summary>
        public int PathIndex { get; set; }

        /// <summary>路径状态：completed / failed</summary>
        public string Status { get; set; } = "pending";

        /// <summary>失败时，失败的节点 ID</summary>
        public string FailedAtNodeId { get; set; }

        /// <summary>失败原因</summary>
        public string Error { get; set; }

        /// <summary>路径最终输出（end 节点的输出）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>路径中所有节点的 ID 列表（按执行顺序）</summary>
        public List<string> NodeIds { get; set; } = new();
    }

    /// <summary>
    /// Item 执行结果 — 一个 NC 检查项所有路径执行完成后的汇总
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §4.2 §10.5</para>
    /// </summary>
    public class ItemExecutionResult
    {
        /// <summary>是否成功（至少一条路径到达 end 且成功）</summary>
        public bool Success { get; set; }

        /// <summary>失败原因（所有失败路径的原因汇总）</summary>
        public string Error { get; set; }

        /// <summary>业务成功标志（对应 wf_execution_task_item.is_success）</summary>
        public bool IsSuccess { get; set; }

        /// <summary>所有路径的执行结果</summary>
        public List<PathResult> PathResults { get; set; } = new();

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>NC 最终结果（统一格式：success + error + result）</summary>
        public Dictionary<string, object> NcResult { get; set; } = new();
    }

    /// <summary>
    /// 解析后的工作流模型 — WorkflowConfigParser.Parse 的返回值
    /// 包含拓扑分析后的可执行结构
    /// </summary>
    public class ParsedWorkflow
    {
        /// <summary>原始配置</summary>
        public WorkflowConfig Config { get; set; }

        /// <summary>起始节点</summary>
        public WorkflowNodeConfig StartNode { get; set; }

        /// <summary>节点映射：nodeId → 节点配置</summary>
        public Dictionary<string, WorkflowNodeConfig> NodeMap { get; set; } = new();

        /// <summary>邻接表：nodeId → 出边列表</summary>
        public Dictionary<string, List<WorkflowEdgeConfig>> Adjacency { get; set; } = new();

        /// <summary>入边表：nodeId → 入边列表</summary>
        public Dictionary<string, List<WorkflowEdgeConfig>> InEdges { get; set; } = new();

        /// <summary>所有路径（从 start 到 end 的完整路径列表）</summary>
        public List<List<WorkflowNodeConfig>> Paths { get; set; } = new();
    }
}
