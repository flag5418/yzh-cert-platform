using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Audit;
using VOL.CERT.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
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