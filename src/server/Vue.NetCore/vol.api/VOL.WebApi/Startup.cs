using Microsoft.Extensions.Configuration;
using VOL.Core.WorkFlow;
using VOL.Entity.DomainModels;

namespace VOL.WebApi
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
