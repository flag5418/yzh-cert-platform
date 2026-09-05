using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/departments")]
    [ApiController]
    public class SystemDepartmentController : YzhCrudController<SysDepartment>
    {
        public SystemDepartmentController(YzhService<SysDepartment> svc) : base(svc) { }

        [HttpPost("Add")]
        public override IActionResult Add([FromBody] SaveModel<SysDepartment> m)
        {
            if (m.MainData.DepartmentId == Guid.Empty)
                m.MainData.DepartmentId = Guid.NewGuid();
            m.MainData.CreateDate = DateTime.Now;
            return base.Add(m);
        }

        [HttpPost("Del")]
        public override IActionResult Del([FromBody] List<object> ids)
        {
            var guidIds = ids.Select(x => Guid.Parse(x.ToString())).ToList();
            return DelBy(x => guidIds.Contains(x.DepartmentId));
        }

        [HttpGet("tree")]
        public IActionResult Tree()
        {
            var all = _svc.GetAll().OrderBy(d => d.DepartmentName).ToList();
            return Ok(new YzhApiResult { Data = all });
        }
    }
}
