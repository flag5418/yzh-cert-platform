using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YZH.Core.Controllers.Basic;
using YZH.Core.Enums;
using YZH.Core.Filters;
using YZH.Entity.DomainModels;
using YZH.Sys.IServices;

namespace YZH.Sys.Controllers
{
    [Route("api/menu")]
    [ApiController, JWTAuthorize()]
    public partial class Sys_MenuController : ApiBaseController<ISys_MenuService>
    {
        private ISys_MenuService _service { get; set; }
        public Sys_MenuController(ISys_MenuService service) :
            base("System", "System", "Sys_Menu", service)
        {
            _service = service;
        } 
    }
}
