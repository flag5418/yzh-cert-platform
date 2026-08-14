/*
 *代码由框架生成,任何更改都可能导致被代码生成器覆盖
 *如果要增加方法请在当前目录下Partial文件夹CertStageController编写
 */
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Entity.AttributeManager;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/CertStage")]
    [PermissionTable(Name = "CertStage")]
    public partial class CertStageController : ApiBaseController<ICertStageService>
    {
        public CertStageController(ICertStageService service)
        : base(service)
        {
        }
    }
}
