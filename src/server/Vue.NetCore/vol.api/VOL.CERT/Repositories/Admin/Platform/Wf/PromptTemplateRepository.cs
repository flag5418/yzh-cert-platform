using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Wf;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
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
