using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.DomainModels;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IValidationRuleService : IDependency
    {
        Task<PageGridData<dynamic>> GetPageDataAsync(PageDataOptions options, string orgCode = null, string standardCode = null, string phaseCode = null);
        Task<List<ValidationRule>> GetByOrgStandardPhaseAsync(string orgCode, string standardCode, string phaseCode);
        Task<ValidationRule> GetByRuleCodeAsync(string ruleCode);
        Task<ValidationRule> CopyAsync(string sourceRuleCode);
        Task<bool> SaveAsync(ValidationRule entity);
        Task<bool> DeleteAsync(long id);
        Task<bool> ToggleActiveAsync(long id);
    }
}
