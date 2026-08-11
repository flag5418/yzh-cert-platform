using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using YZH.Core.AI.Prompt;

namespace YZH.Core.Tests.AI
{
    public class S3_PromptInterpreterTests
    {
        private readonly PromptInterpreter _interpreter = new();

        [Fact]
        public void Render_SubstituteString_Should_Work()
        {
            var result = _interpreter.Render("Hello {name}, you are {age} years old",
                new Dictionary<string, object> { ["name"] = "张三", ["age"] = 30 });
            Assert.Equal("Hello 张三, you are 30 years old", result);
        }

        [Fact]
        public void Render_MissingPlaceholder_Should_KeepOriginal()
        {
            var result = _interpreter.Render("Hello {name}, score is {score}",
                new Dictionary<string, object> { ["name"] = "李四" });
            Assert.Equal("Hello 李四, score is {score}", result);
        }

        [Fact]
        public void Render_NullValue_Should_Empty()
        {
            var result = _interpreter.Render("Value: {v}",
                new Dictionary<string, object> { ["v"] = null });
            Assert.Equal("Value: ", result);
        }

        [Fact]
        public void Render_ObjectValue_Should_Serialize()
        {
            var result = _interpreter.Render("Data: {obj}",
                new Dictionary<string, object> { ["obj"] = new { x = 1, y = 2 } });
            Assert.Contains("\"x\"", result);
            Assert.Contains("\"y\"", result);
        }

        [Fact]
        public async Task Parse_WithJsonFence_Should_Succeed()
        {
            var output = "```json\n{\"name\":\"张三\",\"age\":30}\n```";
            var result = await _interpreter.ParseAsync<TestDto>(output);
            Assert.True(result.Success);
            Assert.Equal("张三", result.Value!.Name);
            Assert.Equal(30, result.Value.Age);
        }

        [Fact]
        public async Task Parse_PureJson_Should_Succeed()
        {
            var output = "{\"name\":\"李四\",\"age\":25}";
            var result = await _interpreter.ParseAsync<TestDto>(output);
            Assert.True(result.Success);
            Assert.Equal("李四", result.Value!.Name);
        }

        [Fact]
        public async Task Parse_WithSurroundingText_Should_ExtractJson()
        {
            var output = "根据文档，结果如下：\n{\"name\":\"王五\",\"age\":40}\n以上是提取结果。";
            var result = await _interpreter.ParseAsync<TestDto>(output);
            Assert.True(result.Success);
            Assert.Equal("王五", result.Value!.Name);
        }

        [Fact]
        public async Task Parse_InvalidJson_Should_ReturnFailure()
        {
            var result = await _interpreter.ParseAsync<TestDto>("not valid json {{{");
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        private class TestDto
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
