using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YZH.Core.AI.Clients;
using YZH.Core.AI.Clients.Models;
using YZH.Core.AI.Prompt;
using YZH.Core.AI.Prompt.Models;
using YZH.Core.Skills;
using YZH.Core.Workflow;

namespace YZH.Core.Tests.Skills
{
    public class S2_LlmExtractSkillTests
    {
        [Fact]
        public async Task ExecuteAsync_MockProvider_Should_ReturnFields()
        {
            var mockLlm = new Mock<ILlmClient>();
            mockLlm.Setup(c => c.CompleteAsync(It.IsAny<LlmRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmResponse
                {
                    Success = true,
                    Content = "{\"fields\":[{\"FieldCode\":\"name\",\"FieldValue\":\"张三\",\"Confidence\":0.95}],\"tables\":[]}",
                    Provider = "mock"
                });

            var mockInterpreter = new Mock<IPromptInterpreter>();
            mockInterpreter.Setup(i => i.Render(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
                .Returns((string t, IDictionary<string, object> c) => t);
            mockInterpreter.Setup(i => i.ParseAsync<AiExtractionResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ParseResult<AiExtractionResult>
                {
                    Success = true,
                    Value = new AiExtractionResult
                    {
                        Fields = new List<AiField> { new() { FieldCode = "name", FieldValue = "张三", Confidence = 0.95 } },
                        Tables = new List<AiTable>()
                    },
                    RawText = "{\"fields\":[...],\"tables\":[]}"
                });

            var skill = new LlmExtractSkill(mockLlm.Object, mockInterpreter.Object);
            var context = new SkillContext
            {
                Inputs = new Dictionary<string, object>
                {
                    ["document_content"] = "营业执照内容",
                    ["prompt"] = "请提取企业名称"
                }
            };
            var result = await skill.ExecuteAsync(context);
            Assert.True(result.Success);
            Assert.NotNull(result.Outputs["fields"]);
            Assert.NotNull(result.Confidence);
            Assert.True(result.Confidence.Value > 0);
        }

        [Fact]
        public async Task ExecuteAsync_MissingPrompt_Should_ReturnFailure()
        {
            var mockLlm = new Mock<ILlmClient>();
            var mockInterpreter = new Mock<IPromptInterpreter>();
            var skill = new LlmExtractSkill(mockLlm.Object, mockInterpreter.Object);
            var context = new SkillContext
            {
                Inputs = new Dictionary<string, object> { ["document_content"] = "test" }
            };
            var result = await skill.ExecuteAsync(context);
            Assert.False(result.Success);
            Assert.Contains("prompt", result.Error);
        }
    }
}
