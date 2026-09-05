using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YZH.Core.Workflow.Models
{
    public class WorkflowNode
    {
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;
        [JsonPropertyName("skill_code")]
        public string SkillCode { get; set; } = string.Empty;
        [JsonPropertyName("inputs")]
        public Dictionary<string, object> Inputs { get; set; } = new();
        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }
}
