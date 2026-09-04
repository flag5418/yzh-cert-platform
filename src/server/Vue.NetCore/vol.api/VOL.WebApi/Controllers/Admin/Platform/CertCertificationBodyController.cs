/*
 *代码由框架生成,任何更改都可能导致被代码生成器覆盖
 *如果要增加方法请在当前目录下Partial文件夹CertCertificationBodyController编写
 */
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Entity.AttributeManager;
using VOL.CERT.IServices.Admin.Platform;

namespace VOL.WebApi.Controllers.Admin.Platform
{
    [Route("api/CertCertificationBody")]
    [PermissionTable(Name = "CertCertificationBody")]
    public partial class CertCertificationBodyController : ApiBaseController<ICertCertificationBodyService>
    {
        public CertCertificationBodyController(ICertCertificationBodyService service)
        : base(service)
        {
        }
    }
}
