using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Wf;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Wf
{
    public partial class PromptTemplateRepository : RepositoryBase<PromptTemplate>, IPromptTemplateRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public PromptTemplateRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IPromptTemplateRepository Instance
        {
            get { return AutofacContainerModule.GetService<IPromptTemplateRepository>(); }
        }
    }
}
