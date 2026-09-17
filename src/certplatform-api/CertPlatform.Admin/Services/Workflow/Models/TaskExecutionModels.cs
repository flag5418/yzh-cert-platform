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
    /// 路径执行结果（对外 JSON 契约，camelCase 序列化）
    /// </summary>
    public class TaskPathResult
    {
        public int pathIndex { get; set; }
        public string status { get; set; } = "pending";
        public string? failedAtNodeId { get; set; }
        public string? error { get; set; }
        public Dictionary<string, object> output { get; set; } = new();
        public List<string> nodeIds { get; set; } = new();
    }

    /// <summary>
    /// 任务执行响应 — 与旧后端 JSON 形状完全一致（前端零适配）
    /// <para>属性用 camelCase 命名 + JsonPropertyName 保障，序列化不受全局 PascalCase 策略影响</para>
    /// </summary>
    public class TaskExecutionResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("taskCode")]
        public string TaskCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("itemCode")]
        public string ItemCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ncResult")]
        public Dictionary<string, object> NcResult { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("pathResults")]
        public List<TaskPathResult> PathResults { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("durationMs")]
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
    }

    /// <summary>
    /// 单节点测试响应 data（camelCase JSON）
    /// </summary>
    public class NodeTestResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string? Error { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("output")]
        public Dictionary<string, object> Output { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("durationMs")]
        public int DurationMs { get; set; }
    }
}
