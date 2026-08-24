using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
{
    public partial class CertCertificationBodyRepository : RepositoryBase<CertificationBody>, ICertCertificationBodyRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public CertCertificationBodyRepository(VOLContext dbContext)
        : base(dbContext)
        {

        }
        public static ICertCertificationBodyRepository Instance
        {
            get { return AutofacContainerModule.GetService<ICertCertificationBodyRepository>(); }
        }
    }
}
