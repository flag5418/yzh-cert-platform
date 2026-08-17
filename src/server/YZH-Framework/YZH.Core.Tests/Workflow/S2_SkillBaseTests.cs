using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YZH.Core.Skills;
using YZH.Core.Workflow;

namespace YZH.Core.Tests.Workflow
{
    /// <summary>SkillBase 抽象类 + 核心 Skill（assemble 等）测试</summary>
    public class S2_SkillBaseTests
    {
        [Fact]
        public async Task SkillBase_MissingRequired_Should_Fail()
        {
            var skill = new AssembleSkill();
            // parts 必填缺失
            var ctx = new SkillContext { Inputs = new Dictionary<string, object> { ["joiner"] = "" } };
            var result = await skill.ExecuteAsync(ctx);
            Assert.False(result.Success);
            Assert.Contains("parts", result.Error);
        }

        [Fact]
        public async Task Assemble_MultiPart_Ordered_Should_JoinDirectly()
        {
            var skill = new AssembleSkill();
            var ctx = new SkillContext
            {
                Inputs = new Dictionary<string, object>
                {
                    ["parts"] = new object[] { "该企业", "2024年", "培训记录不完整" },
                    ["joiner"] = ""
                }
            };
            var result = await skill.ExecuteAsync(ctx);
            Assert.True(result.Success);
            Assert.Equal("该企业2024年培训记录不完整", result.Outputs["assembled_text"]);
        }

        [Fact]
        public async Task Assemble_MultiPart_WithSeparator_Should_Join()
        {
            var skill = new AssembleSkill();
            var ctx = new SkillContext
            {
                Inputs = new Dictionary<string, object>
                {
                    ["parts"] = new object[] { "条款7.1", "资源充分性" },
                    ["joiner"] = "："
                }
            };
            var result = await skill.ExecuteAsync(ctx);
            Assert.True(result.Success);
            Assert.Equal("条款7.1：资源充分性", result.Outputs["assembled_text"]);
        }

        [Fact]
        public async Task Assemble_JsonArrayString_Should_Parse()
        {
            var skill = new AssembleSkill();
            var ctx = new SkillContext
            {
                Inputs = new Dictionary<string, object>
                {
                    ["parts"] = "[\"结论\",\"不符合\"]",
                    ["joiner"] = ""
                }
            };
            var result = await skill.ExecuteAsync(ctx);
            Assert.True(result.Success);
            Assert.Equal("结论不符合", result.Outputs["assembled_text"]);
        }

        [Fact]
        public async Task Compare_NotEmpty_Should_ReturnBoolean()
        {
            var skill = new CompareSkill();
            var ctx = new SkillContext
            {
                Inputs = new Dictionary<string, object> { ["value"] = "有值", ["operator"] = "not_empty" }
            };
            var result = await skill.ExecuteAsync(ctx);
            Assert.True(result.Success);
            Assert.Equal(true, result.Outputs["result"]);
        }
    }

    /// <summary>引擎 ResolveInputs 递归解析（数组/对象内模板，支撑常量+变量混合拼接）</summary>
    public class S4_ResolveInputsRecursionTests
    {
        private static WorkflowEngine CreateEngine(params ISkillNode[] skills)
        {
            var registry = new SkillRegistry(Mock.Of<ILogger<SkillRegistry>>());
            foreach (var s in skills) registry.RegisterAsync(s).GetAwaiter().GetResult();
            return new WorkflowEngine(registry, Mock.Of<ILogger<WorkflowEngine>>());
        }

        private class EchoSkill : ISkillNode
        {
            public string SkillCode { get; }
            public object? OutputValue { get; set; }
            public EchoSkill(string code, object? outputValue = null) { SkillCode = code; OutputValue = outputValue; }
            public Task<SkillResult> ExecuteAsync(SkillContext context, System.Threading.CancellationToken ct = default)
            {
                var output = OutputValue ?? (context.Inputs.TryGetValue("value", out var v) ? v.ToString() : "default");
                return Task.FromResult(SkillResult.Ok(new Dictionary<string, object> { ["output"] = output }));
            }
        }

        /// <summary>把解析后的输入原样 JSON 序列化回显（非转义中文），用于断言递归解析结果</summary>
        private class EchoResolvedSkill : ISkillNode
        {
            public string SkillCode => "echo_resolved";
            private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            public Task<SkillResult> ExecuteAsync(SkillContext context, System.Threading.CancellationToken ct = default)
                => Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
                {
                    ["echo"] = System.Text.Json.JsonSerializer.Serialize(context.Inputs, JsonOpts)
                }));
        }

        [Fact]
        public async Task RunAsync_ArrayInput_WithTemplate_Should_ResolveInsideArray()
        {
            var engine = CreateEngine(
                new EchoSkill("n1", "北京"),
                new EchoResolvedSkill());
            var config = @"{
                ""nodes"": [
                    {""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""city""},
                    {""node_id"":""n2"",""skill_code"":""echo_resolved"",""inputs"":{""value"":[""该企业位于"", ""{{n1.output}}"", ""市""]},""output"":""result""}
                ],
                ""edges"": [{""from"":""n1"",""to"":""n2""}]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
            var echo = result.NodeOutputs["n2"]["echo"].ToString();
            // 数组内模板已递归解析：{{n1.output}} → 北京
            Assert.Contains("\"北京\"", echo);
            Assert.Contains("\"该企业位于\"", echo);
            Assert.Contains("\"市\"", echo);
        }

        [Fact]
        public async Task RunAsync_Assemble_WithConstantAndReference_Should_ConcatInOrder()
        {
            var engine = CreateEngine(
                new EchoSkill("n1", "15"),
                new AssembleSkill());
            var config = @"{
                ""nodes"": [
                    {""node_id"":""n1"",""skill_code"":""n1"",""inputs"":{},""output"":""staff""},
                    {""node_id"":""n2"",""skill_code"":""assemble"",""inputs"":{""parts"":[""企业人员数量"", ""{{n1.output}}"", ""人""], ""joiner"":""""},""output"":""text""}
                ],
                ""edges"": [{""from"":""n1"",""to"":""n2""}]
            }";
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf1", BusinessType = "test", LogStore = null };
            var result = await engine.RunAsync(config, ctx);
            Assert.True(result.Success);
            Assert.Equal("企业人员数量15人", result.NodeOutputs["n2"]["assembled_text"]);
        }
    }
}
