using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Cert;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.Admin.Platform.Cert
{
  public partial class ISOStandardRepository : RepositoryBase<ISOStandard>, IISOStandardRepository, IDependency
  {
    [ActivatorUtilitiesConstructor]
    public ISOStandardRepository(VOLContext dbContext)
    : base(dbContext)
    {

    }
    public static IISOStandardRepository Instance
    {
      get { return AutofacContainerModule.GetService<IISOStandardRepository>(); }
    }
  }
}