using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Entities.Dir;
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
/// <para><b>★ 覆盖范围（2026-10-13 阶段 1.B 扩充后）</b>：已覆盖「机构 + 标准 + 阶段」<b>核心域</b>
/// 的 <b>15 类实体</b>（含 <c>CertificationBody</c> / <c>Enterprise</c> /
/// <c>StandardDirectoryConfig</c> / <c>StandardDirectoryFolder</c> / <c>DocTemplate</c>）。
///
/// <para>⏸ <b>挂账 3 类</b>（实体未下沉 ⇒ 等阶段 2/3 收口，<c>Shared</c> ⛔ 不能引 <c>Admin</c>/<c>Auditor</c>）：
/// <c>ConclusionRule</c> / <c>NcJudgePrompt</c> / <c>NcConcludePrompt</c>
/// （<b>三个类同一文件</b> <c>Admin/Entities/Cert/ConclusionRule.cs</c>）·
/// <c>FileRequirement</c>（<c>Admin/Entities/</c>）· <c>CertEnterpriseStage</c>（<c>Auditor/Entities/</c>）。</para>
///
/// <para>★ <b>树装配与排序口径</b>见 <c>Shared/Domain/Organization/OrgStandardTreeAssembler</c>
/// （⛔ 端内不要自己写排序链）。</para>
///
/// <para><b>⚠️ 归属判据（归属模型 V2）</b>：本接口内的 SQL 谓词以 <c>x.OrgCode == ""</c>
/// 表达「平台标准级」（= <b>两列都空</b>；⛔ 旧常量 <c>yzh-std-env</c> 已废除）；
/// 内存判断一律走 <see cref="OrgScope"/>（<c>IsEnterpriseRow</c> 为唯一分类判据）。</para>
/// </summary>
public interface ICoreResourceQuery
{
    // ──────────────────────────── 标准 ────────────────────────────

    /// <summary>
    /// 取「平台标准级」标准（<c>OrgCode</c> 为空 = 两列都空）；
    /// 给了 <paramref name="enterpriseCode"/> 时<b>并上</b>该企业的专属标准。
    /// </summary>
    Task<List<ISOStandard>> GetStandardsAsync(string? enterpriseCode = null);

    /// <summary>
    /// 取<b>全部有效标准</b>（<c>IsValid==1</c> 且未删 —— 框架 <c>GetListAsync</c> 内含两条件，
    /// 调用方 ⛔ 不要再写 <c>x.IsValid == 1</c> / <c>!x.IsDeleted</c> 重复谓词）。
    /// </summary>
    Task<List<ISOStandard>> GetValidStandardsAsync();

    /// <summary>按 <c>Code</c> 集合批量取标准（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<ISOStandard>> GetStandardsByCodesAsync(IEnumerable<string> standardCodes);

    /// <summary>按 <c>Code</c> 单查标准（未命中返回 <c>null</c>）。</summary>
    Task<ISOStandard?> GetStandardByCodeAsync(string? standardCode);

    /// <summary>取指定标准下的条款（按 <c>Sort</c> 升序）。<paramref name="standardCode"/> = 标准 <c>Code</c>（GUID）。</summary>
    Task<List<ISOClause>> GetClausesAsync(string standardCode);

    /// <summary>按条款 <c>Code</c> 单查条款（未命中返回 <c>null</c>）。</summary>
    Task<ISOClause?> GetClauseByCodeAsync(string? clauseCode);

    // ──────────────────────────── 阶段 ────────────────────────────

    /// <summary>取全部认证阶段（全局基础资料，无 <c>OrgCode</c> 维度）。</summary>
    Task<List<CertStage>> GetStagesAsync();

    /// <summary>按阶段 <c>Code</c> 单查阶段（未命中返回 <c>null</c>）。</summary>
    Task<CertStage?> GetStageByCodeAsync(string? stageCode);

    /// <summary>按阶段 <c>Code</c> 集合批量取阶段（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<CertStage>> GetStagesByCodesAsync(IEnumerable<string> stageCodes);

