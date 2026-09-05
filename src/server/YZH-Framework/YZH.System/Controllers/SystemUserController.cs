using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/users")]
    [ApiController]
    public class SystemUserController : YzhCrudController<SysUser>
    {
        public SystemUserController(YzhService<SysUser> svc) : base(svc) { }

        public override IActionResult GetPageData([FromBody] PageQuery q)
        {
            var (rows, total) = _svc.GetPage(q);
            // 脱敏：列表/详情不返回密码
            var safe = rows.Select(StripPwd).Cast<object>().ToList();
            return Ok(new YzhApiResult { Rows = safe, Total = total });
        }

        [HttpPost("Add")]
        public override IActionResult Add([FromBody] SaveModel<SysUser> m)
        {
            if (m.MainData.UserPwd == null) m.MainData.UserPwd = "";
            if (m.MainData.Enable == 0 && m.MainData.User_Id == 0) m.MainData.Enable = 1;
            if (m.MainData.UserType == 0) m.MainData.UserType = 10;
            m.MainData.CreateDate = DateTime.Now;
            return base.Add(m);
        }

        [HttpPost("Update")]
        public override IActionResult Update([FromBody] SaveModel<SysUser> m)
        {
            m.MainData.ModifyDate = DateTime.Now;
            return base.Update(m);
        }

        [HttpPost("Del")]
        public override IActionResult Del([FromBody] List<object> ids)
        {
            var intIds = ids.Select(x => Convert.ToInt32(x)).ToList();
            return DelBy(x => intIds.Contains(x.User_Id));
        }

        private static SysUser StripPwd(SysUser u)
        {
            u.UserPwd = null;
            return u;
        }
    }
}
