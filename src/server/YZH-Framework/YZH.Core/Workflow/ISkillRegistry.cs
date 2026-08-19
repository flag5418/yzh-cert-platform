using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// Skill 注册表接口（V2 静态方法版）。
    /// </summary>
    public interface ISkillRegistry
    {
        Task<SkillMetadata?> LoadAsync(string skillCode, CancellationToken ct = default);
        Task<SkillResult> ExecuteAsync(string skillCode, SkillContext context, CancellationToken ct = default);
    }
}
