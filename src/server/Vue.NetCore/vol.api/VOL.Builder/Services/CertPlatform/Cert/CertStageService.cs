/*
 *Author：CertPlatform Generator
 *Contact：auto@certplatform.com
 *代码由框架生成,此处任何更改都可能导致被代码生成器覆盖
 *所有业务编写全部应在Partial文件夹下CertStageService与ICertStageService中编写
 */
using VOL.Builder.IRepositories;
using VOL.Builder.IServices;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.Services.CertPlatform
{
    public partial class CertStageService : ServiceBase<CertStage, ICertStageRepository>
    , ICertStageService, IDependency
    {
        public static ICertStageService Instance
        {
            get { return AutofacContainerModule.GetService<ICertStageService>(); }
        }
    }
}
