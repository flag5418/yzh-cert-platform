/*
 *Author：CertPlatform Generator
 *Contact：auto@certplatform.com
 *代码由框架生成,此处任何更改都可能导致被代码生成器覆盖
 *所有业务编写全部应在Partial文件夹下AuditTaskService与IAuditTaskService中编写
 */
using VOL.Builder.IRepositories;
using VOL.Builder.IServices;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Audit;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class AuditTaskService : ServiceBase<AuditTask, IAuditTaskRepository>
    , IAuditTaskService, IDependency
    {
        public static IAuditTaskService Instance
        {
            get { return AutofacContainerModule.GetService<IAuditTaskService>(); }
        }
    }
}
