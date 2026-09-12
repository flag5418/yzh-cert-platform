extern alias SharedEntities;

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
[Route("api/Foundation/[controller]")]
public class PhaseDefinitionController : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.PhaseDefinition>
{
    public PhaseDefinitionController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.PhaseDefinition> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>
    /// 加载 EntityConfig 配置
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<SharedEntities::YZH.Entity.Admin.Platform.Cert.PhaseDefinition>();
    }

    /// <summary>新增前校验：PhaseCode 唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.PhaseDefinition entity)
    {
        var exists = await Entity.ExistsAsync(p => p.PhaseCode == entity.PhaseCode);
        if (exists.Data)
            return (false, $"阶段编码【{entity.PhaseCode}】已存在");
        return (true, null);
    }

    /// <summary>修改前校验：PhaseCode 唯一性（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.PhaseDefinition entity)
    {
        var exists = await Entity.ExistsAsync(p =>
            p.Code != entity.Code && p.PhaseCode == entity.PhaseCode);
        if (exists.Data)
            return (false, $"阶段编码【{entity.PhaseCode}】已存在");
        return (true, null);
    }
}
