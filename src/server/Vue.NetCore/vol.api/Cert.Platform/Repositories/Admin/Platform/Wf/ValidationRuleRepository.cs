using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Wf
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
