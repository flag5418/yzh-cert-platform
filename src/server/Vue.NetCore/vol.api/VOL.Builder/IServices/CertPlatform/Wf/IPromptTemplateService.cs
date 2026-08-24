using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Utilities;

using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// Prompt 模板服务接口
    /// </summary>
    public interface IPromptTemplateService : IDependency
    {
        /// <summary>获取所有提示词列表（可按类型筛选）</summary>
        Task<WebResponseContent> GetListAsync(string? promptType = null, string? skillTarget = null);

        /// <summary>根据编码获取提示词</summary>
        Task<PromptTemplate?> GetByCodeAsync(string promptCode);

        /// <summary>获取指定类型当前生效的提示词</summary>
        Task<PromptTemplate?> GetActiveAsync(string promptType, string? skillTarget = null);

        /// <summary>创建或更新提示词（幂等：按 prompt_code 匹配）</summary>
        Task<bool> SaveAsync(PromptTemplate entity);

        /// <summary>删除提示词</summary>
        Task<bool> DeleteAsync(string promptCode);

        /// <summary>激活指定提示词（同类型其他版本设为不活跃）</summary>
        Task<bool> ActivateAsync(string promptCode);
    }
}
