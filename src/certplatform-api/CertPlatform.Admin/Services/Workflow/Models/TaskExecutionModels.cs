using System;
using System.Collections.Generic;

namespace CertPlatform.Admin.Services.Workflow.Models
{
    /// <summary>
    /// 任务执行请求 — 工作流执行 API 契约（前端 designer.vue test/run 现有零改动）
    /// <para>移植自：旧 WfExecutionTaskService.cs 内 TaskExecutionRequest</para>
    /// </summary>
    public class TaskExecutionRequest
    {
        /// <summary>任务类型：TEST | NC_CHECK | REPORT_GENERATE</summary>
        public string TaskType { get; set; } = "TEST";

        /// <summary>
        /// 测试范围：FULL | NODE | AI_NODE（仅 TaskType=TEST 时有效）
        /// <para>由各测试入口显式赋值，最终落到 wf_execution_task.TestScope。
        /// 不传时按 FULL 处理（兼容存量前端调用）。</para>
        /// </summary>
        public string? TestScope { get; set; }

        /// <summary>规则编码</summary>
        public string? RuleCode { get; set; }

        /// <summary>企业编码</summary>
        public string? EnterpriseCode { get; set; }

        /// <summary>标准编码</summary>
        public string? StandardCode { get; set; }

        /// <summary>阶段编码</summary>
        public string? PhaseCode { get; set; }

        /// <summary>工作流配置 JSON（rule_json）</summary>
        public string? ConfigJson { get; set; }
    }

    /// <summary>
    /// 路径执行结果 — 对外 JSON 契约
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。
    /// 全局序列化策略为 PropertyNamingPolicy = null，故属性名即 JSON 名，无需映射注解。</para>
    /// </summary>
    public class TaskPathResult
    {
        public int PathIndex { get; set; }
        public string Status { get; set; } = "pending";
        public string? FailedAtNodeId { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> Output { get; set; } = new();
        public List<string> NodeIds { get; set; } = new();

        /// <summary>
        /// 路径内每个节点的执行明细（按执行顺序）
        /// <para>2026-09-22 新增：让前端能渲染「节点级子表」，不必为每条路径再发一次查询。
        /// 中间节点的输出/耗时/时序/复用标记都在这里，是「分支为何走这条路」的直接证据。</para>
        /// </summary>
        public List<NodeExecutionRecord> NodeResults { get; set; } = new();

        /// <summary>路径耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>路径开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>路径完成时间</summary>
        public DateTime CompletedAt { get; set; }
    }

    /// <summary>
    /// 任务执行响应 — 对外 JSON 契约
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。</para>
    /// </summary>
    public class TaskExecutionResponse
    {
        public string TaskCode { get; set; } = "";

        public string ItemCode { get; set; } = "";

        public string Status { get; set; } = "";

        public bool IsSuccess { get; set; }

        /// <summary>NC 执行结果（键为运行时动态键，由 Skill 执行器产出，不参与命名规约）</summary>
        public Dictionary<string, object> NcResult { get; set; } = new();

        public List<TaskPathResult> PathResults { get; set; } = new();

        public int DurationMs { get; set; }
    }

    /// <summary>
    /// 单节点测试请求 — 前端 designer.vue test/node 契约
    /// </summary>
    public class NodeTestRequest
    {
        /// <summary>节点 ID</summary>
        public string? NodeId { get; set; }

        /// <summary>节点类型：skill / ai_node / docField / docTable / branch</summary>
        public string? NodeType { get; set; }

        /// <summary>节点标题</summary>
        public string? Title { get; set; }

        /// <summary>Skill 编码（功能节点必填）</summary>
        public string? SkillCode { get; set; }

        /// <summary>节点配置</summary>
        public Dictionary<string, object>? Config { get; set; }

        /// <summary>输入参数（portName → value）</summary>
        public Dictionary<string, string>? Inputs { get; set; }

        /// <summary>输入类型（portName → "link" | "constant"）</summary>
        public Dictionary<string, string>? InputTypes { get; set; }

        /// <summary>输入端口声明</summary>
        public List<PortConfig>? InputPorts { get; set; }

        /// <summary>输出端口声明</summary>
        public List<PortConfig>? OutputPorts { get; set; }

        // ──── 测试上下文元数据（2026-09-22 阶段二新增，全部可选）────
        // 用途：单节点测试改为走 WfExecutionTaskService 落库后，需要这些字段填充
        //      wf_execution_task / wf_node_execution 的归属信息，否则测试任务无法回溯。
        // 兼容性：均为可选，前端不发也能正常执行（落库时为空串）。

        /// <summary>规则编码（cert_validation_rule.Code），用于落库归属</summary>
        public string? RuleCode { get; set; }

        /// <summary>企业编码（运行时绑定，默认 YZH-STD-ENT 测试企业）</summary>
        public string? EnterpriseCode { get; set; }

        /// <summary>标准编码</summary>
        public string? StandardCode { get; set; }

        /// <summary>审核阶段编码</summary>
        public string? PhaseCode { get; set; }
    }

    /// <summary>
    /// 单节点测试响应 data
    /// <para>字段命名遵循 YZH 命名铁律：C# 属性名 = JSON 字段名 = TS 字段名（PascalCase）。</para>
    /// </summary>
    public class NodeTestResponse
    {
        /// <summary>
        /// 任务编码 —— 本次节点测试在 wf_execution_task 里的 Code
        /// <para>2026-09-22 阶段二新增：节点测试改为落库后，前端可凭此查
        /// wf_execution_task / wf_node_execution 回溯本次测试（阶段四「测试历史」入口依赖它）。</para>
        /// </summary>
        public string TaskCode { get; set; } = "";

        public bool Success { get; set; }

        public string? Error { get; set; }

        public Dictionary<string, object> Output { get; set; } = new();

        public int DurationMs { get; set; }
    }
}
