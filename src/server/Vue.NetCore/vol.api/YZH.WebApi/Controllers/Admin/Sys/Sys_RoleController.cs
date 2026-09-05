using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using YZH.Core.Controllers.Basic;
using YZH.Core.Enums;
using YZH.Core.Filters;
using YZH.Entity.AttributeManager;
using YZH.Entity.DomainModels;
using YZH.Sys.IServices;

namespace YZH.Sys.Controllers
{
    [Route("api/Sys_Role")]
    [PermissionTable(Name = "Sys_Role")]
    public partial class Sys_RoleController : ApiBaseController<ISys_RoleService>
    {
        public Sys_RoleController(ISys_RoleService service)
        : base("System", "System", "Sys_Role", service)
        {

        }
    }
}


