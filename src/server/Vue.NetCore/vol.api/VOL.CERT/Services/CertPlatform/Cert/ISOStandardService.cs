/*
 *Author：CertPlatform Generator
 *Contact：auto@certplatform.com
 *代码由框架生成,此处任何更改都可能导致被代码生成器覆盖
 *所有业务编写全部应在Partial文件夹下ISOStandardService与IISOStandardService中编写
 */
using VOL.CERT.IRepositories;
using VOL.CERT.IServices;
using VOL.CERT.IRepositories.CertPlatform;
using VOL.CERT.IServices.CertPlatform;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using YZH.Core;

namespace VOL.CERT.Services.CertPlatform
{
    public partial class ISOStandardService : YZHTableServiceBase<ISOStandard, IISOStandardRepository>
    , IISOStandardService, IDependency
    {
        public static IISOStandardService Instance
        {
            get { return AutofacContainerModule.GetService<IISOStandardService>(); }
        }
    }
}
