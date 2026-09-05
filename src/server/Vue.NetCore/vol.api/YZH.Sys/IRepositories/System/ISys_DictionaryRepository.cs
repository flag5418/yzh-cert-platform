// ──────────────────────────────────────────────
// 映智汇体系认证平台 · 衍生自 vol 框架（MIT）
// 版权声明与来源说明见仓库根 LICENSE / NOTICE.md
// ──────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YZH.Core.BaseProvider;
using YZH.Entity.DomainModels;
using YZH.Core.Extensions.AutofacManager;
namespace YZH.Sys.IRepositories
{
    public partial interface ISys_DictionaryRepository : IDependency,IRepository<Sys_Dictionary>
    {
    }
}

