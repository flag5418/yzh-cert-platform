using System.Text.Json.Serialization;

namespace YZH.Core.Workflow.Models
{
    public class WorkflowEdge
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;
        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;
    }
}
