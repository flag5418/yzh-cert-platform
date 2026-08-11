using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    public class SkillRegistry : ISkillRegistry
    {
        private readonly ConcurrentDictionary<string, ISkillNode> _skills = new();
        private readonly ILogger<SkillRegistry> _logger;

        public SkillRegistry(ILogger<SkillRegistry> logger) => _logger = logger;

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
