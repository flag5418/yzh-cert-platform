using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Entities.Sys;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Shared.Domain.Scope;

/// <summary>
/// ★★ <b>核心资源检索唯一入口</b>（铁律 <b>L5</b>：一个业务概念一个检索入口）。
///
/// <para><b>规则</b>（见 <c>docs/30-项目规则/跨端共享与核心资源检索规则-V1.md</c> §三）：
/// 「机构 + 标准 + 阶段」及其派生的 <b>NC 规则 / 章节规则 / 填写规则 / 提取规则</b> 的<b>取数</b>，
/// <b>只允许</b>在本接口内实现。端内（`Admin/`、`Auditor/`）只允许<b>投影</b>：
/// 选字段、加派生列、排序分页、组装 DTO；⛔ <b>不允许</b>自建
/// <c>Queryable&lt;T&gt;()</c> / <c>GetListAsync&lt;T&gt;()</c> / <c>WHERE</c>。</para>
///
/// <para><b>★ 为什么</b>（用户 2026-10-12 原话）：
/// 「<i>将核心体系认证系统的核心数据（主要是检索），统一到一个通用方法中，作为公共方法，
/// 这样避免不同逻辑中，查看到的结果不一致</i>」。</para>
///
/// <para><b>★ 判定口诀</b>：<b>同一个 <c>WHERE</c> 出现在第二处 ⇒ 就是违规。</b>
/// 守卫 <b>A-R6</b> 会拦下对下列实体的端内直查。</para>
///
/// <para><b>⚠️ 首期覆盖范围</b>：下表 8 类实体（<b>已在 <c>Shared/Entities/</c></b>）。
/// 仍在 <c>Admin/Entities/</c> 的 <c>ConclusionRule</c> / <c>NcJudgePrompt</c> /
/// <c>NcConcludePrompt</c> / <c>DirectoryTemplate</c> / <c>FileRequirement</c>
/// 待阶段 2/3 下沉后再纳入（<c>Shared</c> ⛔ 不能引 <c>Admin</c>）。</para>
///
/// <para><b>⚠️ 机构判据</b>：本接口内的 <c>OrgCode</c> 过滤一律用 <see cref="CoreOrgCodes.StdEnv"/>
/// 常量（⛔ 不用 <c>NULL</c> / <c>''</c>）；内存判断用 <see cref="OrgScope"/>。</para>
/// </summary>
public interface ICoreResourceQuery
{
    // ──────────────────────────── 标准 ────────────────────────────

    /// <summary>
    /// 取「后台标准级」标准（<c>OrgCode == 'yzh-std-env'</c>）；
    /// 给了 <paramref name="enterpriseCode"/> 时<b>并上</b>该企业的专属标准。
    /// </summary>
    Task<List<ISOStandard>> GetStandardsAsync(string? enterpriseCode = null);

    /// <summary>取指定标准下的条款（按 <c>SortOrder</c> 升序）。<paramref name="standardCode"/> = 标准 <c>Code</c>（GUID）。</summary>
    Task<List<ISOClause>> GetClausesAsync(string standardCode);

    // ──────────────────────────── 阶段 ────────────────────────────

    /// <summary>取全部认证阶段（全局基础资料，无 <c>OrgCode</c> 维度）。</summary>
    Task<List<CertStage>> GetStagesAsync();

    // ────────────────────── 机构×标准 / 机构×阶段 ──────────────────────

    /// <summary>取机构已关联的标准（<paramref name="orgCode"/> = 机构 <c>Code</c>）。</summary>
    Task<List<CertOrgStandard>> GetOrgStandardsAsync(string orgCode);

    /// <summary>取机构已关联的阶段（<paramref name="orgCode"/> = 机构 <c>Code</c>）。</summary>
    Task<List<CertOrgStage>> GetOrgStagesAsync(string orgCode);

    // ─────────────────────── 填写规则（参数定义 / 提示词）───────────────────────

    /// <summary>
    /// 取后台参数定义（<c>OrgCode == 'yzh-std-env'</c>）；
    /// 给了 <paramref name="standardCode"/> 时只取「通用（<c>StandardCode=''</c>）+ 该标准」两组。
    /// </summary>
    Task<List<FillParamDef>> GetFillParamDefsAsync(string? standardCode = null);

    /// <summary>取填写提示词。<paramref name="orgCode"/> 缺省 = 后台标准级（<c>'yzh-std-env'</c>）。</summary>
    Task<List<DocFillPrompt>> GetDocFillPromptsAsync(string? orgCode = null);

    // ──────────────────────────── 提取规则 ────────────────────────────

