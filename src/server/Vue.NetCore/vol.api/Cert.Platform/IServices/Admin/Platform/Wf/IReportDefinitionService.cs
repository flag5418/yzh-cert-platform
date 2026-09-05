using System.Collections.Generic;
using System.Threading.Tasks;
using YZH.Core.BaseProvider;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using YZH.Entity.Admin.Platform.Rpt;
using YZH.Entity.DomainModels;

namespace Cert.Platform.IServices.Admin.Platform
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
        /// <summary>按 org+std+phase 查询章节列表（先查模板再查章节）</summary>
        Task<List<ReportSection>> GetSectionsByContextAsync(string orgCode, string standardCode, string phaseCode);
        Task<bool> SaveSectionAsync(ReportSection entity);
        Task<bool> DeleteSectionAsync(long id);
        Task<ReportSection> CopySectionAsync(long sourceId);
    }
}
