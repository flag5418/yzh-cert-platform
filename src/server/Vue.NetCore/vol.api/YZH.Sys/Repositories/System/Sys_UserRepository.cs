// ──────────────────────────────────────────────
// 映智汇体系认证平台 · 衍生自 vol 框架（MIT）
// 版权声明与来源说明见仓库根 LICENSE / NOTICE.md
// ──────────────────────────────────────────────

using YZH.Sys.IRepositories;
using YZH.Core.BaseProvider;
using YZH.Core.EFDbContext;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.DomainModels;

namespace YZH.Sys.Repositories
{
    public partial class Sys_UserRepository : RepositoryBase<Sys_User>, ISys_UserRepository
    {
        public Sys_UserRepository(VOLContext dbContext)
        : base(dbContext)
        {

        }
        public static ISys_UserRepository Instance
        {
            get { return AutofacContainerModule.GetService<ISys_UserRepository>(); }
        }
    }
}

