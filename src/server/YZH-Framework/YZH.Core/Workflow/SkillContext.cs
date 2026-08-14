using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    public class SkillContext
    {
        public IDictionary<string, object> Inputs { get; set; } = new Dictionary<string, object>();
        public string WorkflowInstanceId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public ILogger? Logger { get; set; }
    }

    public class SkillResult
    {
        public bool Success { get; set; }
        public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
        public double? Confidence { get; set; }
        public string? Error { get; set; }
        public long DurationMs { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
    }
}
