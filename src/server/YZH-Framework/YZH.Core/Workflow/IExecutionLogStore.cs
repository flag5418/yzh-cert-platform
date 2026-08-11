using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Workflow
{
    public interface IExecutionLogStore
    {
        Task WriteAsync(ExecutionLogEntry entry, CancellationToken ct = default);
        Task<IReadOnlyList<ExecutionLogEntry>> QueryByInstanceAsync(string workflowCode, CancellationToken ct = default);
    }

    public class ExecutionLogEntry
    {
        public string WorkflowCode { get; set; } = string.Empty;
        public int WorkflowVersion { get; set; } = 1;
        public string BusinessType { get; set; } = "file_upload";
        public string BusinessCode { get; set; } = string.Empty;
        public long BusinessId { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string SkillCode { get; set; } = string.Empty;
        public string? InputDataJson { get; set; }
        public string? OutputDataJson { get; set; }
        public string Status { get; set; } = "pending";
        public string? ErrorMsg { get; set; }
        public long DurationMs { get; set; }
        public System.DateTime StartedAt { get; set; }
        public System.DateTime? CompletedAt { get; set; }
    }
}
