using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using YZH.Core.AI.Plan;

namespace YZH.Core.Tests.AI
{
    public class S3_AiPlanParserTests
    {
        [Fact]
        public void Parse_ValidJson_Should_ReturnPlan()
        {
            var json = @"{
                ""plan_name"": ""营业执照提取"",
                ""steps"": [
                    {""order"": 1, ""skill_code"": ""llm_extract"", ""params"": {""prompt"": ""提取企业名称""}},
                    {""order"": 2, ""skill_code"": ""compare"", ""params"": {""operator"": ""not_empty""}}
                ],
                ""output_mapping"": {""name"": ""B08:name""}
            }";
            var plan = AiPlanParser.Parse(json);
            Assert.Equal("营业执照提取", plan.PlanName);
            Assert.Equal(2, plan.Steps.Count);
            Assert.Equal("llm_extract", plan.Steps[0].SkillCode);
            Assert.Equal(1, plan.Steps[0].Order);
            Assert.Equal("B08:name", plan.OutputMapping!["name"]);
        }

        [Fact]
        public void Parse_WithoutOutputMapping_Should_NotThrow()
        {
            var json = @"{""plan_name"": ""test"", ""steps"": [{""order"":1,""skill_code"":""a"",""params"":{}}]}";
            var plan = AiPlanParser.Parse(json);
            Assert.Null(plan.OutputMapping);
        }
    }
}
