using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Clients.Models;

namespace YZH.Core.Tests.AI
{
    public class S1_LlmClientTests
    {
        private static ILlmClient CreateClient(
            List<ILlmProvider>? extraProviders = null,
            string activeProvider = "qwen")
        {
            var providers = new List<ILlmProvider> { new MockProvider() };
            if (extraProviders != null) providers.AddRange(extraProviders);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:Provider"] = activeProvider })
                .Build();
            var logger = new Mock<ILogger<LlmClient>>().Object;
            return new LlmClient(providers.AsEnumerable(), config, logger);
        }

        [Fact]
        public async Task CompleteAsync_WithMockProvider_Should_ReturnFixedContent()
        {
            var client = CreateClient();
            var request = new LlmRequest
            {
                Provider = "mock",
                Messages = new List<LlmMessage> { new() { Role = "user", Content = "test" } }
            };
            var response = await client.CompleteAsync(request);
            Assert.True(response.Success);
            Assert.Equal("{\"fields\":[],\"tables\":[]}", response.Content);
            Assert.Equal("mock", response.Provider);
        }

        [Fact]
        public async Task CompleteAsync_ActiveProvider_Should_UseConfig()
        {
            // 显式设置 Provider=null，走 ActiveProvider（config=mock）
            var client = CreateClient(activeProvider: "mock");
            var request = new LlmRequest { Provider = null };
            var response = await client.CompleteAsync(request);
            Assert.True(response.Success);
            Assert.Equal("mock", response.Provider);
        }

        [Fact]
        public async Task CompleteAsync_UnknownProvider_Should_ThrowLlmCallException()
        {
            // 注册表中只有 MockProvider(Name="mock")，request.Provider="unknown" 未注册 → 抛异常
            var client = CreateClient();
            var request = new LlmRequest { Provider = "unknown" };
            var ex = await Assert.ThrowsAsync<YZH.Core.AI.LlmCallException>(() => client.CompleteAsync(request));
            Assert.True(ex.IsUnreachable);
            Assert.Contains("未注册", ex.Message);
        }

        [Fact]
        public async Task CompleteAsync_Concurrent10_Should_CompleteWithoutDeadlock()
        {
            var client = CreateClient(activeProvider: "mock");
            var requests = Enumerable.Range(1, 10)
                .Select(i => new LlmRequest { Provider = "mock", Messages = new List<LlmMessage> { new() { Content = "msg" + i } } })
                .ToList();
            var tasks = requests.Select(r => client.CompleteAsync(r));
            var responses = await Task.WhenAll(tasks);
            Assert.Equal(10, responses.Length);
            Assert.All(responses, r => Assert.True(r.Success));
        }

        [Fact]
        public async Task CompleteAsync_FailingProvider_Should_FallbackToNext()
        {
            var failingProvider = new Mock<ILlmProvider>();
            failingProvider.Setup(p => p.Name).Returns("qwen");
            failingProvider.Setup(p => p.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new YZH.Core.AI.LlmCallException("Qwen 超时", true));

            var mockProvider = new Mock<ILlmProvider>();
            mockProvider.Setup(p => p.Name).Returns("mock");
            mockProvider.Setup(p => p.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmResponse { Success = true, Content = "fallback", Provider = "mock" });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:Provider"] = "qwen" })
                .Build();
            var logger = new Mock<ILogger<LlmClient>>().Object;
            var client = new LlmClient(
                new List<ILlmProvider> { failingProvider.Object, mockProvider.Object }.AsEnumerable(),
                config, logger);

            var response = await client.CompleteAsync(new LlmRequest { Provider = "qwen" });
            Assert.True(response.Success);
            Assert.Equal("fallback", response.Content);
            Assert.Equal("mock", response.Provider);
        }

        [Fact]
        public async Task CompleteAsync_5ConsecutiveFailures_Should_CircuitOpen()
        {
            var failingProvider = new Mock<ILlmProvider>();
            failingProvider.Setup(p => p.Name).Returns("qwen");
            failingProvider.Setup(p => p.ChatAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new YZH.Core.AI.LlmCallException("每次都失败", true));

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:Provider"] = "qwen" })
                .Build();
            var logger = new Mock<ILogger<LlmClient>>().Object;
            var client = new LlmClient(
                new List<ILlmProvider> { failingProvider.Object }.AsEnumerable(),
                config, logger);

            for (var i = 0; i < 5; i++)
            {
                await Assert.ThrowsAsync<YZH.Core.AI.LlmCallException>(
                    () => client.CompleteAsync(new LlmRequest { Provider = "qwen" }));
            }

            var ex = await Assert.ThrowsAsync<YZH.Core.AI.LlmCallException>(
                () => client.CompleteAsync(new LlmRequest { Provider = "qwen" }));
            Assert.True(ex.IsUnreachable);
            Assert.Contains("熔断", ex.Message);
        }

        [Fact]
        public void LlmRequest_Defaults_Should_BeCorrect()
        {
            var req = new LlmRequest();
            Assert.Equal("qwen", req.Provider);
            Assert.Equal("qwen-turbo", req.Model);
            Assert.Equal(0.1, req.Temperature);
            Assert.Equal(4096, req.MaxTokens);
            Assert.True(req.JsonMode);
            Assert.Equal(60, req.TimeoutSeconds);
        }
    }
}
