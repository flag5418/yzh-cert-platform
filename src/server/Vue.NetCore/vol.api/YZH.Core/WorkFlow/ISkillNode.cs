using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    public interface ISkillNode
    {
        string SkillCode { get; }
        Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default);
    }
}