    /// <summary>按集合同时匹配 <c>Code</c> 或 <c>StageCode</c>（据实谓词：端内既有 <c>list.Contains(x.Code) || list.Contains(x.StageCode)</c> 写法）。</summary>
    Task<List<CertStage>> GetStagesByCodeOrStageCodeAsync(IEnumerable<string> codes);

    // ────────────────────── 机构×标准 / 机构×阶段 ──────────────────────

    /// <summary>取机构已关联的标准（<paramref name="orgCode"/> = 机构 <c>Code</c>）。</summary>
    Task<List<CertOrgStandard>> GetOrgStandardsAsync(string orgCode);

    /// <summary>取机构已关联的阶段（<paramref name="orgCode"/> = 机构 <c>Code</c>）。</summary>
    Task<List<CertOrgStage>> GetOrgStagesAsync(string orgCode);

    /// <summary>
    /// 取<b>全部</b>机构×标准关联 —— 供「一次取回 + 内存组树」的整树端点使用
    /// （机构数在个位到十位，优于逐机构 N+1）。
    /// </summary>
    Task<List<CertOrgStandard>> GetAllOrgStandardsAsync();

    /// <summary>取<b>全部</b>机构×阶段关联（用途同 <see cref="GetAllOrgStandardsAsync"/>）。</summary>
    Task<List<CertOrgStage>> GetAllOrgStagesAsync();

    // ─────────────────────── 填写规则（参数定义 / 提示词）───────────────────────

    /// <summary>
    /// 取平台标准级参数定义（<c>OrgCode</c> 为空 = 平台级）。
    /// <list type="bullet">
    /// <item><paramref name="standardCode"/> / <paramref name="stageCode"/> 为 <c>null</c> = 该维<b>不限</b>（不过滤）；</item>
    /// <item>传入值（含空串）= 取「通配（<c>列 == ""</c>）+ 等值」两组 —— 与端内既有谓词逐字一致。</item>
    /// </list>
    /// </summary>
    Task<List<FillParamDef>> GetFillParamDefsAsync(string? standardCode = null, string? stageCode = null);

    /// <summary>取填写提示词。<paramref name="orgCode"/> 缺省 = 平台标准级（<c>OrgCode</c> 为空）。</summary>
    Task<List<DocFillPrompt>> GetDocFillPromptsAsync(string? orgCode = null);

    /// <summary>按提示词版本行 <c>Code</c> 单查（未命中返回 <c>null</c>）。</summary>
    Task<DocFillPrompt?> GetDocFillPromptByCodeAsync(string? promptCode);

    // ──────────────────────────── 提取规则 ────────────────────────────

    /// <summary>取文档提取规则。<paramref name="orgCode"/> 缺省 = 平台标准级（<c>OrgCode</c> 为空）。</summary>
    Task<List<DocExtractionRule>> GetDocExtractionRulesAsync(string? orgCode = null);

    /// <summary>按规则 <c>Code</c> 单查提取规则（未命中返回 <c>null</c>）。</summary>
    Task<DocExtractionRule?> GetDocExtractionRuleByCodeAsync(string? ruleCode);

    // ─────────────────────── 标准目录文件 ───────────────────────

    /// <summary>取<b>全部有效</b>标准目录文件（对应端内 16 处冗余的 <c>x.IsValid == 1</c> 全表写法）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesAsync();

    /// <summary>按目录配置 <c>Code</c> 取该目录下文件。<paramref name="includeDisabled"/> = <c>true</c> 时含已禁用行（「已移除槽位」仍属对账清单）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByConfigCodeAsync(string configCode, bool includeDisabled = false);

    /// <summary>按文件 <c>Code</c> 单查标准目录文件（未命中返回 <c>null</c>）。</summary>
    Task<StandardDirectoryFile?> GetDirectoryFileByCodeAsync(string? fileCode);

    /// <summary>按文件 <c>Code</c> 集合批量取有效文件（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByCodesAsync(IEnumerable<string> fileCodes);

    /// <summary>按目录配置 <c>Code</c> 集合批量取文件（<paramref name="includeDisabled"/> 语义同单数版）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByConfigCodesAsync(IEnumerable<string> configCodes, bool includeDisabled = false);

