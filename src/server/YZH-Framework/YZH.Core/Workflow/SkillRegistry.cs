using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    public class SkillRegistry : ISkillRegistry
    {
        private readonly ConcurrentDictionary<string, ISkillNode> _skills = new();
        private readonly ILogger<SkillRegistry> _logger;

        public SkillRegistry(ILogger<SkillRegistry> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// DI 构造：自动注册容器中已注册的全部 ISkillNode（llm_extract / document_extract / get_field 等），
        /// 避免 SkillRegistry 为空导致 WorkflowEngine 报“未知 Skill 编码”。
        /// </summary>
        public SkillRegistry(IEnumerable<ISkillNode> skills, ILogger<SkillRegistry> logger)
        {
            _logger = logger;
            foreach (var skill in skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.SkillCode)) continue;
                _skills[skill.SkillCode] = skill;
                _logger.LogInformation("Skill 已注册: {SkillCode}", skill.SkillCode);
            }
        }

        public ISkillNode? Get(string skillCode) =>
            _skills.TryGetValue(skillCode, out var skill) ? skill : null;

        public Task RegisterAsync(ISkillNode skill, CancellationToken ct = default)
        {
            _skills[skill.SkillCode] = skill;
            _logger.LogInformation("Skill 已注册: {SkillCode}", skill.SkillCode);
            return Task.CompletedTask;
        }

        public Task UnregisterAsync(string skillCode, CancellationToken ct = default)
        {
            _skills.TryRemove(skillCode, out _);
            _logger.LogInformation("Skill 已注销: {SkillCode}", skillCode);
            return Task.CompletedTask;
        }

        public IReadOnlyCollection<string> AllCodes() => _skills.Keys.ToList();
    }
}
