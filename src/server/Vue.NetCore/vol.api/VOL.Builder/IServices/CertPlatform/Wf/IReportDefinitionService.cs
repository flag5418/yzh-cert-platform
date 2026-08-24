using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.Rpt;
using VOL.Entity.DomainModels;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IReportDefinitionService : IDependency
    {
        Task<PageGridData<ReportTemplate>> GetPageDataAsync(PageDataOptions options, string orgCode = null, string standardCode = null, string phaseCode = null);
        Task<List<ReportTemplate>> GetByOrgStandardPhaseAsync(string orgCode, string standardCode, string phaseCode);
        Task<ReportTemplate> GetTemplateAsync(string code);
        /// <summary>按 org+std+phase 查询唯一报告模板，不存在返回 null</summary>
        Task<ReportTemplate> GetByContextAsync(string orgCode, string standardCode, string phaseCode);
        Task<bool> SaveTemplateAsync(ReportTemplate entity);
        Task<bool> DeleteTemplateAsync(long id);

        Task<List<ReportSection>> GetSectionsAsync(string reportCode);
        Task<bool> SaveSectionAsync(ReportSection entity);
        Task<bool> DeleteSectionAsync(long id);
        Task<ReportSection> CopySectionAsync(long sourceId);
    }
}
