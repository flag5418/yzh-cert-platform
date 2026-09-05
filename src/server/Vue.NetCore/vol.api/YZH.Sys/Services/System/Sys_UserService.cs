// ──────────────────────────────────────────────
// 映智汇体系认证平台 · 衍生自 vol 框架（MIT）
// 版权声明与来源说明见仓库根 LICENSE / NOTICE.md
// ──────────────────────────────────────────────

using YZH.Sys.IRepositories;
using YZH.Sys.IServices;
using YZH.Core.BaseProvider;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.DomainModels;

namespace YZH.Sys.Services
{
    public partial class Sys_UserService : ServiceBase<Sys_User, ISys_UserRepository>, ISys_UserService, IDependency
    {
        public Sys_UserService(ISys_UserRepository repository)
             : base(repository) 
        { 
           Init(repository);
        }
        public static ISys_UserService Instance
        {
           get { return AutofacContainerModule.GetService<ISys_UserService>(); }
        }
    }
}

