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
    public partial class Sys_QuartzOptionsService : ServiceBase<Sys_QuartzOptions, ISys_QuartzOptionsRepository>
    , ISys_QuartzOptionsService, IDependency
    {
    public Sys_QuartzOptionsService(ISys_QuartzOptionsRepository repository)
    : base(repository)
    {
    Init(repository);
    }
    public static ISys_QuartzOptionsService Instance
    {
      get { return AutofacContainerModule.GetService<ISys_QuartzOptionsService>(); } }
    }
 }
