using YZH.Core.Api.Models.Organization;
using YZH.Core.Api.Models.Users;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services;

/// <summary>
/// 专家工作区上下文解析（★ 专家端多租户隔离的唯一入口）
///
/// <para><b>为什么需要它</b>：<c>IUserContext</c> 只提供 <c>UserCode</c> / <c>RoleCode</c> / <c>ClientIp</c>，
/// **没有 OrgCode** —— 后端无法直接得知「当前工作区」。而专家端所有业务数据（企业、文档、任务）
/// 都必须按工作区隔离，所以先建本解析器，后续模块统一复用。</para>
///
/// <para><b>解析链路</b>（依据 <c>AuditorRegisterService</c> 建树规则）：</para>
/// <code>
/// Sys_User.Code(当前登录人) → Sys_User.OrgCode        ← 指向 L3 角色分组（如「管理员」）
///   → Sys_Organization.OrgPath                        ← /{根}/{工作区}/{分组}
///   → 取第 2 段 = 工作区 Code
///   → Sys_Organization(工作区) 且 OrgType='VirtualOrg' ← 校验，防止把分组/根当成工作区
/// </code>
///
/// <para><b>为什么不直接用 Sys_User.OrgCode 当工作区</b>：该字段指向的是 L3 <b>分组</b>（人员挂叶子机构约束），
/// 不是工作区，直接用会把不同分组的人隔离成不同租户。</para>
/// </summary>
public class WorkspaceContextService
{
    private readonly IDbOrm _db;

    /// <summary>工作区在机构树中的层级（根为 1）</summary>
    private const int WorkspaceLevel = 2;

    /// <summary>工作区节点类型</summary>
    private const string WorkspaceOrgType = "VirtualOrg";

    public WorkspaceContextService(IDbOrm db)
    {
        _db = db;
    }

    /// <summary>
    /// 解析当前用户所属的专家工作区。
    /// <para>同步实现 —— 供 <c>OnBuildingFilter</c> 等**同步钩子**复用（SqlSugar 支持同步查询）。</para>
    /// </summary>
    public Result<Sys_Organization> Resolve(string? userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return Result<Sys_Organization>.Fail("未获取到当前登录用户，无法定位工作区");

        var user = _db.Client.Queryable<Sys_User>()
            .Where(x => x.Code == userCode)
            .First();

        if (user == null)
            return Result<Sys_Organization>.Fail("当前登录用户不存在，请重新登录");

        if (string.IsNullOrWhiteSpace(user.OrgCode))
            return Result<Sys_Organization>.Fail(
                "当前账号未挂靠任何机构，无法定位工作区（请用专家注册的账号登录）");

        var ownNode = _db.Client.Queryable<Sys_Organization>()
            .Where(x => x.Code == user.OrgCode)
            .First();

        if (ownNode == null)
            return Result<Sys_Organization>.Fail("账号挂靠的机构节点不存在（数据异常，请联系管理员）");

        var path = ownNode.OrgPath ?? string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // 期望 /{根}/{工作区}/{分组} → 工作区 = 第 2 段（索引 1）
        if (segments.Length < 2)
            return Result<Sys_Organization>.Fail(
                $"机构路径异常（OrgPath={path}），无法定位工作区；账号可能未挂在工作区下");

        var workspaceCode = segments[1];

        var workspace = _db.Client.Queryable<Sys_Organization>()
            .Where(x => x.Code == workspaceCode)
            .First();

        if (workspace == null)
            return Result<Sys_Organization>.Fail($"工作区节点 {workspaceCode} 不存在（数据异常）");

        if (!string.Equals(workspace.OrgType, WorkspaceOrgType, StringComparison.OrdinalIgnoreCase))
            return Result<Sys_Organization>.Fail(
                $"机构【{workspace.OrgName}】不是专家工作区（OrgType={workspace.OrgType}，期望 {WorkspaceOrgType}）");

        if (workspace.OrgLevel != WorkspaceLevel)
            return Result<Sys_Organization>.Fail(
                $"机构【{workspace.OrgName}】层级异常（OrgLevel={workspace.OrgLevel}，期望 {WorkspaceLevel}）");

        return Result<Sys_Organization>.Ok(workspace);
    }

    /// <summary>解析当前用户所属的专家工作区（异步包装，供 async 上下文使用）</summary>
    public Task<Result<Sys_Organization>> ResolveAsync(string? userCode)
    {
        return Task.FromResult(Resolve(userCode));
    }
}
