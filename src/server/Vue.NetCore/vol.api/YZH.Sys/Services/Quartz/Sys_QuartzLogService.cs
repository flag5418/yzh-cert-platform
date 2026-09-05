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
    public partial class Sys_QuartzLogService : ServiceBase<Sys_QuartzLog, ISys_QuartzLogRepository>
    , ISys_QuartzLogService, IDependency
    {
    public Sys_QuartzLogService(ISys_QuartzLogRepository repository)
    : base(repository)
    {
    Init(repository);
    }
    public static ISys_QuartzLogService Instance
    {
      get { return AutofacContainerModule.GetService<ISys_QuartzLogService>(); } }
    }
 }
