using System.Text.Json.Serialization;

namespace VOL.Builder.Services.CertPlatform.WorkflowEngine.Models
{
    /// <summary>
    /// 单条边的数据模型 — 对应 rule_json 中 edges[] 的一个元素
    /// <para>文档：工作流执行引擎-数据模型与接口设计-V3 §三</para>
    /// </summary>
    public class WorkflowEdgeConfig
    {
        /// <summary>源节点 ID</summary>
        public string Source { get; set; }

        /// <summary>目标节点 ID</summary>
        public string Target { get; set; }

        /// <summary>源输出端口。branch 节点区分 success / failure，其他节点为 null</summary>
        [JsonPropertyName("sourceHandle")]
        public string SourceHandle { get; set; }

        /// <summary>目标输入端口名（如 condition、result），无特定端口时为 null</summary>
        [JsonPropertyName("targetHandle")]
        public string TargetHandle { get; set; }
    }
}
