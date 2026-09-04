/*
 *代码由框架生成,任何更改都可能导致被代码生成器覆盖
 *如果要增加方法请在当前目录下Partial文件夹AuthController编写
 *
 * 审核员端认证 Controller
 * - 注册接口：POST /api/AuditorAuth/Register（AllowAnonymous）
 * - 登录接口：复用 Vol 原生 /api/User/login，前端根据 Role_Id 判断跳转
 */
using Microsoft.AspNetCore.Mvc;
using VOL.Core.Controllers.Basic;
using VOL.Sys.IServices;

namespace VOL.WebApi.Controllers.Auditor
{
    [Route("api/AuditorAuth")]
    public partial class AuthController : ApiBaseController<ISys_UserService>
    {
        public AuthController(ISys_UserService service)
        : base(service)
        {
        }
    }
}
