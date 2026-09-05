using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Audit;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Audit
{
  public partial class AuditTaskRepository : RepositoryBase<AuditTask>, IAuditTaskRepository, IDependency
  {
    [ActivatorUtilitiesConstructor]
    public AuditTaskRepository(VOLContext dbContext)
    : base(dbContext)
    {

    }
    public static IAuditTaskRepository Instance
    {
      get { return AutofacContainerModule.GetService<IAuditTaskRepository>(); }
    }
  }
}