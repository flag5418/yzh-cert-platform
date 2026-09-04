using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Cert;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
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
