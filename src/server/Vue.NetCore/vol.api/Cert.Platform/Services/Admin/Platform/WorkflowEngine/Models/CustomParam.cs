using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cert.Platform.Services.Admin.Platform.WorkflowEngine.Models
{
    /// <summary>
    /// AI 节点异常 — 携带错误码用于前端分类提示
    /// </summary>
    public class AiNodeException : Exception
    {
        /// <summary>错误码：LLM_TIMEOUT / LLM_CALL_FAILED / SKILL_FAILED / PARAM_RESOLVE_FAILED</summary>
        public string ErrorCode { get; }

        public AiNodeException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public AiNodeException(string errorCode, string message, Exception inner) : base(message, inner)
        {
            ErrorCode = errorCode;
        }
    }
    /// <summary>
    /// AI 节点自定义参数 — 声明式变量定义
    /// <para>文档：AI节点-详细设计-V1 §2</para>
    /// <para>用户在前端通过可视化面板定义，后端编译为 inputs/inputTypes 后执行</para>
    /// </summary>
    public class CustomParam
    {
        /// <summary>参数名（提示词中引用标识，同一 AI 节点内唯一）</summary>
        [JsonPropertyName("paramName")]
        public string ParamName { get; set; } = string.Empty;

        /// <summary>参数类型：string / number / boolean / date / json</summary>
        [JsonPropertyName("paramType")]
        public string ParamType { get; set; } = "string";

        /// <summary>来源类型：link（节点结果）/ skill（方法调用）/ constant（常量）</summary>
        [JsonPropertyName("sourceType")]
        public string SourceType { get; set; } = "constant";

        /// <summary>来源配置（按 sourceType 结构不同）</summary>
        [JsonPropertyName("sourceConfig")]
        public Dictionary<string, object> SourceConfig { get; set; } = new();
    }

    /// <summary>
    /// AI 节点测试请求
    /// <para>文档：AI节点-详细设计-V1 §7</para>
    /// </summary>
    public class AiNodeTestRequest
    {
        /// <summary>节点 ID</summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>节点类型（应为 "ai_node"）</summary>
        public string NodeType { get; set; } = "ai_node";

        /// <summary>节点标题</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>节点配置（promptTemplate / outputType / model / temperature 等）</summary>
        public Dictionary<string, object> Config { get; set; } = new();

        /// <summary>输入端口声明</summary>
        public List<PortConfig> InputPorts { get; set; } = new();

        /// <summary>输出端口声明</summary>
        public List<PortConfig> OutputPorts { get; set; } = new();

        /// <summary>输入参数绑定（portName → 值）</summary>
        public Dictionary<string, string> Inputs { get; set; } = new();

        /// <summary>输入参数类型（portName → "link" | "constant" | "skill"）</summary>
        public Dictionary<string, string> InputTypes { get; set; } = new();

        /// <summary>自定义参数列表</summary>
        public List<CustomParam> CustomParams { get; set; } = new();

        /// <summary>工作流上下文</summary>
        public AiNodeWorkflowContext WorkflowContext { get; set; } = new();
    }

    /// <summary>
    /// AI 节点测试用 — 工作流上下文
    /// </summary>
    public class AiNodeWorkflowContext
    {
        /// <summary>完整的 rule_json（含所有节点配置）</summary>
        public string RuleJson { get; set; } = string.Empty;

        /// <summary>运行时上下文参数（enterpriseCode / phaseCode / standardCode / fileCode）</summary>
        public Dictionary<string, object> ContextParams { get; set; } = new();

        /// <summary>模拟的上游节点输出（nodeId → output）</summary>
        public Dictionary<string, Dictionary<string, object>> MockOutputs { get; set; } = new();
    }

    /// <summary>
    /// AI 节点测试响应（含 debug 信息）
    /// </summary>
    public class AiNodeTestResponse
    {
        /// <summary>执行是否成功</summary>
        public bool Success { get; set; }

        /// <summary>失败原因</summary>
        public string Error { get; set; }

        /// <summary>最终结果（按 outputType 转换后）</summary>
        public object Result { get; set; }

        /// <summary>执行耗时(ms)</summary>
        public int DurationMs { get; set; }

        /// <summary>Debug 信息</summary>
        public AiNodeDebugInfo Debug { get; set; } = new();
    }

    /// <summary>
    /// AI 节点 Debug 信息
    /// </summary>
    public class AiNodeDebugInfo
    {
        /// <summary>替换后的真实提示词</summary>
        public string RenderedPrompt { get; set; } = string.Empty;

        /// <summary>参数池（参数名 → 值）</summary>
        public Dictionary<string, object> ParamPool { get; set; } = new();

        /// <summary>LLM 原始返回</summary>
        public string LlmResponse { get; set; } = string.Empty;

        /// <summary>转换后的结果</summary>
        public object ConvertedResult { get; set; }
    }
}
