using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VOL.Builder.Services.CertPlatform.WorkflowEngine.Models
{
    /// <summary>
    /// 单个节点的配置模型 — 对应 rule_json 中 nodes[] 的一个元素
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §二</para>
    /// </summary>
    public class WorkflowNodeConfig
    {
        /// <summary>唯一标识，格式 {classCode}_n{序号}，如 start_n1、docField_n2</summary>
        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; }

        /// <summary>节点类型：start / end / branch / docField / docTable / ai_node / skill</summary>
        [JsonPropertyName("nodeType")]
        public string NodeType { get; set; }

        /// <summary>用户自定义名称（画布显示名），全流程唯一</summary>
        public string Title { get; set; }

        /// <summary>功能节点的 Skill 编码（如 compare、assemble），非功能节点为空</summary>
        [JsonPropertyName("skillCode")]
        public string SkillCode { get; set; }

        /// <summary>节点特有配置（按 nodeType 不同结构不同）</summary>
        public Dictionary<string, object> Config { get; set; } = new();

        /// <summary>输入参数绑定（portName → 值，值格式见 V3 §2.4）</summary>
        public Dictionary<string, string> Inputs { get; set; } = new();

        /// <summary>输入端口声明</summary>
        [JsonPropertyName("inputPorts")]
        public List<PortConfig> InputPorts { get; set; } = new();

        /// <summary>输出端口声明</summary>
        [JsonPropertyName("outputPorts")]
        public List<PortConfig> OutputPorts { get; set; } = new();
    }
}