    /// <summary>按上传任务 <c>TaskId</c> 取文件（<paramref name="includeDisabled"/>：待激活/等转换行 <c>IsValid=0</c>，须显式带上）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByTaskIdAsync(string taskId, bool includeDisabled = false);

    /// <summary>按企业（<c>EnterpriseCode</c>，文件表的机构维度）取文件行。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByEnterpriseCodeAsync(string enterpriseCode, bool includeDisabled = false);

    /// <summary>按文件夹 <c>Code</c> 取该文件夹下全部有效文件。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByFolderCodeAsync(string folderCode);

    // ─────────────── 机构 / 企业（树根级）· 2026-10-13 阶段 1.B 扩充 ───────────────

    /// <summary>
    /// 取<b>全部有效认证机构</b>（树根级）。
    /// <para>⚠️ <b>不排序</b> —— 排序口径唯一实现在
    /// <c>OrgStandardTreeAssembler.OrderOrgs</c>，调用方自行排序。</para>
    /// </summary>
    Task<List<CertificationBody>> GetCertificationBodiesAsync();

    /// <summary>
    /// 取某机构（= 工作区）下的<b>有效企业</b>。
    /// <para>⚠️ <b>不排序</b> —— 用 <c>OrgStandardTreeAssembler.OrderEnterprises</c>。</para>
    /// </summary>
    Task<List<Enterprise>> GetEnterprisesAsync(string orgCode);

    // ─────────────── 目录配置 / 文件夹 / 模板 · 2026-10-13 阶段 1.B 扩充 ───────────────

    /// <summary>
    /// 取标准目录配置。<paramref name="orgCode"/> / <paramref name="standardCode"/> /
    /// <paramref name="stageCode"/> / <paramref name="enterpriseCode"/> 为 <c>null</c> = 该维<b>不限</b>。
    /// <para>⚠️ 全 <c>null</c> = 取全部有效行（配置数在十量级，全量取回优于逐阶段 N+1）。</para>
    /// </summary>
    Task<List<StandardDirectoryConfig>> GetDirectoryConfigsAsync(
        string? orgCode = null, string? standardCode = null, string? stageCode = null, string? enterpriseCode = null);

    /// <summary>按目录配置 <c>Code</c> 集合取文件夹（空集合直接返回空，⛔ 不发查询）。<b>不排序</b>。</summary>
    Task<List<StandardDirectoryFolder>> GetDirectoryFoldersAsync(IEnumerable<string> configCodes);

    /// <summary>
    /// 取标准文档模板。<paramref name="orgCode"/> / <paramref name="standardCode"/> /
    /// <paramref name="stageCode"/> 为 <c>null</c> = 该维<b>不限</b>；
    /// <paramref name="onlyPublished"/> = <c>true</c> 时只取 <c>PublishStatus == "published"</c>。
    /// </summary>
    Task<List<DocTemplate>> GetDocTemplatesAsync(
        string? orgCode = null, string? standardCode = null, string? stageCode = null, bool onlyPublished = false);

