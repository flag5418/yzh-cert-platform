using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
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
