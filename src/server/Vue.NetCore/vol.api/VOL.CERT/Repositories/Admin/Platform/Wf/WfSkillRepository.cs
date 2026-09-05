using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Wf;
using VOL.CERT.IRepositories.Admin.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace VOL.CERT.Repositories.Admin.Platform.Wf
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
