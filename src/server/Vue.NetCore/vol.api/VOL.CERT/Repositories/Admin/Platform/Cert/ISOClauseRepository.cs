using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Cert;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
{
    public partial class ISOClauseRepository : RepositoryBase<ISOClause>, IISOClauseRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public ISOClauseRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IISOClauseRepository Instance
        {
            get { return AutofacContainerModule.GetService<IISOClauseRepository>(); }
        }
    }
}
