using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Wf;
using VOL.CERT.IRepositories.Admin.Platform.Wf;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.Admin.Platform.Wf
{
    public partial class WorkflowDefinitionRepository : RepositoryBase<WorkflowDefinition>, IWorkflowDefinitionRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public WorkflowDefinitionRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }
    }
}