    /// <summary>取文档提取规则。<paramref name="orgCode"/> 缺省 = 后台标准级（<c>'yzh-std-env'</c>）。</summary>
    Task<List<DocExtractionRule>> GetDocExtractionRulesAsync(string? orgCode = null);
}

/// <summary>
/// <see cref="ICoreResourceQuery"/> 的默认实现。
///
/// <para><b>⛔ 本类是核心资源检索的「唯一实现」</b> —— 新增取数需求时<b>加方法到本类</b>，
/// ⛔ 不要在端内另写一份。</para>
///
/// <para>⚠️ 表达式里一律用<b>显式分支</b>而非 <c>!isEmpty || x.OrgCode == org</c> ——
/// 把常量折叠交给 ORM 容易踩表达式翻译坑（先例：<c>FillParamValueProvider.FindDefAsync</c>）。</para>
/// </summary>
public sealed class CoreResourceQuery : ICoreResourceQuery
{
    private readonly IDbOrm _db;

    public CoreResourceQuery(IDbOrm db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // ──────────────────────────── 标准 ────────────────────────────

    public async Task<List<ISOStandard>> GetStandardsAsync(string? enterpriseCode = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode))
        {
            var backend = await _db.GetListAsync<ISOStandard>(x => x.OrgCode == CoreOrgCodes.StdEnv);
            return backend.Data ?? new List<ISOStandard>();
        }

        var ent = enterpriseCode;
        var both = await _db.GetListAsync<ISOStandard>(
            x => x.OrgCode == CoreOrgCodes.StdEnv || x.OrgCode == ent);
        return both.Data ?? new List<ISOStandard>();
    }

    public async Task<List<ISOClause>> GetClausesAsync(string standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return new List<ISOClause>();

        var sc = standardCode;
        var rows = await _db.GetListAsync<ISOClause>(x => x.StandardCode == sc);
        return rows.Data ?? new List<ISOClause>();
    }

    // ──────────────────────────── 阶段 ────────────────────────────

    public async Task<List<CertStage>> GetStagesAsync()
    {
        var rows = await _db.GetListAsync<CertStage>();
        return rows.Data ?? new List<CertStage>();
    }

    // ────────────────────── 机构×标准 / 机构×阶段 ──────────────────────

    public async Task<List<CertOrgStandard>> GetOrgStandardsAsync(string orgCode)
    {
        if (string.IsNullOrWhiteSpace(orgCode)) return new List<CertOrgStandard>();

        var org = orgCode;
        var rows = await _db.GetListAsync<CertOrgStandard>(x => x.OrgCode == org);
        return rows.Data ?? new List<CertOrgStandard>();
    }

    public async Task<List<CertOrgStage>> GetOrgStagesAsync(string orgCode)
    {
        if (string.IsNullOrWhiteSpace(orgCode)) return new List<CertOrgStage>();

        var org = orgCode;
        var rows = await _db.GetListAsync<CertOrgStage>(x => x.OrgCode == org);
        return rows.Data ?? new List<CertOrgStage>();
    }

    // ─────────────────────── 填写规则（参数定义 / 提示词）───────────────────────

    public async Task<List<FillParamDef>> GetFillParamDefsAsync(string? standardCode = null)
    {
        if (string.IsNullOrWhiteSpace(standardCode))
        {
            var all = await _db.GetListAsync<FillParamDef>(x => x.OrgCode == CoreOrgCodes.StdEnv);
            return all.Data ?? new List<FillParamDef>();
        }

        var sc = standardCode;
        var rows = await _db.GetListAsync<FillParamDef>(
            x => x.OrgCode == CoreOrgCodes.StdEnv && (x.StandardCode == "" || x.StandardCode == sc));
        return rows.Data ?? new List<FillParamDef>();
    }

    public async Task<List<DocFillPrompt>> GetDocFillPromptsAsync(string? orgCode = null)
    {
        var org = string.IsNullOrWhiteSpace(orgCode) ? CoreOrgCodes.StdEnv : orgCode!;
        var rows = await _db.GetListAsync<DocFillPrompt>(x => x.OrgCode == org);
        return rows.Data ?? new List<DocFillPrompt>();
    }

    // ──────────────────────────── 提取规则 ────────────────────────────

    public async Task<List<DocExtractionRule>> GetDocExtractionRulesAsync(string? orgCode = null)
    {
        var org = string.IsNullOrWhiteSpace(orgCode) ? CoreOrgCodes.StdEnv : orgCode!;
        var rows = await _db.GetListAsync<DocExtractionRule>(x => x.OrgCode == org);
        return rows.Data ?? new List<DocExtractionRule>();
    }
}
