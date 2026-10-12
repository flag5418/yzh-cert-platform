using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Workflow.Models;

namespace CertPlatform.Shared.Services.Workflow
{
    /// <summary>
    /// ★ 工作流执行器（Shared 契约）。
    /// <para>实现在 <c>CertPlatform.Admin.Services.Workflow.WfExecutionTaskService</c>。
    /// 专家端要跑 NC 检查 / 报告章节（<c>ExpertItemExecutors</c>）⇒ 必须经本接口，
    /// ⛔ 不能直接 <c>GetRequiredService&lt;WfExecutionTaskService&gt;()</c>（那是 A-R1 违规）。</para>
    /// </summary>
    public interface IWorkflowRunner
    {
        /// <summary>创建并执行一次工作流（TestScope=FULL）</summary>
        Task<TaskExecutionResponse> CreateAndRunAsync(TaskExecutionRequest request, CancellationToken ct = default);
    }
}
