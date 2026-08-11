using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Workflow
{
    public interface ISkillRegistry
    {
        ISkillNode? Get(string skillCode);
        Task RegisterAsync(ISkillNode skill, CancellationToken ct = default);
        Task UnregisterAsync(string skillCode, CancellationToken ct = default);
        IReadOnlyCollection<string> AllCodes();
    }
}
