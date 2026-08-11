using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YZH.Core.Workflow;

namespace YZH.Core.Skills
{
    /// <summary>
    /// 文本拼接 Skill（报告引擎用）。
    /// SkillCode = "assemble"。
    /// </summary>
    public class AssembleSkill : ISkillNode
    {
        public string SkillCode => "assemble";

        public Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            var parts = context.Inputs.TryGetValue("parts", out var p)
                ? (p as IEnumerable<object>)?.Select(x => x?.ToString() ?? string.Empty).ToList()
                : new List<string>();

            var joiner = context.Inputs.TryGetValue("joiner", out var j) ? j?.ToString() ?? string.Empty : string.Empty;

            var result = string.Join(joiner, parts);

            return Task.FromResult(new SkillResult
            {
                Success = true,
                Outputs = new Dictionary<string, object> { ["assembled_text"] = result }
            });
        }
    }
}
