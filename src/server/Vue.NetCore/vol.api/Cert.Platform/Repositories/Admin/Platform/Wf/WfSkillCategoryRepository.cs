using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Wf;
using Cert.Platform.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Cert.Platform.Repositories.Admin.Platform.Wf
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
