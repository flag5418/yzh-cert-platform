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
///     ISO 标准管理控制器
///     
///     功能：ISO 标准的增删改查（单表 CRUD）
///     路由前缀：/api/Foundation/ISOStandard
///     
///     继承 YzhControllerBase 获得能力：
///     - GET    /config              获取页面配置（EntityConfig）
///     - POST   /filter              分页查询
///     - POST   /add                 新增
///     - POST   /update              修改
///     - POST   /delete              软删除
///     - POST   /toggle-valid        切换有效标志
///     - POST   /export              导出
///     - POST   /import              批量导入
///     - POST   /action/{methodName} 自定义行操作
///     
///     数据库：cert_iso_standard
///     
///     前端路由映射：
///     - path: '/cert/iso-standard'
///     - component: '@/pages/foundation/iso-standard/index.vue'
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class ISOStandardController : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard>
{
    public ISOStandardController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    // ========================================================
    // 自定义配置
    // ========================================================

    /// <summary>
    /// 加载 EntityConfig 配置
    /// 配置文件路径：Assets/EntityConfigs/Foundation/ISOStandard.json
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard>();
    }

    // ========================================================
    // 业务校验
    // ========================================================

    /// <summary>
    /// 新增前校验：同版本下标准编号唯一
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard entity)
    {
        var exists = await Entity.ExistsAsync(s =>
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    /// <summary>
    /// 修改前校验：同版本下标准编号唯一（排除自身）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard entity)
    {
        var exists = await Entity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }
}
