using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Cert
{
    public partial class CertStageRepository : RepositoryBase<CertStage>, ICertStageRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public CertStageRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static ICertStageRepository Instance
        {
            get { return AutofacContainerModule.GetService<ICertStageRepository>(); }
        }
    }
}
