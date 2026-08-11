using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Workflow
{
    public interface IWorkflowEngine
    {
        Task<WorkflowRunResult> RunAsync(string workflowConfigJson, WorkflowContext context, CancellationToken ct = default);
    }
}
