using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YZH.Core.AI.Plan
{
    public class AiStep
    {
        [JsonPropertyName("order")]
        public int Order { get; set; }
        [JsonPropertyName("skill_code")]
        public string SkillCode { get; set; } = string.Empty;
        [JsonPropertyName("params")]
        public Dictionary<string, object> Params { get; set; } = new();
    }
}
