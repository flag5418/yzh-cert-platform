using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CertPlatform.Admin.Services.Workflow.Models
{
    /// <summary>
    /// 工作流配置顶层模型 — 对应 rule_json 反序列化后的完整结构
    /// <para>移植自：旧 Cert.Platform .../WorkflowEngine/Models/WorkflowConfig.cs（结构零改动）</para>
    /// <para>三类信息：nodes[]（节点配置）+ edges[]（拓扑关系）+ layout（画布布局，引擎不读）</para>
    /// </summary>
    public class WorkflowConfig
    {
        /// <summary>配置版本号</summary>
        public int Version { get; set; }

        /// <summary>工作流类型：validation | report</summary>
        public string WorkflowType { get; set; } = "validation";

        /// <summary>节点列表（执行引擎读取）</summary>
        public List<WorkflowNodeConfig> Nodes { get; set; } = new();

        /// <summary>边列表 / 拓扑关系（执行引擎读取）</summary>
        public List<WorkflowEdgeConfig> Edges { get; set; } = new();

        /// <summary>输出配置（可选）</summary>
        [JsonPropertyName("outputConfig")]
        public Dictionary<string, object>? OutputConfig { get; set; }

        /// <summary>术语表（可选）</summary>
        public string? Glossary { get; set; }
    }
}
