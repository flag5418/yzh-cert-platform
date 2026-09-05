using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YZH.Core.Workflow.Models
{
    public class BranchConfig
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;
        [JsonPropertyName("condition")]
        public BranchCondition? Condition { get; set; }
        [JsonPropertyName("then")]
        public List<WorkflowNode> Then { get; set; } = new();
    }
}
