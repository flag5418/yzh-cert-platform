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
    public partial class Sys_WorkFlowTableStepService : ServiceBase<Sys_WorkFlowTableStep, ISys_WorkFlowTableStepRepository>
    , ISys_WorkFlowTableStepService, IDependency
    {
    public Sys_WorkFlowTableStepService(ISys_WorkFlowTableStepRepository repository)
    : base(repository)
    {
    Init(repository);
    }
    public static ISys_WorkFlowTableStepService Instance
    {
      get { return AutofacContainerModule.GetService<ISys_WorkFlowTableStepService>(); } }
    }
 }
