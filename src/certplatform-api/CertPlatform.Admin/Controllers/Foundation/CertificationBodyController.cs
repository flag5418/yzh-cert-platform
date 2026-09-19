
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Organization;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Shared.Entities.Cert;

using CB = CertPlatform.Shared.Entities.Cert.CertificationBody;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
/// 认证机构管理控制器（机构-Attach 试点）
///
/// <para>路由前缀：/api/Foundation/CertificationBody</para>
/// <para>数据库：cert_certification_body（业务主体）+ Sys_Organization（权限主体）</para>
///
/// <para>形态：**标准单表 CRUD**（继承 YzhControllerBase，其余能力全部复用）＋ 两个偏差：</para>
/// <list type="number">
///   <item>偏差 1：新增/修改/删除/启停时，在**同一事务内**同步系统机构记录 Sys_Organization</item>
///   <item>偏差 2：实体已重声明审计与软删除字段（见 CertificationBody.cs）</item>
/// </list>
///
/// <para>机构-Attach 契约：</para>
/// <list type="bullet">
///   <item>Attach.Code == Attach.OrgCode == Sys_Organization.Code</item>
///   <item>挂载点 = Sys_Organization 中 OrgType='CertBody' 且无父节点的根节点</item>
///   <item>Name→OrgName、CbCode→OrgCode、IsValid→Enable</item>
/// </list>
///
/// <para>事务说明：IDbOrm 与 EntityService&lt;T&gt; 均为 Scoped（YzhWebBuilder.cs:49/63），
/// 同一请求内共享同一个 SqlSugarClient，故 _orgService 与 Entity 的写入落在同一事务中。</para>
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class CertificationBodyController : YzhControllerBase<CB>
{
    private readonly EntityService<Sys_Organization> _orgService;
    private readonly IDbOrm _dbOrm;

    /// <summary>认证机构在机构树中的层级（根节点为 1）</summary>
    private const int CertBodyOrgLevel = 2;

    public CertificationBodyController(
        EntityService<CB> entityService,
        EntityService<Sys_Organization> orgService,
        IDbOrm dbOrm,
        IUserContext userContext)
        : base(entityService, userContext)
    {
        _orgService = orgService;
        _dbOrm = dbOrm;
    }

    // ========================================================
    // 一、配置
    // ========================================================

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig() => EntityConfigHelper.GetConfig<CB>();

    /// <summary>缺 JSON 配置时直接抛错，开发期暴露（避免空白页静默）</summary>
    protected override bool StrictConfigLoad => true;

    // ========================================================
    // 二、唯一性校验（新增/修改）
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(CB entity)
    {
        var nameExists = await Entity.ExistsAsync(p => p.Name == entity.Name);
        if (nameExists.Data)
            return (false, $"机构名称【{entity.Name}】已存在");

        if (!string.IsNullOrWhiteSpace(entity.CbCode))
        {
            var cbExists = await Entity.ExistsAsync(p => p.CbCode == entity.CbCode);
            if (cbExists.Data)
                return (false, $"机构编号【{entity.CbCode}】已存在");
        }

        return (true, null);
    }

    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(CB entity)
    {
        var nameExists = await Entity.ExistsAsync(p =>
            p.Code != entity.Code && p.Name == entity.Name);
        if (nameExists.Data)
            return (false, $"机构名称【{entity.Name}】已存在");

        if (!string.IsNullOrWhiteSpace(entity.CbCode))
        {
            var cbExists = await Entity.ExistsAsync(p =>
                p.Code != entity.Code && p.CbCode == entity.CbCode);
            if (cbExists.Data)
                return (false, $"机构编号【{entity.CbCode}】已存在");
        }

        return (true, null);
    }

    // ========================================================
    // 三、偏差 1：事务内同步 Sys_Organization
    // ========================================================

    /// <summary>新增认证机构：事务内先建机构主体记录，再建 Attach 记录</summary>
    public override async Task<Result<CB>> AddCore(CB entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");

        // 单 Code 契约
        entity.OrgCode = entity.Code;

        if (string.IsNullOrEmpty(entity.Status))
            entity.Status = entity.IsValid == 1 ? "active" : "inactive";

        var rootCode = await ResolveCertBodyRootCodeAsync();
        if (string.IsNullOrEmpty(rootCode))
            return Result<CB>.Fail(
                "未找到认证机构根节点：Sys_Organization 中不存在 OrgType='CertBody' 且无父节点的记录，请先初始化机构树");

        var orgDup = await _orgService.ExistsAsync(o => o.Code == entity.Code);
        if (orgDup.Data)
            return Result<CB>.Fail($"系统机构记录 {entity.Code} 已存在，无法重复创建");

        var org = BuildOrgNode(entity, rootCode);

        using var tx = _dbOrm.BeginTransaction();

        var orgResult = await _orgService.Insert(org, UserContext.ClientIp);
        if (!orgResult.Success)
        {
            tx.Rollback();
            return Result<CB>.Fail($"创建系统机构记录失败：{orgResult.Error}");
        }

        // 复用基类的校验 / 钩子 / 审计填充 / 软删除策略
        var result = await base.AddCore(entity);
        if (!result.Success)
        {
            tx.Rollback();
            return result;
        }

        tx.Commit();
        return result;
    }

    /// <summary>修改认证机构：事务内更新 Attach，并同步机构主体名称/编号/启用状态</summary>
    public override async Task<Result<CB>> UpdateCore(CB entity)
    {
        var existing = await Entity.GetByCode(entity.Code);
        if (!existing.Success || existing.Data == null)
            return Result<CB>.Fail($"认证机构 {entity.Code} 不存在");

        // 前端可能不回传隔离键，这里补齐
        if (string.IsNullOrEmpty(entity.OrgCode))
            entity.OrgCode = string.IsNullOrEmpty(existing.Data.OrgCode)
                ? existing.Data.Code
                : existing.Data.OrgCode;

        using var tx = _dbOrm.BeginTransaction();

        var result = await base.UpdateCore(entity);
        if (!result.Success)
        {
            tx.Rollback();
            return result;
        }

        var (ok, msg) = await SyncOrgAsync(result.Data!);
        if (!ok)
        {
            tx.Rollback();
            return Result<CB>.Fail(msg ?? "同步系统机构记录失败");
        }

        tx.Commit();
        return result;
    }

    /// <summary>删除认证机构：机构下有子机构则拒绝，否则事务内软删 Attach 与机构主体</summary>
    public override async Task<Result<int>> DeleteCore(params string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return Result<int>.Fail("未指定要删除的记录");

        // 前置校验：存在子机构时整体拒绝，避免产生孤儿数据
        foreach (var code in codes)
        {
            var childCount = await _orgService.CountAsync(o => o.ParentCode == code);
            if (childCount.Success && childCount.Data > 0)
                return Result<int>.Fail($"该认证机构下有 {childCount.Data} 个子机构，请先处理子机构");
        }

        using var tx = _dbOrm.BeginTransaction();

        var result = await base.DeleteCore(codes);
        if (!result.Success)
        {
            tx.Rollback();
            return result;
        }

        foreach (var code in codes)
        {
            var orgResult = await _orgService.GetByCode(code);
            if (!orgResult.Success || orgResult.Data == null)
                continue;

            var delResult = await _orgService.DeleteByCode(code, UserContext.ClientIp);
            if (!delResult.Success)
            {
                tx.Rollback();
                return Result<int>.Fail($"同步删除系统机构记录失败：{delResult.Error}");
            }
        }

        tx.Commit();
        return result;
    }

    /// <summary>
    /// 启用 / 禁用认证机构（IsValid: 0 ↔ 1）
    /// <para>禁用时同步 Sys_Organization.Enable = 0，启用时同步为 1</para>
    /// </summary>
    [HttpPost("toggle-valid")]
    public override async Task<ActionResult<ApiResponse<object?>>> ToggleIsValid([FromBody] JsonElement entityData)
    {
        try
        {
            var code = entityData.TryGetProperty("Code", out var codeProp) ? codeProp.GetString() : null;
            if (string.IsNullOrEmpty(code))
                return BadRequest(ApiResponse.Fail("Code 不能为空"));

            var getResult = await Entity.GetByCodeAny(code);
            if (!getResult.Success || getResult.Data == null)
                return BadRequest(ApiResponse.Fail($"认证机构 {code} 不存在"));

            var entity = getResult.Data;
            var newVal = entity.IsValid == 1 ? 0 : 1;
            entity.IsValid = newVal;
            entity.Status = newVal == 1 ? "active" : "inactive";

            using var tx = _dbOrm.BeginTransaction();

            var updateResult = await Entity.Update(entity, UserContext.ClientIp);
            if (!updateResult.Success)
            {
                tx.Rollback();
                return BadRequest(ApiResponse.Fail(updateResult.Error));
            }

            var (ok, msg) = await SyncOrgAsync(entity);
            if (!ok)
            {
                tx.Rollback();
                return BadRequest(ApiResponse.Fail(msg ?? "同步系统机构记录失败"));
            }

            tx.Commit();
            return Ok(ApiResponse<object?>.Ok(new { Code = code, IsValid = newVal }));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"切换启用/禁用失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 四、机构同步辅助
    // ========================================================

    /// <summary>定位认证机构根节点：OrgType='CertBody' 且无父节点</summary>
    private async Task<string?> ResolveCertBodyRootCodeAsync()
    {
        var roots = await _orgService.GetListAsync(o =>
            o.OrgType == "CertBody" && (o.ParentCode == null || o.ParentCode == ""));

        if (!roots.Success || roots.Data == null || roots.Data.Count == 0)
            return null;

        return roots.Data
            .OrderBy(o => o.Sort ?? 0)
            .Select(o => o.Code)
            .FirstOrDefault();
    }

    /// <summary>由 Attach 实体构造对应的 Sys_Organization 记录</summary>
    private static Sys_Organization BuildOrgNode(CB entity, string rootCode) => new()
    {
        Code = entity.Code,
        OrgName = entity.Name,
        OrgCode = entity.CbCode,
        ParentCode = rootCode,
        OrgType = "CertBody",
        OrgLevel = CertBodyOrgLevel,
        OrgPath = $"/{rootCode}/{entity.Code}",
        LeaderName = entity.ContactName,
        LeaderPhone = entity.ContactPhone,
        Sort = entity.Sort,
        Enable = (byte)(entity.IsValid == 1 ? 1 : 0),
        IsValid = 1,
        Remark = entity.Remark
    };

    /// <summary>同步 Attach → Sys_Organization（名称 / 编号 / 启用状态）</summary>
    private async Task<(bool ok, string? msg)> SyncOrgAsync(CB entity)
    {
        var orgCode = string.IsNullOrEmpty(entity.OrgCode) ? entity.Code : entity.OrgCode!;

        var orgResult = await _orgService.GetByCode(orgCode);
        if (!orgResult.Success || orgResult.Data == null)
            return (false, $"未找到对应的系统机构记录（Sys_Organization.Code={orgCode}），数据可能不一致");

        var org = orgResult.Data;
        org.OrgName = entity.Name;
        org.OrgCode = entity.CbCode;
        org.Enable = (byte)(entity.IsValid == 1 ? 1 : 0);

        if (org.OrgLevel == null)
            org.OrgLevel = CertBodyOrgLevel;

        if (string.IsNullOrEmpty(org.OrgPath) && !string.IsNullOrEmpty(org.ParentCode))
            org.OrgPath = $"/{org.ParentCode}/{org.Code}";

        var updateResult = await _orgService.Update(org, UserContext.ClientIp);
        return updateResult.Success ? (true, null) : (false, updateResult.Error);
    }
}
