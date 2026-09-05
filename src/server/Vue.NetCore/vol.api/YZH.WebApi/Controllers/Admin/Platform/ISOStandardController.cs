/*
 *代码由框架生成,任何更改都可能导致被代码生成器覆盖
 *如果要增加方法请在当前目录下Partial文件夹ISOStandardController编写
 */
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Controllers.Basic;
using YZH.Entity.AttributeManager;
using Cert.Platform.IServices.Admin.Platform;

namespace YZH.WebApi.Controllers.Admin.Platform
{
    [Route("api/ISOStandard")]
    [PermissionTable(Name = "ISOStandard")]
    public partial class ISOStandardController : ApiBaseController<IISOStandardService>
    {
        public ISOStandardController(IISOStandardService service)
        : base(service)
        {
        }
    }
}
