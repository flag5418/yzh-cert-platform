using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Workflow
{
    public class InMemoryExecutionLogStore : IExecutionLogStore
    {
        private readonly List<ExecutionLogEntry> _entries = new();

        public Task WriteAsync(ExecutionLogEntry entry, CancellationToken ct = default)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExecutionLogEntry>> QueryByInstanceAsync(string workflowCode, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ExecutionLogEntry>>(
                _entries.Where(e => e.WorkflowCode == workflowCode).ToList());
        }

        public IReadOnlyList<ExecutionLogEntry> All => _entries;
    }
}
