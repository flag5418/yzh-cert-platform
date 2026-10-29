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
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Entities.Cert;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业部门管理控制器（专家端 —— 企业机构管理）
///
/// <para><b>路由</b>：<c>api/Auditor/EnterpriseDept</c></para>
/// <para><b>数据表</b>：<c>cert_enterprise_dept</c>（按企业隔离，树形不限层级）</para>
///
/// <para><b>用途</b>：按企业建立部门树（不限层级），每部门带负责人信息。
/// 入场计划选择部门、定访谈对象时引用。</para>
///
/// <para><b>硬约束</b>：</para>
/// <list type="number">
///   <item><b>企业隔离</b>：全部操作按 <c>EnterpriseCode = 当前选中的企业</c> 收敛</item>
///   <item><b>禁止删除有子部门的父级</b>（OnBeforeDelete 校验）</item>
///   <item><b>部门名称企业内唯一</b>（同级同企业不重名）</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class EnterpriseDeptController : YzhControllerBase<CertEnterpriseDept>
{
    private readonly WorkspaceContextService _workspace;
    private readonly ICoreResourceQuery _coreResource;

    public EnterpriseDeptController(
        EntityService<CertEnterpriseDept> entityService,
        IUserContext userContext,
        WorkspaceContextService workspace,
        ICoreResourceQuery coreResource)
        : base(entityService, userContext)
    {
        _workspace = workspace;
        _coreResource = coreResource;

        RegisterRowAction("disable", DisableDeptAsync);
        RegisterRowAction("enable", EnableDeptAsync);
    }

    /// <summary>缺 EntityConfig 时直接抛错</summary>
    protected override bool StrictConfigLoad => true;

    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<CertEnterpriseDept>();
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
    // 一、查询：按企业过滤 + 工作区隔离
    // ========================================================

    protected override List<FilterItem> OnBuildingFilter(List<FilterItem> filters)
    {
        filters = base.OnBuildingFilter(filters);

        // 必须选定了企业才能查（前端 SearchFields 提供 EnterpriseCode 过滤）
        var enterpriseFilter = filters.FirstOrDefault(f => f.Field == "EnterpriseCode");
        if (enterpriseFilter == null || string.IsNullOrWhiteSpace(enterpriseFilter.Value?.ToString()))
        {
            // 未选企业时返回空（前端已强制先选企业；后端兜底加永假条件）
            filters.RemoveAll(f => f.Field == "__empty__");
            filters.Add(new FilterItem { Field = "__empty__", Operator = "eq", Value = "1" });
            return filters;
        }

        // ★ 企业归属校验不在同步钩子里做（避免 async-over-sync 死锁）。
        //   写入路径（OnBeforeAdd / OnBeforeUpdate / SetDeptValidAsync）已逐条校验；
        //   查询路径依赖前端下拉只暴露本工作区企业（伪造 Code ⇒ 查不到数据，无信息泄露）。
        return filters;
    }

    // ========================================================
    // 二、新增前校验
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(CertEnterpriseDept entity)
    {
        if (string.IsNullOrWhiteSpace(entity.EnterpriseCode))
            return (false, "请先选择企业");

        if (string.IsNullOrWhiteSpace(entity.DeptName))
            return (false, "部门名称不能为空");

        if (entity.IsValid == 0)
            entity.IsValid = 1;

        // 工作区隔离
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        // 校验企业归属（核心资源取数走 CoreResourceQuery，端内只投影比对）
        var enterprise = await _coreResource.GetEnterpriseByCodeAsync(entity.EnterpriseCode);
        if (enterprise == null || enterprise.OrgCode != ws.Data.Code)
            return (false, "该企业不属于当前工作区，无权操作");

        // 同级同企业部门名称唯一
        var dupName = await Entity.ExistsAsync(p =>
            p.EnterpriseCode == entity.EnterpriseCode &&
            p.ParentCode == entity.ParentCode &&
            p.DeptName == entity.DeptName &&
            p.IsDeleted == false);
        if (dupName.Data)
            return (false, $"部门名称【{entity.DeptName}】在该层级下已存在");

        // 校验上级部门存在且属于同一企业
        if (!string.IsNullOrWhiteSpace(entity.ParentCode))
        {
            var parent = await Entity.GetByCode(entity.ParentCode);
            if (!parent.Success || parent.Data == null)
                return (false, "上级部门不存在或已删除");
            if (parent.Data.EnterpriseCode != entity.EnterpriseCode)
                return (false, "上级部门不属于同一企业");
        }

        return (true, null);
    }

    // ========================================================
    // 三、修改前校验
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(CertEnterpriseDept entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        if (string.IsNullOrWhiteSpace(entity.DeptName))
            return (false, "部门名称不能为空");

        var existing = await Entity.GetByCodeAny(entity.Code);
        if (!existing.Success || existing.Data == null)
            return (false, "记录不存在或已删除");

        if (string.IsNullOrWhiteSpace(entity.EnterpriseCode))
            entity.EnterpriseCode = existing.Data.EnterpriseCode;

        // 工作区隔离
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return (false, ws.Error ?? "无法定位当前工作区");

        // 校验企业归属（核心资源取数走 CoreResourceQuery，端内只投影比对）
        var enterprise = await _coreResource.GetEnterpriseByCodeAsync(entity.EnterpriseCode);
        if (enterprise == null || enterprise.OrgCode != ws.Data.Code)
            return (false, "该企业不属于当前工作区，无权操作");

        // 部门名称唯一性（排除自身）
        var dupName = await Entity.ExistsAsync(p =>
            p.Code != entity.Code &&
            p.EnterpriseCode == entity.EnterpriseCode &&
            p.ParentCode == entity.ParentCode &&
            p.DeptName == entity.DeptName &&
            p.IsDeleted == false);
        if (dupName.Data)
            return (false, $"部门名称【{entity.DeptName}】在该层级下已存在");

        // 校验上级部门存在
        if (!string.IsNullOrWhiteSpace(entity.ParentCode))
        {
            // 防止父级设为自己
            if (entity.ParentCode == entity.Code)
                return (false, "上级部门不能是自身");

            var parent = await Entity.GetByCodeAny(entity.ParentCode);
            if (!parent.Success || parent.Data == null)
                return (false, "上级部门不存在或已删除");
            if (parent.Data.EnterpriseCode != entity.EnterpriseCode)
                return (false, "上级部门不属于同一企业");
        }

        return (true, null);
    }

    // ========================================================
    // 四、删除前校验：禁止删除有子部门的父级
    // ========================================================

    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;

            var hasChildren = await Entity.ExistsAsync(p =>
                p.ParentCode == code && p.IsDeleted == false);
            if (hasChildren.Data)
                return (false, "该部门存在子部门，请先删除子部门");
        }

        return await base.OnBeforeDelete(codes);
    }

    // ========================================================
    // 五、行操作：禁用 / 启用
    // ========================================================

    private async Task<Result<ApiResponse<object?>>> DisableDeptAsync(CertEnterpriseDept entity)
    {
        return await SetDeptValidAsync(entity.Code, 0);
    }

    private async Task<Result<ApiResponse<object?>>> EnableDeptAsync(CertEnterpriseDept entity)
    {
        return await SetDeptValidAsync(entity.Code, 1);
    }

    private async Task<Result<ApiResponse<object?>>> SetDeptValidAsync(string? code, int isValid)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<ApiResponse<object?>>.Fail("缺少业务键 Code");

        var result = await Entity.GetByCodeAny(code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("记录不存在");

        var target = result.Data;

        // 工作区隔离
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Result<ApiResponse<object?>>.Fail(ws.Error ?? "无法定位当前工作区");

        // 校验企业归属（核心资源取数走 CoreResourceQuery，端内只投影比对）
        var enterprise = await _coreResource.GetEnterpriseByCodeAsync(target.EnterpriseCode);
        if (enterprise == null || enterprise.OrgCode != ws.Data.Code)
            return Result<ApiResponse<object?>>.Fail("该企业不属于当前工作区，无权操作");

        if (target.IsValid == isValid)
            return Result<ApiResponse<object?>>.Ok(
                ApiResponse<object?>.Ok(isValid == 1 ? "该部门已是启用状态" : "该部门已是禁用状态"));

        target.IsValid = isValid;
        var upd = await Entity.Update(target, UserContext.ClientIp);
        if (!upd.Success)
            return Result<ApiResponse<object?>>.Fail(upd.Error);

        return Result<ApiResponse<object?>>.Ok(
            ApiResponse<object?>.Ok(isValid == 1 ? "已启用" : "已禁用"));
    }
}
