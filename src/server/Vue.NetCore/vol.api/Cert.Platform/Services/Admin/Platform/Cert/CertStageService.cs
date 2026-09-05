/*
 *Author：CertPlatform Generator
 *Contact：auto@certplatform.com
 *代码由框架生成,此处任何更改都可能导致被代码生成器覆盖
 *所有业务编写全部应在Partial文件夹下CertStageService与ICertStageService中编写
 */
using Cert.Platform.IRepositories;
using Cert.Platform.IServices;
using Cert.Platform.IRepositories.Admin.Platform;
using Cert.Platform.IServices.Admin.Platform;
using YZH.Core.BaseProvider;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity.Admin.Platform.Cert;
using YZH.Core;

namespace Cert.Platform.Services.Admin.Platform
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