    /// <summary>
    /// 按<b>宿主标准文件 <c>Code</c></b> 集合批量取模板
    /// （空集合直接返回空，⛔ 不发查询）。<b>不排序</b>。
    /// </summary>
    Task<List<DocTemplate>> GetDocTemplatesByStandardFileCodesAsync(IEnumerable<string> standardFileCodes);
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
            var backend = await _db.GetListAsync<ISOStandard>(x => x.OrgCode == "");
            return backend.Data ?? new List<ISOStandard>();
        }

        var ent = enterpriseCode;
        var both = await _db.GetListAsync<ISOStandard>(
            x => x.OrgCode == "" || x.OrgCode == ent);
        return both.Data ?? new List<ISOStandard>();
    }

    public async Task<List<ISOStandard>> GetValidStandardsAsync()
    {
        // 框架 GetListAsync 内含 IsDeleted=0 + IsValid=1 ⇒ 谓词处 ⛔ 不重复写
        var rows = await _db.GetListAsync<ISOStandard>();
        return rows.Data ?? new List<ISOStandard>();
    }

    public async Task<List<ISOStandard>> GetStandardsByCodesAsync(IEnumerable<string> standardCodes)
    {
        var codes = new List<string>();
        foreach (var c in standardCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<ISOStandard>();

        var rows = await _db.GetListAsync<ISOStandard>(x => codes.Contains(x.Code!));
        return rows.Data ?? new List<ISOStandard>();
    }

    public async Task<ISOStandard?> GetStandardByCodeAsync(string? standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return null;
        var sc = standardCode;
        var r = await _db.GetOneAsync<ISOStandard>(x => x.Code == sc);
        return r.Data;
    }

    public async Task<List<ISOClause>> GetClausesAsync(string standardCode)
    {
        if (string.IsNullOrWhiteSpace(standardCode)) return new List<ISOClause>();

        var sc = standardCode;
        var rows = await _db.GetListAsync<ISOClause>(x => x.StandardCode == sc);
        return rows.Data ?? new List<ISOClause>();
    }

    public async Task<ISOClause?> GetClauseByCodeAsync(string? clauseCode)
    {
        if (string.IsNullOrWhiteSpace(clauseCode)) return null;
        var cc = clauseCode;
        var r = await _db.GetOneAsync<ISOClause>(x => x.Code == cc);
        return r.Data;
    }

    // ──────────────────────────── 阶段 ────────────────────────────

    public async Task<List<CertStage>> GetStagesAsync()
    {
        var rows = await _db.GetListAsync<CertStage>();
        return rows.Data ?? new List<CertStage>();
    }

    public async Task<CertStage?> GetStageByCodeAsync(string? stageCode)
    {
        if (string.IsNullOrWhiteSpace(stageCode)) return null;
        var sc = stageCode;
        var r = await _db.GetOneAsync<CertStage>(x => x.Code == sc);
        return r.Data;
    }

    public async Task<List<CertStage>> GetStagesByCodesAsync(IEnumerable<string> stageCodes)
    {
        var codes = new List<string>();
        foreach (var c in stageCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<CertStage>();

        var rows = await _db.GetListAsync<CertStage>(x => codes.Contains(x.Code!));
        return rows.Data ?? new List<CertStage>();
    }

    public async Task<List<CertStage>> GetStagesByCodeOrStageCodeAsync(IEnumerable<string> codes)
    {
        var set = new List<string>();
        foreach (var c in codes)
            if (!string.IsNullOrWhiteSpace(c)) set.Add(c);
        if (set.Count == 0) return new List<CertStage>();

        var rows = await _db.GetListAsync<CertStage>(
            x => set.Contains(x.Code!) || set.Contains(x.StageCode!));
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

    public async Task<List<CertOrgStandard>> GetAllOrgStandardsAsync()
    {
        var rows = await _db.GetListAsync<CertOrgStandard>();
        return rows.Data ?? new List<CertOrgStandard>();
    }

    public async Task<List<CertOrgStage>> GetAllOrgStagesAsync()
    {
        var rows = await _db.GetListAsync<CertOrgStage>();
        return rows.Data ?? new List<CertOrgStage>();
    }

    // ─────────────────────── 填写规则（参数定义 / 提示词）───────────────────────

    public async Task<List<FillParamDef>> GetFillParamDefsAsync(string? standardCode = null, string? stageCode = null)
    {
        // ⚠️ null = 该维不限；传值（含空串）= 「通配 + 等值」—— 与端内既有谓词逐字一致。
        if (standardCode == null && stageCode == null)
        {
            var all = await _db.GetListAsync<FillParamDef>(x => x.OrgCode == "");
            return all.Data ?? new List<FillParamDef>();
        }

        if (stageCode == null)
        {
            var sc = standardCode;
            var rows = await _db.GetListAsync<FillParamDef>(
                x => x.OrgCode == "" && (x.StandardCode == "" || x.StandardCode == sc));
            return rows.Data ?? new List<FillParamDef>();
        }

        if (standardCode == null)
        {
            var stc = stageCode;
            var rows = await _db.GetListAsync<FillParamDef>(
                x => x.OrgCode == "" && (x.StageCode == "" || x.StageCode == stc));
            return rows.Data ?? new List<FillParamDef>();
        }

        var sc2 = standardCode;
        var stc2 = stageCode;
        var both = await _db.GetListAsync<FillParamDef>(
            x => x.OrgCode == ""
                && (x.StandardCode == "" || x.StandardCode == sc2)
                && (x.StageCode == "" || x.StageCode == stc2));
        return both.Data ?? new List<FillParamDef>();
    }

    public async Task<List<DocFillPrompt>> GetDocFillPromptsAsync(string? orgCode = null)
    {
        var org = string.IsNullOrWhiteSpace(orgCode) ? "" : orgCode!;
        var rows = await _db.GetListAsync<DocFillPrompt>(x => x.OrgCode == org);
        return rows.Data ?? new List<DocFillPrompt>();
    }

    public async Task<DocFillPrompt?> GetDocFillPromptByCodeAsync(string? promptCode)
    {
        if (string.IsNullOrWhiteSpace(promptCode)) return null;
        var pc = promptCode;
        var r = await _db.GetOneAsync<DocFillPrompt>(x => x.Code == pc);
        return r.Data;
    }

    // ──────────────────────────── 提取规则 ────────────────────────────

    public async Task<List<DocExtractionRule>> GetDocExtractionRulesAsync(string? orgCode = null)
    {
        var org = string.IsNullOrWhiteSpace(orgCode) ? "" : orgCode!;
        var rows = await _db.GetListAsync<DocExtractionRule>(x => x.OrgCode == org);
        return rows.Data ?? new List<DocExtractionRule>();
    }

    public async Task<DocExtractionRule?> GetDocExtractionRuleByCodeAsync(string? ruleCode)
    {
        if (string.IsNullOrWhiteSpace(ruleCode)) return null;
        var rc = ruleCode;
        var r = await _db.GetOneAsync<DocExtractionRule>(x => x.Code == rc);
        return r.Data;
    }

    // ─────────────────────── 标准目录文件 ───────────────────────

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesAsync()
    {
        var rows = await _db.GetListAsync<StandardDirectoryFile>();
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesByConfigCodeAsync(string configCode, bool includeDisabled = false)
    {
        if (string.IsNullOrWhiteSpace(configCode)) return new List<StandardDirectoryFile>();

        var cc = configCode;
        var rows = await _db.GetListAsync<StandardDirectoryFile>(x => x.ConfigCode == cc, includeDisabled: includeDisabled);
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<StandardDirectoryFile?> GetDirectoryFileByCodeAsync(string? fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode)) return null;
        var fc = fileCode;
        var r = await _db.GetOneAsync<StandardDirectoryFile>(x => x.Code == fc);
        return r.Data;
    }

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesByCodesAsync(IEnumerable<string> fileCodes)
    {
        var codes = new List<string>();
        foreach (var c in fileCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<StandardDirectoryFile>();

        var rows = await _db.GetListAsync<StandardDirectoryFile>(x => codes.Contains(x.Code!));
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesByConfigCodesAsync(IEnumerable<string> configCodes, bool includeDisabled = false)
    {
        var codes = new List<string>();
        foreach (var c in configCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<StandardDirectoryFile>();

        var rows = await _db.GetListAsync<StandardDirectoryFile>(x => codes.Contains(x.ConfigCode!), includeDisabled: includeDisabled);
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesByTaskIdAsync(string taskId, bool includeDisabled = false)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return new List<StandardDirectoryFile>();

        var tid = taskId;
        var rows = await _db.GetListAsync<StandardDirectoryFile>(x => x.TaskId == tid, includeDisabled: includeDisabled);
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesByEnterpriseCodeAsync(string enterpriseCode, bool includeDisabled = false)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode)) return new List<StandardDirectoryFile>();

        var ec = enterpriseCode;
        var rows = await _db.GetListAsync<StandardDirectoryFile>(x => x.EnterpriseCode == ec, includeDisabled: includeDisabled);
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesByFolderCodeAsync(string folderCode)
    {
        if (string.IsNullOrWhiteSpace(folderCode)) return new List<StandardDirectoryFile>();

        var fc = folderCode;
        var rows = await _db.GetListAsync<StandardDirectoryFile>(x => x.FolderCode == fc);
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    // ─────────────── 机构 / 企业（树根级）· 2026-10-13 阶段 1.B 扩充 ───────────────

    public async Task<List<CertificationBody>> GetCertificationBodiesAsync()
    {
        // 框架 GetListAsync 内含 IsDeleted=0 + IsValid=1 ⇒ 谓词处 ⛔ 不重复写
        var rows = await _db.GetListAsync<CertificationBody>();
        return rows.Data ?? new List<CertificationBody>();
    }

    public async Task<List<Enterprise>> GetEnterprisesAsync(string orgCode)
    {
        if (string.IsNullOrWhiteSpace(orgCode)) return new List<Enterprise>();

        var org = orgCode;
        var rows = await _db.GetListAsync<Enterprise>(x => x.OrgCode == org);
        return rows.Data ?? new List<Enterprise>();
    }

    // ─────────────── 目录配置 / 文件夹 / 模板 · 2026-10-13 阶段 1.B 扩充 ───────────────

    public async Task<List<StandardDirectoryConfig>> GetDirectoryConfigsAsync(
        string? orgCode = null, string? standardCode = null, string? stageCode = null, string? enterpriseCode = null)
    {
        // ⚠️ 4 维 ⇒ 16 种组合，用显式分支会爆炸 ⇒ 这里走链式 Where。
        //    判据与 GetListAsync 口径一致：!IsDeleted && IsValid == 1（Queryable 不带默认过滤）。
        var q = _db.Client.Queryable<StandardDirectoryConfig>()
            .Where(x => !x.IsDeleted && x.IsValid == 1);

        if (orgCode != null) { var v = orgCode; q = q.Where(x => x.OrgCode == v); }
        if (standardCode != null) { var v = standardCode; q = q.Where(x => x.StandardCode == v); }
        if (stageCode != null) { var v = stageCode; q = q.Where(x => x.StageCode == v); }
        if (enterpriseCode != null) { var v = enterpriseCode; q = q.Where(x => x.EnterpriseCode == v); }

        return await q.ToListAsync() ?? new List<StandardDirectoryConfig>();
    }

    public async Task<List<StandardDirectoryFolder>> GetDirectoryFoldersAsync(IEnumerable<string> configCodes)
    {
        var codes = new List<string>();
        foreach (var c in configCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<StandardDirectoryFolder>();

        var rows = await _db.GetListAsync<StandardDirectoryFolder>(x => codes.Contains(x.ConfigCode!));
        return rows.Data ?? new List<StandardDirectoryFolder>();
    }

    public async Task<List<DocTemplate>> GetDocTemplatesAsync(
        string? orgCode = null, string? standardCode = null, string? stageCode = null, bool onlyPublished = false)
    {
        var q = _db.Client.Queryable<DocTemplate>()
            .Where(x => !x.IsDeleted && x.IsValid == 1);

        if (orgCode != null) { var v = orgCode; q = q.Where(x => x.OrgCode == v); }
        if (standardCode != null) { var v = standardCode; q = q.Where(x => x.StandardCode == v); }
        if (stageCode != null) { var v = stageCode; q = q.Where(x => x.StageCode == v); }
        if (onlyPublished) q = q.Where(x => x.PublishStatus == "published");

        return await q.ToListAsync() ?? new List<DocTemplate>();
    }

    public async Task<List<DocTemplate>> GetDocTemplatesByStandardFileCodesAsync(IEnumerable<string> standardFileCodes)
    {
        var codes = new List<string>();
        foreach (var c in standardFileCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<DocTemplate>();

        var rows = await _db.GetListAsync<DocTemplate>(x => codes.Contains(x.StandardFileCode!));
        return rows.Data ?? new List<DocTemplate>();
    }
}
