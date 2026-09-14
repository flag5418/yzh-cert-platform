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
/// 认证阶段管理控制器（全局基础资料）
///
/// 功能：ISO/IEC 17021-1:2015 九阶段认证流程的增删改查
/// 路由前缀：/api/Foundation/CertStage
///
/// 数据库：cert_cert_stage
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class CertStageController : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.CertStage>
{
    public CertStageController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.CertStage> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>
    /// 加载 EntityConfig 配置
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<SharedEntities::YZH.Entity.Admin.Platform.Cert.CertStage>();
    }

    /// <summary>新增前校验：StageCode 唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.CertStage entity)
    {
        var exists = await Entity.ExistsAsync(p => p.StageCode == entity.StageCode);
        if (exists.Data)
            return (false, $"阶段编码【{entity.StageCode}】已存在");
        return (true, null);
    }

    /// <summary>修改前校验：StageCode 唯一性（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.CertStage entity)
    {
        var exists = await Entity.ExistsAsync(p =>
            p.Code != entity.Code && p.StageCode == entity.StageCode);
        if (exists.Data)
            return (false, $"阶段编码【{entity.StageCode}】已存在");
        return (true, null);
    }
}
