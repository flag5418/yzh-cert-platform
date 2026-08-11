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
    }
}
