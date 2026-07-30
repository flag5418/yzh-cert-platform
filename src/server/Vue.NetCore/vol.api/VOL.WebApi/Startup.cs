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
               .Use<MES_ProductionReporting>(
                 "生产报工",
                    filterFields: x => new { x.ReportingNumber, x.AcceptedQuantity, x.RejectedQuantity, x.Total, x.ReportedBy, x.ReportingTime },
                    //审批界面显示表数据字段
                    formFields: x => new { x.ReportedBy, x.ReportingNumber, x.ReportingTime, x.AcceptedQuantity, x.RejectedQuantity, x.Total }
                )
                //run方法必须写在最后位置
                .Run();
        }
    }
}
