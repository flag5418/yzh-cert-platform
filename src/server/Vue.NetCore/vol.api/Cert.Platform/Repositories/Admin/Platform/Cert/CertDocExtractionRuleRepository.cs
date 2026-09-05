using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.DocExtraction;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Cert
{
    public partial class CertDocExtractionRuleRepository : RepositoryBase<CertDocExtractionRule>, ICertDocExtractionRuleRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public CertDocExtractionRuleRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static ICertDocExtractionRuleRepository Instance
        {
            get { return AutofacContainerModule.GetService<ICertDocExtractionRuleRepository>(); }
        }
    }
}
