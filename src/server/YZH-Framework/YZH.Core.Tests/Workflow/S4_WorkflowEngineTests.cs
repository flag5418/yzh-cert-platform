using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YZH.Core.Workflow;
using YZH.Core.Workflow.Models;

namespace YZH.Core.Tests.Workflow
{
    public class S4_WorkflowEngineTests
    {
        private static (WorkflowEngine engine, InMemoryExecutionLogStore logStore) CreateEngine(params ISkillNode[] skills)
        {
            var registry = new SkillRegistry(Mock.Of<ILogger<SkillRegistry>>());
            foreach (var s in skills) registry.RegisterAsync(s).GetAwaiter().GetResult();
            var logStore = new InMemoryExecutionLogStore();
            var logger = Mock.Of<ILogger<WorkflowEngine>>();
            return (new WorkflowEngine(registry, logger), logStore);
        }

        private class EchoSkill : ISkillNode
        {
            public string SkillCode { get; }
            public object? OutputValue { get; set; }
            public EchoSkill(string code, object? outputValue = null) { SkillCode = code; OutputValue = outputValue; }
            public Task<SkillResult> ExecuteAsync(SkillContext context, System.Threading.CancellationToken ct = default)
            {
                var output = OutputValue ?? (context.Inputs.TryGetValue("value", out var v) ? v.ToString() : "default");
                return Task.FromResult(new SkillResult { Success = true, Outputs = new Dictionary<string, object> { ["output"] = output } });
            }
        }

        private class BoolSkill : ISkillNode
        {
            public string SkillCode => "bool_skill";
            public bool BoolValue { get; set; }
            public Task<SkillResult> ExecuteAsync(SkillContext context, System.Threading.CancellationToken ct = default)
                => Task.FromResult(new SkillResult { Success = true, Outputs = new Dictionary<string, object> { ["output"] = BoolValue } });
        }

        [Fact]
        public async Task RunAsync_LinearPipeline_Should_ExecuteInOrder()
        {
            var (engine, _) = CreateEngine(
                new EchoSkill("n1", "val1"),
                new EchoSkill("n2", "val2"),
                new EchoSkill("n3", "val3"));

            var config = @"{
                ""nodes"": [
                    {""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{""value"":""a""},""output"":""out1""},
                    {""node_id"":""n2"",""skill_code"":""n2"",""inputs"":{""value"":""b""},""output"":""out2""},
                    {""node_id"":""n3"",""skill_code"":""n3"",""inputs"":{""value"":""c""},""output"":""out3""}
                ],
                ""edges"": [{""from"":""n1"",""to"":""n2""},{""from"":""n2"",""to"":""n3""}]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
Assert.Equal("val1", result.NodeOutputs["n1"]["output"]);
Assert.Equal("val2", result.NodeOutputs["n2"]["output"]);
Assert.Equal("val3", result.NodeOutputs["n3"]["output"]);
        }

        [Fact]
        public async Task RunAsync_Branch_True_Should_ExecuteThenNodes()
        {
            var boolSkill = new BoolSkill { BoolValue = true };
            var (engine, _) = CreateEngine(
                new EchoSkill("n1", "start"),
                boolSkill,
                new EchoSkill("n_then", "then_result"));

            var config = @"{
                ""nodes"": [
                    {""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""out1""},
                    {""node_id"":""n2"",""skill_code"":""bool_skill"",""inputs"":{},""output"":""flag""}
                ],
                ""edges"": [{""from"":""n1"",""to"":""n2""}],
                ""branches"": [{
                    ""from"":""n2"",
                    ""condition"":{""field"":""output"",""op"":""truthy""},
                    ""then"":[{""node_id"":""n3"",""skill_code"":""n_then"",""inputs"":{},""output"":""result""}]
                }]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
            Assert.Equal("then_result", result.NodeOutputs["n3"]["output"]);
        }

        [Fact]
        public async Task RunAsync_Branch_False_Should_SkipThenNodes()
        {
            var boolSkill = new BoolSkill { BoolValue = false };
            var (engine, _) = CreateEngine(
                new EchoSkill("n1", "start"),
                boolSkill,
                new EchoSkill("n_then", "then_result"));

            var config = @"{
                ""nodes"": [
                    {""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""out1""},
                    {""node_id"":""n2"",""skill_code"":""bool_skill"",""inputs"":{},""output"":""flag""}
                ],
                ""edges"": [{""from"":""n1"",""to"":""n2""}],
                ""branches"": [{
                    ""from"":""n2"",
                    ""condition"":{""field"":""output"",""op"":""truthy""},
                    ""then"":[{""node_id"":""n3"",""skill_code"":""n_then"",""inputs"":{},""output"":""result""}]
                }]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
            Assert.DoesNotContain("n3", result.NodeOutputs.Keys);
        }

        [Fact]
        public async Task RunAsync_Cycle_Should_Throw()
        {
            var (engine, _) = CreateEngine(new EchoSkill("n1", "v1"));
            var config = @"{
                ""nodes"": [{""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""o""}],
                ""edges"": [{""from"":""n1"",""to"":""n1""}]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var ex = await Assert.ThrowsAnyAsync<System.Exception>(() => engine.RunAsync(config, ctx));
            Assert.IsType<WorkflowExecutionException>(ex);
        }

        [Fact]
        public async Task RunAsync_UnknownSkill_Should_Throw()
        {
            var (engine, _) = CreateEngine(new EchoSkill("n1", "v1"));
            var config = @"{
                ""nodes"": [{""node_id"":""n1"",""skill_code"":""unknown_skill"",""inputs"":{},""output"":""o""}],
                ""edges"": []
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var ex = await Assert.ThrowsAnyAsync<System.Exception>(() => engine.RunAsync(config, ctx));
            Assert.IsType<UnknownSkillException>(ex);
        }

        [Fact]
        public async Task RunAsync_WithLogStore_Should_WriteExecutionLogs()
        {
            var (engine, logStore) = CreateEngine(new EchoSkill("n1", "v1"));
            var ctx = new WorkflowContext
            {
                WorkflowInstanceId = "wf_log_test",
                BusinessType = "test",
                BusinessCode = "bc001",
                LogStore = logStore
            };
            var config = @"{
                ""nodes"": [{""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""o""}],
                ""edges"": []
            }";
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
            var logs = await logStore.QueryByInstanceAsync("wf_log_test");
            Assert.Single(logs);
            Assert.Equal("success", logs[0].Status);
            Assert.Equal("n1", logs[0].NodeId);
        }

        [Fact]
        public async Task RunAsync_TemplateResolution_Should_Work()
        {
            var (engine, _) = CreateEngine(
                new EchoSkill("n1", "hello"),
                new EchoSkill("n2"));
            var config = @"{
                ""nodes"": [
                    {""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""greeting""},
                    {""node_id"":""n2"",""skill_code"":""n2"",""inputs"":{""value"":""prefix_{{n1.output}}""},""output"":""result""}
                ],
                ""edges"": [{""from"":""n1"",""to"":""n2""}]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
            Assert.Equal("prefix_hello", result.NodeOutputs["n2"]["output"]);
        }
    }
}
