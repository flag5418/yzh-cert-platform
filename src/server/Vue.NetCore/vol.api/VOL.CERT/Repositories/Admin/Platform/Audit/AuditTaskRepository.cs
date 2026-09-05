using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Audit;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.Admin.Platform.Audit
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