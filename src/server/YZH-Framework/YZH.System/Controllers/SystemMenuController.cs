using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/menus")]
    [ApiController]
    public class SystemMenuController : CrudController<SysMenu>
    {
        public SystemMenuController(DomainService<SysMenu> svc) : base(svc) { }

        [HttpPost("Del")]
        public override IActionResult Del([FromBody] List<object> ids)
        {
            var intIds = ids.Select(x => Convert.ToInt32(x)).ToList();
            return DelBy(x => intIds.Contains(x.Menu_Id));
        }

        /// <summary>
        /// 菜单树（包含全部菜单，前端自行构建层级）
        /// </summary>
        [HttpGet("tree")]
        public IActionResult Tree()
        {
            var all = _svc.GetAll().OrderBy(m => m.OrderNo ?? 0).ThenBy(m => m.Menu_Id).ToList();
            return Ok(new ApiResult { Data = all });
        }
    }
}
