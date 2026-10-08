using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Admin.Entities.Cert;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
///     标准族管理控制器（体系 → 族 → 版本 三层中的「族」层）
///
///     功能：ISO 标准族的增删改查（单表 CRUD）
///     路由前缀：/api/Admin/Foundation/CertStandardFamily
///
///     继承 YzhControllerBase 获得能力：
///     - GET    /config              获取页面配置（EntityConfig）
///     - POST   /filter              分页查询
///     - POST   /add                 新增
///     - POST   /update              修改
///     - POST   /delete              软删除
///     - POST   /toggle-valid        切换有效标志
///     - POST   /export              导出
///
///     数据库：cert_standard_family
///
///     业务规则：
///     1. FamilyNo（族人读编号）全局唯一
///     2. Category + FamilyName 组合唯一
///     3. 删除族不级联删版本（版本 FamilyCode 置空由调用方处理，本控制器仅软删族）
///
///     前端路由映射：'/cert/standard-manage' → pages/foundation/standard-manage/index.vue
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名。⚠️ 本控制器为<b>新增</b>控制器 ⇒ 启动时
/// <c>ApiSyncService.SyncAsync</c>（Program.cs:284）会自动登记到 <c>sys_api</c>，
/// 但 <c>sys_role_api</c> 角色关联需在「角色-接口管理」页补关联（或 SQL 直插）。</para>
[Route("api/Admin/Foundation/[controller]")]
public class CertStandardFamilyController : YzhControllerBase<CertStandardFamily>
{
    public CertStandardFamilyController(
        EntityService<CertStandardFamily> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    /// <summary>
    /// 加载 EntityConfig 配置
    /// 配置文件：Assets/EntityConfigs/Foundation/CertStandardFamily.json
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<CertStandardFamily>();
    }

    /// <summary>新增前校验：FamilyNo 全局唯一 + Category+FamilyName 组合唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        CertStandardFamily entity)
    {
        if (string.IsNullOrWhiteSpace(entity.FamilyNo))
            return (false, "族编号不能为空");
        if (string.IsNullOrWhiteSpace(entity.FamilyName))
            return (false, "族名称不能为空");
        if (string.IsNullOrWhiteSpace(entity.Category))
            return (false, "所属体系不能为空");

        var noExists = await Entity.ExistsAsync(f => f.FamilyNo == entity.FamilyNo);
        if (noExists.Data)
            return (false, $"族编号【{entity.FamilyNo}】已存在");

        var nameExists = await Entity.ExistsAsync(f =>
            f.Category == entity.Category && f.FamilyName == entity.FamilyName);
        if (nameExists.Data)
            return (false, $"该体系下族名称【{entity.FamilyName}】已存在");

        return (true, null);
    }

    /// <summary>修改前校验：FamilyNo 唯一（排除自身）+ Category+FamilyName 组合唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        CertStandardFamily entity)
    {
        if (string.IsNullOrWhiteSpace(entity.FamilyNo))
            return (false, "族编号不能为空");
        if (string.IsNullOrWhiteSpace(entity.FamilyName))
            return (false, "族名称不能为空");
        if (string.IsNullOrWhiteSpace(entity.Category))
            return (false, "所属体系不能为空");

        var noExists = await Entity.ExistsAsync(f =>
            f.Code != entity.Code && f.FamilyNo == entity.FamilyNo);
        if (noExists.Data)
            return (false, $"族编号【{entity.FamilyNo}】已存在");

        var nameExists = await Entity.ExistsAsync(f =>
            f.Code != entity.Code &&
            f.Category == entity.Category && f.FamilyName == entity.FamilyName);
        if (nameExists.Data)
            return (false, $"该体系下族名称【{entity.FamilyName}】已存在");

        return (true, null);
    }
}
