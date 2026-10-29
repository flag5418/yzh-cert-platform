using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Entities.Expert;
using CertPlatform.Auditor.Services;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 审核人员管理控制器（专家端 —— 专家组设置）
///
/// <para><b>路由</b>：<c>api/Auditor/AuditMember</c></para>
/// <para><b>数据表</b>：<c>cert_audit_member</c>（工作区隔离，单表结构）</para>
///
/// <para><b>用途</b>：维护认证审核人员基本信息（姓名/职称/专业/资质/联系方式）。
/// 入场计划指派审核员时从本池子挑选，不关联登录账号。</para>
///
/// <para><b>硬约束</b>：</para>
/// <list type="number">
///   <item><b>工作区隔离</b>：全部操作按 <c>OrgCode = 当前工作区</c> 收敛</item>
///   <item><b>工作区内姓名唯一</b>（同一人不能在同工作区出现两次）</item>
///   <item><b>资质证书编号工作区内唯一</b>（有值时校验）</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class AuditMemberController : YzhControllerBase<CertAuditMember>
{
    private readonly WorkspaceContextService _workspace;

    public AuditMemberController(
        EntityService<CertAuditMember> entityService,
        IUserContext userContext,
        WorkspaceContextService workspace)
        : base(entityService, userContext)
    {
        _workspace = workspace;

        RegisterRowAction("disable", DisableMemberAsync);
        RegisterRowAction("enable", EnableMemberAsync);
    }

    /// <summary>缺 EntityConfig 时直接抛错</summary>
    protected override bool StrictConfigLoad => true;

    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<CertAuditMember>();
    }

    /// <summary>行按钮：Edit + Delete + 自定义 disable/enable（Enable=false 防重复）</summary>
    protected override RowButtonConfig GetRowButtons()
    {
        var source = base.GetRowButtons();
        var custom = new Dictionary<string, string>();

        if (source.CustomButtons != null)
        {
            foreach (var kv in source.CustomButtons)
                custom[kv.Key] = kv.Value;
        }

        custom["disable"] = "禁用";
        custom["enable"] = "启用";

        return new RowButtonConfig
        {
            Edit = source.Edit,
            Delete = source.Delete,
            Enable = false,
            CustomButtons = custom
        };
    }

    // ========================================================
    // 一、查询：强制收敛到当前工作区
    // ========================================================

    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        filters = base.OnBuildingFilter(filters);

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            throw new InvalidOperationException(ws.Error ?? "无法定位当前工作区");

        filters.RemoveAll(f => f.Field == "OrgCode");
        filters.Add(new FilterItem
        {
            Field = "OrgCode",
            Operator = "eq",
            Value = ws.Data.Code
        });

        return filters;
    }

    // ========================================================
    // 二、新增前校验
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(CertAuditMember entity)
    {
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        entity.OrgCode = ws.Data.Code;

        if (string.IsNullOrWhiteSpace(entity.Name))
            return (false, "姓名不能为空");

        if (entity.IsValid == 0)
            entity.IsValid = 1;

        // 工作区内姓名唯一
        var dupName = await Entity.ExistsAsync(p =>
            p.OrgCode == entity.OrgCode && p.Name == entity.Name && p.IsDeleted == false);
        if (dupName.Data)
            return (false, $"姓名【{entity.Name}】在当前工作区已存在");

        // 资质证书编号工作区内唯一（有值时校验）
        if (!string.IsNullOrWhiteSpace(entity.QualificationNo))
        {
            var dupQual = await Entity.ExistsAsync(p =>
                p.OrgCode == entity.OrgCode && p.QualificationNo == entity.QualificationNo && p.IsDeleted == false);
            if (dupQual.Data)
                return (false, $"资质证书编号【{entity.QualificationNo}】在当前工作区已被占用");
        }

        return (true, null);
    }

    // ========================================================
    // 三、修改前校验
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(CertAuditMember entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        if (string.IsNullOrWhiteSpace(entity.Name))
            return (false, "姓名不能为空");

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        var existing = await Entity.GetByCodeAny(entity.Code);
        if (!existing.Success || existing.Data == null)
            return (false, "记录不存在或已删除");

        if (!string.Equals(existing.Data.OrgCode, ws.Data.Code, StringComparison.Ordinal))
            return (false, "无权修改其他工作区的审核人员");

        entity.OrgCode = ws.Data.Code;

        // 姓名唯一性（排除自身）
        var dupName = await Entity.ExistsAsync(p =>
            p.Code != entity.Code && p.OrgCode == entity.OrgCode &&
            p.Name == entity.Name && p.IsDeleted == false);
        if (dupName.Data)
            return (false, $"姓名【{entity.Name}】在当前工作区已存在");

        // 资质证书编号唯一性（排除自身）
        if (!string.IsNullOrWhiteSpace(entity.QualificationNo))
        {
            var dupQual = await Entity.ExistsAsync(p =>
                p.Code != entity.Code && p.OrgCode == entity.OrgCode &&
                p.QualificationNo == entity.QualificationNo && p.IsDeleted == false);
            if (dupQual.Data)
                return (false, $"资质证书编号【{entity.QualificationNo}】在当前工作区已被占用");
        }

        return (true, null);
    }

    // ========================================================
    // 四、行操作：禁用 / 启用
    // ========================================================

    private async Task<Result<ApiResponse<object?>>> DisableMemberAsync(CertAuditMember entity)
    {
        return await SetMemberValidAsync(entity.Code, 0);
    }

    private async Task<Result<ApiResponse<object?>>> EnableMemberAsync(CertAuditMember entity)
    {
        return await SetMemberValidAsync(entity.Code, 1);
    }

    private async Task<Result<ApiResponse<object?>>> SetMemberValidAsync(string? code, int isValid)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<ApiResponse<object?>>.Fail("缺少业务键 Code");

        var result = await Entity.GetByCodeAny(code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("记录不存在");

        var target = result.Data;

        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Result<ApiResponse<object?>>.Fail(ws.Error ?? "无法定位当前工作区");

        if (!string.Equals(target.OrgCode, ws.Data.Code, StringComparison.Ordinal))
            return Result<ApiResponse<object?>>.Fail("无权操作其他工作区的审核人员");

        if (target.IsValid == isValid)
            return Result<ApiResponse<object?>>.Ok(
                ApiResponse<object?>.Ok(isValid == 1 ? "该审核人员已是启用状态" : "该审核人员已是禁用状态"));

        target.IsValid = isValid;
        var upd = await Entity.Update(target, UserContext.ClientIp);
        if (!upd.Success)
            return Result<ApiResponse<object?>>.Fail(upd.Error);

        return Result<ApiResponse<object?>>.Ok(
            ApiResponse<object?>.Ok(isValid == 1 ? "已启用" : "已禁用"));
    }
}
