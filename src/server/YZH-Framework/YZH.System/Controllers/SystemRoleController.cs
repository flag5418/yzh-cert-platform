using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/roles")]
    [ApiController]
    public class SystemRoleController : YzhCrudController<SysRole>
    {
        public SystemRoleController(YzhService<SysRole> svc) : base(svc) { }

        [HttpPost("Del")]
        public override IActionResult Del([FromBody] List<object> ids)
        {
            var intIds = ids.Select(x => Convert.ToInt32(x)).ToList();
            return DelBy(x => intIds.Contains(x.Role_Id));
        }
    }
}
