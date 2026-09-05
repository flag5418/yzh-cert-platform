using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using YZH.System.Entities;

namespace YZH.System.Controllers
{
    [Route("api/yzh/sys/role-auth")]
    [ApiController]
    public class SystemRoleAuthController : ControllerBase
    {
        private readonly YzhService<SysRoleAuth> _authSvc;
        public SystemRoleAuthController(YzhService<SysRoleAuth> authSvc)
        {
            _authSvc = authSvc;
        }

        /// <summary>
        /// 获取某角色的权限配置
        /// </summary>
        [HttpGet]
        public IActionResult GetByRole([FromQuery] int roleId)
        {
            var list = _authSvc.GetAll()
                .Where(x => x.Role_Id == roleId)
                .Cast<object>()
                .ToList();
            return Ok(new YzhApiResult { Rows = list, Total = list.Count });
        }

        /// <summary>
        /// 保存某角色的菜单权限（整体替换）
        /// body: { roleId, items: [ { menuId, authValue } ] }
        /// </summary>
        [HttpPost("save")]
        public IActionResult Save([FromBody] RoleAuthSaveModel model)
        {
            try
            {
                // 删除旧权限
                _authSvc.Delete(x => x.Role_Id == model.roleId);
                // 写入新权限
                foreach (var it in model.items ?? new List<RoleAuthItem>())
                {
                    _authSvc.Add(new SysRoleAuth
                    {
                        Role_Id = model.roleId,
                        Menu_Id = it.menuId,
                        AuthValue = it.authValue ?? "",
                        CreateDate = DateTime.Now
                    });
                }
                return Ok(new YzhApiResult());
            }
            catch (Exception ex)
            {
                return Ok(new YzhApiResult { Status = false, Msg = ex.Message });
            }
        }
    }

    public class RoleAuthSaveModel
    {
        public int roleId { get; set; }
        public List<RoleAuthItem> items { get; set; }
    }
    public class RoleAuthItem
    {
        public int menuId { get; set; }
        public string authValue { get; set; }
    }
}
