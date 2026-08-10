using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Entity.CertPlatform.DocExtraction;
using VOL.Entity.CertPlatform.DocExtraction.DTOs;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 文档提取规则服务接口
    /// </summary>
    public interface IDocExtractionRuleService : IService<CertDocExtractionRule>
    {
        /// <summary>
        /// AI自动分析文档
        /// </summary>
        Task<AIAnalyzeResponse> AIAnalyzeAsync(AIAnalyzeRequest request);

        /// <summary>
        /// 生成提取Prompt
        /// </summary>
        Task<string> GeneratePromptAsync(GeneratePromptRequest request);

        /// <summary>
        /// 验证Prompt
        /// </summary>
        Task<VerifyPromptResponse> VerifyPromptAsync(VerifyPromptRequest request);

        /// <summary>
        /// 保存提取规则（包含字段、表格定义）
        /// </summary>
        Task<bool> SaveExtractionRuleAsync(SaveExtractionRuleRequest request);

        /// <summary>
        /// 获取规则详情
        /// </summary>
        Task<RuleDetailResponse> GetRuleDetailAsync(string fileCode);

        /// <summary>
        /// 删除规则
        /// </summary>
        Task<bool> DeleteRuleAsync(string fileCode);

        /// <summary>
        /// 获取AI配置
        /// </summary>
        Task<AIConfigDto> GetAIConfigAsync();

        /// <summary>
        /// 更新AI配置
        /// </summary>
        Task<bool> UpdateAIConfigAsync(AIConfigDto config);

        /// <summary>
        /// 获取可用技能列表
        /// </summary>
        List<SkillInfo> GetSkills();
    }
}
