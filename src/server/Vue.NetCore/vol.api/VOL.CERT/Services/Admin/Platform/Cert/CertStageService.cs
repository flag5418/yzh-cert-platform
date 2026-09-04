/*
 *Author：CertPlatform Generator
 *Contact：auto@certplatform.com
 *代码由框架生成,此处任何更改都可能导致被代码生成器覆盖
 *所有业务编写全部应在Partial文件夹下CertStageService与ICertStageService中编写
 */
using VOL.CERT.IRepositories;
using VOL.CERT.IServices;
using VOL.CERT.IRepositories.Admin.Platform;
using VOL.CERT.IServices.Admin.Platform;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.Admin.Platform.Cert;
using YZH.Core;

namespace VOL.CERT.Services.Admin.Platform
{
    public partial class CertStageService : YZHTableServiceBase<CertStage, ICertStageRepository>
    , ICertStageService, IDependency
    {
        public static ICertStageService Instance
        {
            get { return AutofacContainerModule.GetService<ICertStageService>(); }
        }
    }
}
