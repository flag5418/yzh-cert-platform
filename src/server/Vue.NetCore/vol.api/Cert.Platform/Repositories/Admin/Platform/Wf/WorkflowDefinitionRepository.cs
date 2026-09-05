using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Wf;
using Cert.Platform.IRepositories.Admin.Platform.Wf;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Wf
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
