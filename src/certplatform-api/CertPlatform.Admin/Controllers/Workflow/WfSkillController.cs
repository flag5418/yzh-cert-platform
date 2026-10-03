
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
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
    [Route("api/Admin/Workflow/[controller]")]
    public class WfSkillController : YzhControllerBase<CertPlatform.Admin.Entities.Wf.Skill>
    {
        public WfSkillController(
            EntityService<CertPlatform.Admin.Entities.Wf.Skill> entityService,
            IUserContext userContext)
            : base(entityService, userContext) { }

        protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
            CertPlatform.Admin.Entities.Wf.Skill entity)
        {
            var result = await Entity.GetByCodeAny(entity.Code);
            if (result.Success && result.Data != null)
                return (false, $"编码 {entity.Code} 已存在");
            return (true, null);
        }

        protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
            CertPlatform.Admin.Entities.Wf.Skill entity)
        {
            var result = await Entity.GetByCodeAny(entity.Code);
            if (result.Success && result.Data != null && result.Data.Code != entity.Code)
                return (false, $"编码 {entity.Code} 已被其他记录使用");
            return (true, null);
        }
    }
}
