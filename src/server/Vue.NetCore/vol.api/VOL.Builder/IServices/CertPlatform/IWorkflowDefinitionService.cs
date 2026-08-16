using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Entity.DomainModels;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IWorkflowDefinitionService : IDependency
    {
        Task<PageGridData<WorkflowDefinition>> GetPageDataAsync(PageDataOptions options, string workflowType = null, bool? isActive = null);
        Task<List<WorkflowDefinition>> GetListAsync(string workflowType = null, bool? isActive = null);
        Task<WorkflowDefinition> GetByCodeAsync(string workflowCode);
        Task<bool> SaveAsync(WorkflowDefinition entity);
        Task<bool> DeleteAsync(long id);
        Task<bool> ToggleActiveAsync(long id);
    }
}
