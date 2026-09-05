using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Cert
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
