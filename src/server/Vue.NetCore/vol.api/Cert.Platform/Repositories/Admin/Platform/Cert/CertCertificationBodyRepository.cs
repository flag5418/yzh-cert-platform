using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Cert
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
