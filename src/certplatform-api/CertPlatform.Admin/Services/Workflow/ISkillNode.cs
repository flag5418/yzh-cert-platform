using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// DI 容器注册式 Skill 节点接口（反射模式之外的回退执行通道）
    /// <para>移植自：旧 YZH.Core.Workflow.ISkillNode.cs（零改动，仅 namespace 调整）</para>
    /// </summary>
    public interface ISkillNode
    {
        string SkillCode { get; }
        Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default);
    }
}
