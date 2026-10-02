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

    // ════════════════════════════════════════════════════════════════════
    // 机构域归一 —— ★ 全项目唯一实现
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>机构域归一</b>：把「工作区节点 Code」或「企业挂靠节点 Code」换成
    /// <b>体系认证机构 Code</b>（<c>cert_certification_body.Code</c>）。
    ///
    /// <para><b>为什么必须归一</b>：本项目同时存在三个「机构」概念，且它们的 Code 互不相同：</para>
    /// <list type="table">
    ///   <listheader><term>层</term><description>表 / 字段 / 值示例</description></listheader>
    ///   <item><term>体系认证机构</term>
    ///         <description><c>cert_certification_body.Code</c> = <c>906e8b2a…</c>（GUID），
    ///         另有业务编号 <c>CbCode</c> = <c>CB001</c></description></item>
    ///   <item><term>专家工作区</term>
    ///         <description><c>Sys_Organization</c>（<c>OrgType='VirtualOrg'</c>）<c>.Code</c> = <c>66bbf572…</c>，
    ///         其 <c>OrgCode</c> 字段存的是<b>认证机构的 <c>CbCode</c></b>（<c>CB001</c>）</description></item>
    ///   <item><term>企业</term>
    ///         <description><c>cert_enterprise.OrgCode</c> = <b>工作区 Code</b>（<c>66bbf572…</c>）</description></item>
    /// </list>
    ///
    /// <para><b>桥</b>：<c>工作区.Code</c> → <c>工作区.OrgCode</c> → <c>CertificationBody.CbCode</c>
    /// → <c>CertificationBody.Code</c>。</para>
    ///
    /// <para><b>★ 为什么放这里而不是各处自己写</b>：本方法原先只存在于
    /// <c>EnterpriseFileService.NormalizeOrgCodeAsync</c>（私有）。参数定义、目录模板、
    /// 标准关联都要做同一次归一 —— 复制成三份后，任一处修好另两处仍坏，且<b>都不报错</b>，
    /// 症状是「某些工作区看不到参数/模板，另一些正常」。故上提为唯一实现，
    /// <c>EnterpriseFileService</c> 改为委托本方法。</para>
    ///
    /// <para><b>解析不到时原样返回</b>（不抛错）：调用方按「查不到数据」处理，
    /// 空态由各自的查询统一回报。这样「企业还没挂机构」是空列表而非 500。</para>
    /// </summary>
    public async Task<string> ResolveCertBodyCodeAsync(string orgCode)
    {
        if (string.IsNullOrWhiteSpace(orgCode)) return orgCode;

        // ① 本身就是认证机构 Code
        var self = await _db.GetOneAsync<CertificationBody>(x => x.Code == orgCode);
        if (self.Data != null) return orgCode;

        // ② 是机构树节点 → 取其业务编号 → 反查认证机构
        var node = await _db.GetOneAsync<Sys_Organization>(x => x.Code == orgCode);
        var bizCode = node.Data?.OrgCode;
        if (!string.IsNullOrWhiteSpace(bizCode))
        {
            var byBiz = await _db.GetOneAsync<CertificationBody>(x => x.CbCode == bizCode);
            if (byBiz.Data?.Code != null) return byBiz.Data.Code;
        }

        // ③ 传进来的直接就是业务编号（CbCode）
        var legacy = await _db.GetOneAsync<CertificationBody>(x => x.CbCode == orgCode);
        return legacy.Data?.Code ?? orgCode;
    }

    /// <summary>
    /// 解析当前用户的<b>工作区 + 所属体系认证机构</b>，一次给全（★ 新模块统一用这个）。
    /// <para>不要只调 <see cref="Resolve"/> 拿到工作区就当机构用 —— 那是两个不同的 Code
    /// （见 <see cref="ResolveCertBodyCodeAsync"/> 的对照表）。</para>
    /// </summary>
    public async Task<Result<WorkspaceScope>> ResolveScopeAsync(string? userCode)
    {
        var ws = Resolve(userCode);
        if (!ws.Success || ws.Data == null)
            return Result<WorkspaceScope>.Fail(ws.Error ?? "无法定位当前工作区");

        var certBodyCode = await ResolveCertBodyCodeAsync(ws.Data.Code!);

        return Result<WorkspaceScope>.Ok(new WorkspaceScope(
            WorkspaceCode: ws.Data.Code!,
            WorkspaceName: ws.Data.OrgName ?? string.Empty,
            CertBodyCode: certBodyCode));
    }
}

/// <summary>
/// 当前登录人所属的作用域：<b>工作区</b>（专家平台租户隔离键）+ <b>体系认证机构</b>（配置数据隔离键）。
/// <para>★ 两个 Code 用途不同，⛔ 不可互换：</para>
/// <list type="bullet">
///   <item><c>WorkspaceCode</c> → <c>cert_enterprise.OrgCode</c>、<c>cert_fill_param_value.OrgCode</c>（数据归属）</item>
///   <item><c>CertBodyCode</c> → <c>cert_fill_param_def.OrgCode</c>、目录模板建档域（配置归属）</item>
/// </list>
/// </summary>
public sealed record WorkspaceScope(string WorkspaceCode, string WorkspaceName, string CertBodyCode);
