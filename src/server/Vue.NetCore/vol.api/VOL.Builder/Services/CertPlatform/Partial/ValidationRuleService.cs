using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.DomainModels;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class ValidationRuleService : IValidationRuleService
    {
        private readonly IValidationRuleRepository _repository;

        public ValidationRuleService(IValidationRuleRepository repository)
        {
            _repository = repository;
        }

        public static IValidationRuleService Instance =>
            AutofacContainerModule.GetService<IValidationRuleService>();

        public async Task<PageGridData<ValidationRule>> GetPageDataAsync(PageDataOptions options, string orgCode = null, string standardCode = null, string phaseCode = null)
        {
            var query = _repository.FindAsIQueryable(x => true);
            if (!string.IsNullOrWhiteSpace(orgCode)) query = query.Where(x => x.OrgCode == orgCode);
            if (!string.IsNullOrWhiteSpace(standardCode)) query = query.Where(x => x.StandardCode == standardCode);
            if (!string.IsNullOrWhiteSpace(phaseCode)) query = query.Where(x => x.PhaseCode == phaseCode);

            string sortField = options.Sort ?? "Id";
            bool isAsc = options.Order?.ToLower() == "asc";
            query = sortField.ToUpper() switch
            {
                "RULECODE" => isAsc ? query.OrderBy(x => x.RuleCode) : query.OrderByDescending(x => x.RuleCode),
                "RULENAME" => isAsc ? query.OrderBy(x => x.RuleName) : query.OrderByDescending(x => x.RuleName),
                "STANDARDCODE" => isAsc ? query.OrderBy(x => x.StandardCode) : query.OrderByDescending(x => x.StandardCode),
                "PHASECODE" => isAsc ? query.OrderBy(x => x.PhaseCode) : query.OrderByDescending(x => x.PhaseCode),
                "CREATEDATE" => isAsc ? query.OrderBy(x => x.CreateDate) : query.OrderByDescending(x => x.CreateDate),
                _ => query.OrderByDescending(x => x.Id),
            };

            int totalCount = await query.CountAsync();
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = await query.Skip((page - 1) * rows).Take(rows).ToListAsync();
            return new PageGridData<ValidationRule> { rows = list, total = totalCount };
        }

        public async Task<List<ValidationRule>> GetByOrgStandardPhaseAsync(string orgCode, string standardCode, string phaseCode)
        {
            return await _repository.FindAsIQueryable(x =>
                x.OrgCode == orgCode && x.StandardCode == standardCode && x.PhaseCode == phaseCode)
                .OrderBy(x => x.RuleName).ToListAsync();
        }

        public async Task<ValidationRule> GetByRuleCodeAsync(string ruleCode)
        {
            return await _repository.FindFirstAsync(x => x.RuleCode == ruleCode);
        }

        public async Task<ValidationRule> CopyAsync(string sourceRuleCode)
        {
            var source = await _repository.FindFirstAsync(x => x.RuleCode == sourceRuleCode);
            if (source == null) return null;
            var copy = new ValidationRule
            {
                OrgCode = source.OrgCode, StandardCode = source.StandardCode, PhaseCode = source.PhaseCode,
                ClauseCode = source.ClauseCode, WorkflowCode = source.WorkflowCode,
                RuleCode = $"COPY-{System.Guid.NewGuid():N}",
                RuleName = $"{source.RuleName}（副本）", RuleNameEn = source.RuleNameEn,
                SeverityIfViolated = source.SeverityIfViolated, RuleJson = source.RuleJson,
                NcDescriptionTemplate = source.NcDescriptionTemplate, Remark = source.Remark,
                IsActive = false, CreateDate = System.DateTime.Now, Creator = UserContext.Current?.UserName
            };
            await _repository.AddAsync(copy);
            return copy;
        }

        public async Task<bool> SaveAsync(ValidationRule entity)
        {
            if (entity.Id > 0)
            {
                var existing = await _repository.FindFirstAsync(x => x.Id == entity.Id);
                if (existing == null) return false;
                existing.OrgCode = entity.OrgCode; existing.StandardCode = entity.StandardCode;
                existing.PhaseCode = entity.PhaseCode; existing.ClauseCode = entity.ClauseCode;
                existing.WorkflowCode = entity.WorkflowCode; existing.RuleName = entity.RuleName;
                existing.RuleNameEn = entity.RuleNameEn; existing.SeverityIfViolated = entity.SeverityIfViolated;
                existing.RuleJson = entity.RuleJson; existing.NcDescriptionTemplate = entity.NcDescriptionTemplate;
                existing.Remark = entity.Remark;
                _repository.Update(existing, new[] { "OrgCode","StandardCode","PhaseCode","ClauseCode","WorkflowCode",
                    "RuleName","RuleNameEn","SeverityIfViolated","RuleJson","NcDescriptionTemplate","Remark" }, true);
                return true;
            }
            entity.CreateDate = System.DateTime.Now;
            entity.Creator = UserContext.Current?.UserName;
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity != null) { _repository.Delete(entity, true); return true; }
            return false;
        }

        public async Task<bool> ToggleActiveAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity != null)
            {
                entity.IsActive = !entity.IsActive;
                _repository.Update(entity, new[] { "IsActive" }, true);
                return true;
            }
            return false;
        }
    }
}
