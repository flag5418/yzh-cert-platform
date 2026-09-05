using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YZH.Core.BaseProvider;
using YZH.Entity.DomainModels;
using YZH.Core.Extensions.AutofacManager;
namespace YZH.Builder.IRepositories
{
    public partial interface ISys_TableInfoRepository : IDependency,IRepository<Sys_TableInfo>
    {
    }
}

