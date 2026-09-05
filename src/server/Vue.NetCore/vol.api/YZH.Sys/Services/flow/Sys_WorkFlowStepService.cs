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
    public partial class Sys_WorkFlowStepService : ServiceBase<Sys_WorkFlowStep, ISys_WorkFlowStepRepository>
    , ISys_WorkFlowStepService, IDependency
    {
    public Sys_WorkFlowStepService(ISys_WorkFlowStepRepository repository)
    : base(repository)
    {
    Init(repository);
    }
    public static ISys_WorkFlowStepService Instance
    {
      get { return AutofacContainerModule.GetService<ISys_WorkFlowStepService>(); } }
    }
 }
