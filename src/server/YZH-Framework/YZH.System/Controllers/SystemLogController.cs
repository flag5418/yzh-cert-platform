using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/logs")]
    [ApiController]
    public class SystemLogController : CrudController<SysLog>
    {
        public SystemLogController(DomainService<SysLog> svc) : base(svc) { }

        // 日志由系统自动产生，禁止通过界面新增/修改
        public override IActionResult Add([FromBody] SaveModel<SysLog> m)
            => Ok(new ApiResult { Status = false, Msg = "系统日志不允许手动新增" });

        public override IActionResult Update([FromBody] SaveModel<SysLog> m)
            => Ok(new ApiResult { Status = false, Msg = "系统日志不允许手动修改" });

        [HttpPost("Del")]
        public override IActionResult Del([FromBody] List<object> ids)
        {
            var longIds = ids.Select(x => Convert.ToInt64(x)).ToList();
            return DelBy(x => longIds.Contains(x.Id));
        }
    }
}
