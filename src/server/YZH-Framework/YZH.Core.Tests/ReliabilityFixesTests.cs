using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YZH.Core.AI;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Clients.Models;
using YZH.Core.Workflow;
using YZH.Core.Workflow.Models;

namespace YZH.Core.Tests
{
    /// <summary>
    /// 本次 4 项可靠性/整洁度修复的定向回归（替换已停更的旧 YZH.Core.Tests 陈旧套件）。
    /// 覆盖：
    ///  - 修复 #2：LlmClient 熔断改为实例级（多 Provider / 调用方互不污染）
    ///  - 修复 #4：WorkflowEngine 拓扑环改用结构化 _logger.LogError（替换 Console.WriteLine 调试残留）
    /// （修复 #1 死代码删除、#3 HttpClient 复用为编译期/架构级验证，不参与本文件断言。）
    /// </summary>
    public class ReliabilityFixesTests
    {
        #region 修复 #4：拓扑环结构化日志

        [Fact]
        public async Task WorkflowEngine_CyclicGraph_Should_LogError_And_Throw_WorkflowExecutionException()
        {
            // Arrange：A->B->A 成环
            const string json = @"{
                ""nodes"": [
                    { ""node_id"": ""A"", ""skill_code"": ""sA"" },
                    { ""node_id"": ""B"", ""skill_code"": ""sB"" }
                ],
                ""edges"": [
                    { ""from"": ""A"", ""to"": ""B"" },
                    { ""from"": ""B"", ""to"": ""A"" }
                ]
            }";

            var logger = new Mock<ILogger<WorkflowEngine>>();
            var registry = Mock.Of<ISkillRegistry>();
            var engine = new WorkflowEngine(registry, logger.Object);
            var ctx = new WorkflowContext { WorkflowInstanceId = "wf-cycle" };

            // Act & Assert：应抛环异常，且用结构化日志（非 Console.WriteLine）记录
            var ex = await Assert.ThrowsAsync<WorkflowExecutionException>(
                () => engine.RunAsync(json, ctx, CancellationToken.None));

            Assert.Contains("环", ex.Message);

            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((_, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region 修复 #2：熔断为实例级状态

        [Fact]
        public async Task LlmClient_CircuitBreaker_ShouldBe_InstanceScoped_Not_ProcessStatic()
        {
            // Arrange：同一成功 Provider，构造两个独立 LlmClient 实例
            var successProvider = new SuccessProvider();
            var config = Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>();

            var client1 = new LlmClient(new[] { successProvider }, config, Mock.Of<ILogger<LlmClient>>());
            var client2 = new LlmClient(new[] { successProvider }, config, Mock.Of<ILogger<LlmClient>>());

            // 通过反射将 client1 的熔断窗口置为打开（验证“实例级”而非进程级 static 共享）
            var circuitField = typeof(LlmClient).GetField(
                "_circuitBreakerUntil",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            circuitField.SetValue(client1, DateTime.Now.AddSeconds(30));

            var req = new LlmRequest
            {
                Provider = "qwen",
                Model = "qwen-turbo",
                Messages = new() { new LlmMessage { Role = "user", Content = "hi" } }
            };

            // client1：熔断打开，直接快速失败（不会调用 Provider）
            await Assert.ThrowsAsync<LlmCallException>(
                () => client1.CompleteAsync(req, CancellationToken.None));

            // client2：熔断状态与 client1 相互独立，应正常返回成功
            var resp = await client2.CompleteAsync(req, CancellationToken.None);
            Assert.True(resp.Success);
        }

        private sealed class SuccessProvider : ILlmProvider
        {
            public string Name => "qwen";
            public Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
                => Task.FromResult(new LlmResponse { Success = true, Content = "ok" });
        }

        #endregion
    }
}
