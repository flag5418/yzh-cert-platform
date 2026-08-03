using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
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