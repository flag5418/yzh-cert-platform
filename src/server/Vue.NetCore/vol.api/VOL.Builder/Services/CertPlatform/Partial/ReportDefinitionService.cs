using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.Rpt;
using VOL.Entity.DomainModels;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class ReportDefinitionService : IReportDefinitionService
    {
        private readonly IReportTemplateRepository _templateRepo;
        private readonly IReportSectionRepository _sectionRepo;

        public ReportDefinitionService(IReportTemplateRepository templateRepo, IReportSectionRepository sectionRepo)
        {
            _templateRepo = templateRepo;
            _sectionRepo = sectionRepo;
        }

        public static IReportDefinitionService Instance =>
            AutofacContainerModule.GetService<IReportDefinitionService>();

        // ── 报告模板 ──

        public async Task<PageGridData<ReportTemplate>> GetPageDataAsync(PageDataOptions options, string orgCode = null, string standardCode = null, string phaseCode = null)
        {
            var query = _templateRepo.FindAsIQueryable(x => true);
            if (!string.IsNullOrWhiteSpace(orgCode)) query = query.Where(x => x.OrgCode == orgCode);
            if (!string.IsNullOrWhiteSpace(standardCode)) query = query.Where(x => x.StandardCode == standardCode);
            if (!string.IsNullOrWhiteSpace(phaseCode)) query = query.Where(x => x.PhaseCode == phaseCode);

            string sortField = options.Sort ?? "Id";
            bool isAsc = options.Order?.ToLower() == "asc";
            query = sortField.ToUpper() switch
            {
                "TEMPLATENAME" => isAsc ? query.OrderBy(x => x.TemplateName) : query.OrderByDescending(x => x.TemplateName),
                "STANDARDCODE" => isAsc ? query.OrderBy(x => x.StandardCode) : query.OrderByDescending(x => x.StandardCode),
                "PHASECODE" => isAsc ? query.OrderBy(x => x.PhaseCode) : query.OrderByDescending(x => x.PhaseCode),
                "CREATEDATE" => isAsc ? query.OrderBy(x => x.CreateDate) : query.OrderByDescending(x => x.CreateDate),
                _ => query.OrderByDescending(x => x.Id),
            };

            int totalCount = await query.CountAsync();
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = await query.Skip((page - 1) * rows).Take(rows).ToListAsync();
            return new PageGridData<ReportTemplate> { rows = list, total = totalCount };
        }

        public async Task<List<ReportTemplate>> GetByOrgStandardPhaseAsync(string orgCode, string standardCode, string phaseCode)
        {
            return await _templateRepo.FindAsIQueryable(x =>
                x.OrgCode == orgCode && x.StandardCode == standardCode && x.PhaseCode == phaseCode)
                .OrderBy(x => x.TemplateName).ToListAsync();
        }

        public async Task<ReportTemplate> GetTemplateAsync(string code)
        {
            return await _templateRepo.FindFirstAsync(x => x.Code == code);
        }

        public async Task<bool> SaveTemplateAsync(ReportTemplate entity)
        {
            if (entity.Id > 0)
            {
                var existing = await _templateRepo.FindFirstAsync(x => x.Id == entity.Id);
                if (existing == null) return false;
                existing.OrgCode = entity.OrgCode; existing.StandardCode = entity.StandardCode;
                existing.PhaseCode = entity.PhaseCode; existing.CbCode = entity.CbCode;
                existing.TemplateName = entity.TemplateName; existing.TemplateFilePath = entity.TemplateFilePath;
                existing.SectionConfig = entity.SectionConfig; existing.IsDefault = entity.IsDefault;
                existing.Remark = entity.Remark;
                _templateRepo.Update(existing, new[] { "OrgCode","StandardCode","PhaseCode","CbCode","TemplateName",
                    "TemplateFilePath","SectionConfig","IsDefault","Remark" }, true);
                return true;
            }
            entity.CreateDate = System.DateTime.Now;
            entity.Creator = UserContext.Current?.UserName;
            await _templateRepo.AddAsync(entity);
            return true;
        }

        public async Task<bool> DeleteTemplateAsync(long id)
        {
            var entity = await _templateRepo.FindFirstAsync(x => x.Id == id);
            if (entity != null) { _templateRepo.Delete(entity, true); return true; }
            return false;
        }

        // ── 报告章节 ──

        public async Task<List<ReportSection>> GetSectionsAsync(string reportCode)
        {
            return await _sectionRepo.FindAsIQueryable(x => x.ReportCode == reportCode)
                .OrderBy(x => x.SortOrder).ToListAsync();
        }

        public async Task<bool> SaveSectionAsync(ReportSection entity)
        {
            if (entity.Id > 0)
            {
                var existing = await _sectionRepo.FindFirstAsync(x => x.Id == entity.Id);
                if (existing == null) return false;
                existing.ReportCode = entity.ReportCode; existing.ClauseCode = entity.ClauseCode;
                existing.WorkflowCode = entity.WorkflowCode; existing.SectionName = entity.SectionName;
                existing.SectionNameEn = entity.SectionNameEn; existing.SectionJson = entity.SectionJson;
                existing.Remark = entity.Remark; existing.IsActive = entity.IsActive;
                existing.Content = entity.Content; existing.SortOrder = entity.SortOrder;
                _sectionRepo.Update(existing, new[] { "ReportCode","ClauseCode","WorkflowCode","SectionName",
                    "SectionNameEn","SectionJson","Remark","IsActive","Content","SortOrder" }, true);
                return true;
            }
            entity.CreateDate = System.DateTime.Now;
            entity.Creator = UserContext.Current?.UserName;
            await _sectionRepo.AddAsync(entity);
            return true;
        }

        public async Task<bool> DeleteSectionAsync(long id)
        {
            var entity = await _sectionRepo.FindFirstAsync(x => x.Id == id);
            if (entity != null) { _sectionRepo.Delete(entity, true); return true; }
            return false;
        }

        public async Task<ReportSection> CopySectionAsync(long sourceId)
        {
            var source = await _sectionRepo.FindFirstAsync(x => x.Id == sourceId);
            if (source == null) return null;
            var copy = new ReportSection
            {
                OrgCode = source.OrgCode, ReportCode = source.ReportCode, ClauseCode = source.ClauseCode,
                WorkflowCode = source.WorkflowCode, SectionName = $"{source.SectionName}（副本）",
                SectionNameEn = source.SectionNameEn, SectionJson = source.SectionJson,
                Remark = source.Remark, IsActive = false, Content = source.Content,
                SortOrder = source.SortOrder + 1, CreateDate = System.DateTime.Now,
                Creator = UserContext.Current?.UserName
            };
            await _sectionRepo.AddAsync(copy);
            return copy;
        }
    }
}
