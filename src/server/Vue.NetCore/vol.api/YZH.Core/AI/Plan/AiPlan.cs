using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YZH.Core.AI.Plan
{
    public class AiPlan
    {
        [JsonPropertyName("plan_name")]
        public string PlanName { get; set; } = string.Empty;
        [JsonPropertyName("steps")]
        public List<AiStep> Steps { get; set; } = new();
        [JsonPropertyName("output_mapping")]
        public Dictionary<string, string>? OutputMapping { get; set; }
    }
}
