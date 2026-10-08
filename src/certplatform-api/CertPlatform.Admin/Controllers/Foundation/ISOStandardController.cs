
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

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Foundation/[controller]")]
public class ISOStandardController : YzhControllerBase<ISOStandard>
{
    public ISOStandardController(
        EntityService<ISOStandard> entityService,
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
        return EntityConfigHelper.GetConfig<ISOStandard>();
    }

    // ========================================================
    // 业务校验
    // ========================================================

    /// <summary>
    /// 新增前校验：标准名称唯一 + 同版本下标准编号唯一
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        ISOStandard entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardName))
            return (false, "标准名称不能为空");

        var nameExists = await Entity.ExistsAsync(s =>
            s.StandardName == entity.StandardName &&
            s.VersionYear == entity.VersionYear);
        if (nameExists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准名称【{entity.StandardName}】已存在");

        var exists = await Entity.ExistsAsync(s =>
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    /// <summary>
    /// 修改前校验：标准名称+版本年份唯一（排除自身）+ 同版本下标准编号唯一（排除自身）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        ISOStandard entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardName))
            return (false, "标准名称不能为空");

        var nameExists = await Entity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardName == entity.StandardName &&
            s.VersionYear == entity.VersionYear);
        if (nameExists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准名称【{entity.StandardName}】已存在");

        var exists = await Entity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }
}
