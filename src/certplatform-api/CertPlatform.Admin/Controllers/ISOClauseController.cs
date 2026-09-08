extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;

namespace CertPlatform.Admin.Controllers;

/// <summary>
///     ISO 标准条款管理控制器
///     
///     功能：标准条款的增删改查（支持树形结构：标准 → 条款 → 子条款）
///     路由前缀：/api/ISOClause
///     
///     继承 YzhControllerBase 获得能力：
///     - GET    /config              获取页面配置（EntityConfig）
///     - POST   /filter              分页查询
///     - POST   /add                 新增
///     - POST   /update              修改
///     - POST   /delete              软删除
///     - POST   /action/{methodName} 自定义行操作
///     
///     数据库：cert_iso_clause（delete_time IS NULL 过滤软删除）
///     
///     前端路由映射（certplatform-web/admin/src/router/）：
///     - path: '/iso-clause'
///     - component: '@/pages/cert/iso-clause/index.vue'
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ISOClauseController : YzhControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause>
{
    public ISOClauseController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    // ========================================================
    // 自定义配置
    // ========================================================

    /// <summary>
    /// 加载配置（从 EntityConfigHelper 获取）
    /// 配置文件路径：Assets/EntityConfigs/ISOClause.json
    /// 如配置文件不存在，EntityConfigHelper 会自动返回默认空配置
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause>();
    }

    // ========================================================
    // 业务校验
    // ========================================================

    /// <summary>新增前校验：同标准下条款编号唯一性</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause entity)
    {
        var exists = await Entity.ExistsAsync(c =>
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        if (!string.IsNullOrEmpty(entity.ParentCode))
        {
            var parentExists = await Entity.ExistsAsync(c => c.Code == entity.ParentCode);
            if (!parentExists.Data)
                return (false, "指定的父条款不存在");
        }

        return (true, null);
    }

    /// <summary>修改前校验：同标准下条款编号唯一性（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause entity)
    {
        var exists = await Entity.ExistsAsync(c =>
            c.Code != entity.Code &&
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return (true, null);
    }
}
