
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
/// 认证阶段定义管理控制器
///
/// 功能：通用五阶段定义（S1/S2/Surv1/Surv2/Recert）的增删改查
/// 路由前缀：/api/Foundation/PhaseDefinition
///
/// 数据库：cert_phase_definition
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Foundation/[controller]")]
public class PhaseDefinitionController : YzhControllerBase<PhaseDefinition>
{
    public PhaseDefinitionController(
        EntityService<PhaseDefinition> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>
    /// 加载 EntityConfig 配置
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<PhaseDefinition>();
    }

    /// <summary>新增前校验：PhaseCode 唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        PhaseDefinition entity)
    {
        var exists = await Entity.ExistsAsync(p => p.PhaseCode == entity.PhaseCode);
        if (exists.Data)
            return (false, $"阶段编码【{entity.PhaseCode}】已存在");
        return (true, null);
    }

    /// <summary>修改前校验：PhaseCode 唯一性（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        PhaseDefinition entity)
    {
        var exists = await Entity.ExistsAsync(p =>
            p.Code != entity.Code && p.PhaseCode == entity.PhaseCode);
        if (exists.Data)
            return (false, $"阶段编码【{entity.PhaseCode}】已存在");
        return (true, null);
    }
}
