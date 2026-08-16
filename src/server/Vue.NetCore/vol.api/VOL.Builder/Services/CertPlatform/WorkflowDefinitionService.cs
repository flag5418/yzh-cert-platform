using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Entity.DomainModels;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class WorkflowDefinitionService : IWorkflowDefinitionService
    {
        private readonly VOLContext _db;

        public WorkflowDefinitionService(VOLContext db)
        {
            _db = db;
        }

        public async Task<PageGridData<WorkflowDefinition>> GetPageDataAsync(PageDataOptions options, string workflowType = null, bool? isActive = null)
        {
            var query = _db.Set<WorkflowDefinition>().AsQueryable();
            if (!string.IsNullOrWhiteSpace(workflowType)) query = query.Where(x => x.WorkflowType == workflowType);
            if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);

            query = options.Sort?.ToUpper() switch
            {
                "WORKFLOWCODE" => options.Order?.ToLower() == "asc" ? query.OrderBy(x => x.WorkflowCode) : query.OrderByDescending(x => x.WorkflowCode),
                "WORKFLOWNAME" => options.Order?.ToLower() == "asc" ? query.OrderBy(x => x.WorkflowName) : query.OrderByDescending(x => x.WorkflowName),
                "WORKFLOWTYPE" => options.Order?.ToLower() == "asc" ? query.OrderBy(x => x.WorkflowType) : query.OrderByDescending(x => x.WorkflowType),
                "CREATEDATE" => options.Order?.ToLower() == "asc" ? query.OrderBy(x => x.CreateDate) : query.OrderByDescending(x => x.CreateDate),
                _ => query.OrderByDescending(x => x.Id),
            };

            int totalCount = await query.CountAsync();
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = await query.Skip((page - 1) * rows).Take(rows).ToListAsync();
            return new PageGridData<WorkflowDefinition> { rows = list, total = totalCount };
        }

        public async Task<List<WorkflowDefinition>> GetListAsync(string workflowType = null, bool? isActive = null)
        {
            var query = _db.Set<WorkflowDefinition>().AsQueryable();
            if (!string.IsNullOrWhiteSpace(workflowType)) query = query.Where(x => x.WorkflowType == workflowType);
            if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
            return await query.OrderBy(x => x.WorkflowCode).ToListAsync();
        }

        public async Task<WorkflowDefinition> GetByCodeAsync(string workflowCode)
        {
            return await _db.Set<WorkflowDefinition>().FirstOrDefaultAsync(x => x.WorkflowCode == workflowCode);
        }

        public async Task<bool> SaveAsync(WorkflowDefinition entity)
        {
            if (entity.Id > 0)
            {
                _db.Set<WorkflowDefinition>().Update(entity);
            }
            else
            {
                await _db.Set<WorkflowDefinition>().AddAsync(entity);
            }
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var entity = await _db.Set<WorkflowDefinition>().FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return false;
            _db.Set<WorkflowDefinition>().Remove(entity);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleActiveAsync(long id)
        {
            var entity = await _db.Set<WorkflowDefinition>().FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return false;
            entity.IsActive = !entity.IsActive;
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
