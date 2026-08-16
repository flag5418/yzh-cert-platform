using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
{
    public partial class ValidationRuleRepository : RepositoryBase<ValidationRule>, IValidationRuleRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public ValidationRuleRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IValidationRuleRepository Instance
        {
            get { return AutofacContainerModule.GetService<IValidationRuleRepository>(); }
        }
    }
}
