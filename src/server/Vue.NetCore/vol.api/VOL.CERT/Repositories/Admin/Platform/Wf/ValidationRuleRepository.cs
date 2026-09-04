using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Cert;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
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
