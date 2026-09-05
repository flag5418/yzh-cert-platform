using System.Collections.Generic;

namespace YZH.Core.Workflow
{
    public class WorkflowRunResult
    {
        public bool Success { get; set; }
        public Dictionary<string, IDictionary<string, object>> NodeOutputs { get; set; } = new();
        public string? FailedNodeId { get; set; }
        public string? Error { get; set; }
        public long DurationMs { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public string? Model { get; set; }
        public string? Provider { get; set; }
    }
}
