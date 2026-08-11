using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YZH.Core.Workflow;

namespace YZH.Core.Tests.Workflow
{
    public class S2_SkillRegistryTests
    {
        private static SkillRegistry CreateRegistry() => new(Mock.Of<ILogger<SkillRegistry>>());

        [Fact]
        public void Get_NullSkill_Should_ReturnNull()
        {
            var registry = CreateRegistry();
            Assert.Null(registry.Get("non_existent"));
        }

        [Fact]
        public async Task RegisterAndGet_Should_ReturnSameSkill()
        {
            var registry = CreateRegistry();
            var skill = new TestSkill("test_skill");
            await registry.RegisterAsync(skill);
            var result = registry.Get("test_skill");
            Assert.NotNull(result);
            Assert.Equal("test_skill", result!.SkillCode);
        }

        [Fact]
        public async Task RegisterDuplicate_Should_Override()
        {
            var registry = CreateRegistry();
            await registry.RegisterAsync(new TestSkill("skill_a", "v1"));
            await registry.RegisterAsync(new TestSkill("skill_a", "v2"));
            var result = registry.Get("skill_a");
            Assert.Equal("v2", result!.ExecuteAsync(null!).Result.Outputs.TryGetValue("version", out var v) ? v.ToString() : null);
        }

        [Fact]
        public async Task AllCodes_Should_ReturnRegisteredCodes()
        {
            var registry = CreateRegistry();
            await registry.RegisterAsync(new TestSkill("a"));
            await registry.RegisterAsync(new TestSkill("b"));
            var codes = registry.AllCodes().OrderBy(c => c).ToList();
            Assert.Equal(new[] { "a", "b" }, codes);
        }

        [Fact]
        public async Task Unregister_Should_RemoveSkill()
        {
            var registry = CreateRegistry();
            await registry.RegisterAsync(new TestSkill("to_remove"));
            Assert.NotNull(registry.Get("to_remove"));
            await registry.UnregisterAsync("to_remove");
            Assert.Null(registry.Get("to_remove"));
        }

        [Fact]
        public async Task ConcurrentRegister_Should_BeThreadSafe()
        {
            var registry = CreateRegistry();
            var tasks = Enumerable.Range(1, 100).Select(i =>
                registry.RegisterAsync(new TestSkill($"skill_{i}")));
            await Task.WhenAll(tasks);
            Assert.Equal(100, registry.AllCodes().Count);
        }

        private class TestSkill : ISkillNode
        {
            public string SkillCode { get; }
            public string Version { get; }
            public TestSkill(string skillCode, string version = "v1")
            { SkillCode = skillCode; Version = version; }
            public Task<SkillResult> ExecuteAsync(SkillContext context, System.Threading.CancellationToken ct = default)
                => Task.FromResult(new SkillResult { Success = true, Outputs = new Dictionary<string, object> { ["version"] = Version } });
        }
    }
}
