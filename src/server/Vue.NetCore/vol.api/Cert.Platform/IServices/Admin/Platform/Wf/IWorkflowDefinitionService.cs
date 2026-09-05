using System.Collections.Generic;
using System.Threading.Tasks;
using YZH.Core.BaseProvider;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Wf;
using YZH.Entity.DomainModels;

namespace Cert.Platform.IServices.Admin.Platform.Wf
{
    public interface IWorkflowDefinitionService : IDependency
    {
        Task<PageGridData<WorkflowDefinition>> GetPageDataAsync(PageDataOptions options, string workflowType = null, bool? isActive = null);
        Task<List<WorkflowDefinition>> GetListAsync(string workflowType = null, bool? isActive = null);
        Task<WorkflowDefinition?> GetByCodeAsync(string workflowCode);
        Task<bool> SaveAsync(WorkflowDefinition entity);
        Task<bool> DeleteAsync(long id);
        Task<bool> ToggleActiveAsync(long id);
    }
}
