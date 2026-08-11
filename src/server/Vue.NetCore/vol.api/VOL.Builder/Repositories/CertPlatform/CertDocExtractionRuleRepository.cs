using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.DocExtraction;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
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
