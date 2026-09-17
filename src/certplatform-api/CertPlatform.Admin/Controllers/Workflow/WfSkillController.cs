extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;

namespace CertPlatform.Admin.Controllers.Workflow
{
    /// <summary>
    /// 技能管理 - 单表 CRUD
    /// <para>路由前缀：/api/Workflow/WfSkill</para>
    /// <para>继承 YzhControllerBase&lt;Skill&gt;，自动获得：</para>
    /// <para>  POST /filter        分页查询</para>
    /// <para>  POST /add           新增</para>
    /// <para>  POST /update        修改</para>
    /// <para>  POST /delete        删除</para>
    /// <para>  POST /toggle-valid  启用/禁用（IsValid 0↔1）</para>
    /// </summary>
    [ApiController]
    [Route("api/Workflow/[controller]")]
    public class WfSkillController : YzhControllerBase<SharedEntities::CertPlatform.Shared.Entities.Wf.Skill>
    {
        public WfSkillController(
            EntityService<SharedEntities::CertPlatform.Shared.Entities.Wf.Skill> entityService,
            IUserContext userContext)
            : base(entityService, userContext) { }

        protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
            SharedEntities::CertPlatform.Shared.Entities.Wf.Skill entity)
        {
            var result = await Entity.GetByCodeAny(entity.Code);
            if (result.Success && result.Data != null)
                return (false, $"编码 {entity.Code} 已存在");
            return (true, null);
        }

        protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
            SharedEntities::CertPlatform.Shared.Entities.Wf.Skill entity)
        {
            var result = await Entity.GetByCodeAny(entity.Code);
            if (result.Success && result.Data != null && result.Data.Code != entity.Code)
                return (false, $"编码 {entity.Code} 已被其他记录使用");
            return (true, null);
        }
    }
}
