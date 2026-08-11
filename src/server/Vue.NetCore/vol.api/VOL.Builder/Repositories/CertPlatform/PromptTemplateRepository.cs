using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
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
