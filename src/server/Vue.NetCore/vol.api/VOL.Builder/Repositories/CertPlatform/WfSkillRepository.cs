using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IRepositories.CertPlatform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.Builder.Repositories.CertPlatform
{
    public partial class WfSkillRepository : RepositoryBase<Skill>, IWfSkillRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public WfSkillRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IWfSkillRepository Instance
        {
            get { return AutofacContainerModule.GetService<IWfSkillRepository>(); }
        }
    }
}
