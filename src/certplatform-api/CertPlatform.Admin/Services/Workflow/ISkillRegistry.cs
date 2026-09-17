using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// Skill 注册表接口（V2 静态方法反射版）
    /// <para>移植自：旧 YZH.Core.Workflow.ISkillRegistry.cs（零改动，仅 namespace 调整）</para>
    /// </summary>
    public interface ISkillRegistry
    {
        Task<SkillMetadata?> LoadAsync(string skillCode, CancellationToken ct = default);
        Task<SkillResult> ExecuteAsync(string skillCode, SkillContext context, CancellationToken ct = default);
    }
}
