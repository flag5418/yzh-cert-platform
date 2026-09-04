using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Wf;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.CertPlatform
{
    public partial class WfSkillCategoryRepository : RepositoryBase<WfSkillCategory>, IWfSkillCategoryRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public WfSkillCategoryRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IWfSkillCategoryRepository Instance
        {
            get { return AutofacContainerModule.GetService<IWfSkillCategoryRepository>(); }
        }
    }
}
