using System;
using System.Collections.Generic;

namespace CertPlatform.Admin.Services.Workflow.Models
{
    /// <summary>
    /// 节点执行结果模型 — 单个节点执行后的标准输出
    /// <para>移植自：旧 Models/NodeExecutionResult.cs（含 PathResult/ItemExecutionResult/ParsedWorkflow）</para>
    /// <para>统一格式：{ success, error, result, durationMs }</para>
    /// </summary>
    public class NodeExecutionResult
    {
        /// <summary>该节点是否成功执行</summary>
        public bool Success { get; set; } = true;

        /// <summary>失败原因（成功时为 null）</summary>
        public string? Error { get; set; }

        /// <summary>节点输出（所有输出端口的值，key=端口名，value=输出值）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>是否复用了历史结果</summary>
        public bool IsReused { get; set; }

        /// <summary>开始执行时间（由 NodeExecutor 在真实执行前后写入，用于 wf_node_execution 时序还原）</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>完成时间（与 StartedAt 同源，保证 StartedAt &lt;= CompletedAt）</summary>
        public DateTime CompletedAt { get; set; }

        // ── AI 节点专属元数据（阶段五：单节点执行详情增强）──

        /// <summary>LLM Prompt Tokens（仅 ai_node 有值）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>LLM Completion Tokens（仅 ai_node 有值）</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>纯 LLM API 调用耗时(ms)（不含参数解析、模板渲染、类型转换）</summary>
        public int? LlmDurationMs { get; set; }

        /// <summary>快速创建成功结果</summary>
        public static NodeExecutionResult Ok(Dictionary<string, object> output, int durationMs = 0)
            => new() { Success = true, Output = output ?? new(), DurationMs = durationMs };

        /// <summary>快速创建失败结果</summary>
        public static NodeExecutionResult Fail(string error, int durationMs = 0)
            => new() { Success = false, Error = error, DurationMs = durationMs };

        /// <summary>快速创建失败结果（带输出字典，可携带 errorCode 等结构化信息）</summary>
        public static NodeExecutionResult Fail(string error, int durationMs, Dictionary<string, object> output)
            => new() { Success = false, Error = error, DurationMs = durationMs, Output = output ?? new() };
    }

    /// <summary>
    /// 节点执行明细 — 路径内单个节点的一次执行记录
    /// <para>存在的意义：<see cref="PathResult.NodeIds"/> 只保留节点 ID 列表，
    /// 中间节点的输出/耗时/时序在路径聚合时会被丢弃，导致 wf_node_execution 只能靠路径级状态反推
    /// （所有节点同状态、耗时硬编码 0、只有最后一个节点有输出）。本类型把 per-node 事实原样带出来。</para>
    /// </summary>
    public class NodeExecutionRecord
    {
        /// <summary>节点 ID</summary>
        public string NodeId { get; set; } = "";

        /// <summary>该节点本次是否执行成功</summary>
        public bool Success { get; set; }

        /// <summary>失败原因（成功时为 null）</summary>
        public string? Error { get; set; }

        /// <summary>节点输出（所有输出端口的 JSON；中间节点同样保留）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>该节点耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>是否为跨路径复用（true=本路径未真跑，取自结果池）</summary>
        public bool IsReused { get; set; }

        /// <summary>开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>完成时间</summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>LLM Prompt Tokens（仅 ai_node 有值）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>LLM Completion Tokens（仅 ai_node 有值）</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>纯 LLM API 调用耗时(ms)</summary>
        public int? LlmDurationMs { get; set; }

        /// <summary>由一次节点执行结果构造记录（isReused=true 表示本次未真跑，取自结果池）</summary>
        public static NodeExecutionRecord From(string nodeId, NodeExecutionResult result, bool isReused)
            => new()
            {
                NodeId = nodeId,
                Success = result.Success,
                Error = result.Error,
                Output = result.Output ?? new(),
                DurationMs = result.DurationMs,
                IsReused = isReused,
                StartedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                PromptTokens = result.PromptTokens,
                CompletionTokens = result.CompletionTokens,
                LlmDurationMs = result.LlmDurationMs
            };
    }

    /// <summary>
    /// 路径执行结果 — 一条路径（start → ... → end）执行完成后的状态
    /// </summary>
    public class PathResult
    {
        /// <summary>路径索引（从 0 开始）</summary>
        public int PathIndex { get; set; }

        /// <summary>路径状态：completed / failed</summary>
        public string Status { get; set; } = "pending";

        /// <summary>失败时，失败的节点 ID</summary>
        public string? FailedAtNodeId { get; set; }

        /// <summary>失败原因</summary>
        public string? Error { get; set; }

        /// <summary>路径最终输出（end 节点的输出）</summary>
        public Dictionary<string, object> Output { get; set; } = new();

        /// <summary>路径中所有节点的 ID 列表（按执行顺序）</summary>
        public List<string> NodeIds { get; set; } = new();

        /// <summary>
        /// 路径内每个节点的执行明细（按执行顺序）
        /// <para>与 <see cref="NodeIds"/> 的区别：这里携带 per-node 的真实状态/输出/耗时/时序/复用标记。
        /// 落库时以本列表为准，不再用路径级 Status 反推节点状态。</para>
        /// </summary>
        public List<NodeExecutionRecord> NodeResults { get; set; } = new();

        /// <summary>路径耗时(ms)——为 wf_path_execution 表（阶段三）预留，当前仅内存/响应可用</summary>
        public int DurationMs { get; set; }

        /// <summary>路径开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>路径完成时间</summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// Item 执行结果 — 一个 NC 检查项所有路径执行完成后的汇总
    /// </summary>
    public class ItemExecutionResult
    {
        /// <summary>是否成功（至少一条路径到达 end 且成功）</summary>
        public bool Success { get; set; }

        /// <summary>失败原因（所有失败路径的原因汇总）</summary>
        public string? Error { get; set; }

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
        public WorkflowConfig Config { get; set; } = new();

        /// <summary>起始节点</summary>
        public WorkflowNodeConfig StartNode { get; set; } = new();

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
