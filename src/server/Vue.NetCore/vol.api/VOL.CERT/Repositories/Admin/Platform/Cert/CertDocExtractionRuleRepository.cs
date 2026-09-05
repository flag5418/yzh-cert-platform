using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.DocExtraction;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.Admin.Platform.Cert
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
