using Microsoft.Extensions.Configuration;
using YZH.Core.WorkFlow;
using YZH.Entity.DomainModels;

namespace YZH.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            //初始化流程表，表里面必须有AuditStatus字段
            WorkFlowContainer.Instance
                //run方法必须写在最后位置
                .Run();
        }
    }
}
