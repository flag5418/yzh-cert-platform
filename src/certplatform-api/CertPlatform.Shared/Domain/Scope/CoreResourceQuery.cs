using System;
using System.Collections.Generic;
using System.Linq;
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
/// 的 <b>18 类实体</b>（含 <c>CertificationBody</c> / <c>Enterprise</c> /
/// <c>StandardDirectoryConfig</c> / <c>StandardDirectoryFolder</c> / <c>DocTemplate</c> /
/// <c>ValidationRule</c>（章节规则，2026-10-13 收口））。
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

    /// <summary>
    /// 取全部 <b>叶子标准</b>（<c>IsLeaf == true</c> 且未删）。
    /// <para><b>★ 语义</b>：等价于端内 <c>_db.Client.Queryable&lt;ISOStandard&gt;().Where(x => x.IsLeaf == true).OrderBy(x => x.Sort)</c>；
    /// 排序口径在端内完成（与 <see cref="GetStandardsAsync"/> 一致，本方法不固化排序）。</para>
    /// </summary>
    Task<List<ISOStandard>> GetLeafStandardsAsync();

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

    // ─────────────── 机构×标准×阶段 关联链（视图）· 2026-10-13 阶段 1.B 试点 ───────────────

    /// <summary>
    /// ★★ 取<b>「机构 → 标准 → 阶段」关联链</b>（含目录配置 Code）—— 底层为视图
    /// <c>v_org_standard_stage</c>（<b>一次取数</b>替代 6 次查询 + 内存组树）。
    ///
    /// <para><b>★ 返回值已做「逐级有效性归一」</b>（视图<b>不固化</b> <c>IsValid</c>，
    /// 判据收口在本方法 —— 见 2026-10-13 用户裁定「视图不固化业务有效性」）：
    /// <list type="number">
    ///   <item><c>OrgIsValid != 1</c> ⇒ <b>整行剔除</b>（机构无效 = 不存在）；</item>
    ///   <item>标准维无效（<c>StandardIsValid != 1</c> 或关联行 <c>OrgStandardIsValid != 1</c>）
    ///         ⇒ 置空 <c>StandardCode</c>（<b>保留该行</b>，否则「机构下无标准」会整行消失）；</item>
    ///   <item>阶段维无效（<c>StageIsValid != 1</c> 或关联行 <c>OrgStageIsValid != 1</c>）
    ///         ⇒ 置空 <c>StageCode</c>；</item>
    ///   <item>配置无效（<c>ConfigIsValid != 1</c>）⇒ 置空 <c>ConfigCode</c>。</item>
    /// </list>
    /// ⇒ 调用方只需判 <c>StandardCode == null</c> / <c>StageCode == null</c>，
    /// ⛔ <b>不要再自行写 <c>IsValid</c> 谓词</b>（那就是判据分裂）。</para>
    ///
    /// <para><b>⚠️ 不排序</b> —— 排序口径唯一实现在
    /// <c>OrgStandardTreeAssembler.OrderOrgs</c> / <c>OrderStandards</c> / <c>OrderStages</c>；
    /// 调用方按各自页面口径排序（⚠️ 原实现<b>无排序</b>的端点请保持无排序）。</para>
    /// </summary>
    Task<List<OrgStandardStageView>> GetOrgStandardStageChainAsync();

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

    /// <summary>按<b>宿主标准文件 <c>Code</c></b> 集合批量取提取规则（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<DocExtractionRule>> GetDocExtractionRulesByStandardFileCodesAsync(IEnumerable<string> standardFileCodes);

    // ─────────────────────── 标准目录文件 ───────────────────────

    /// <summary>取<b>全部有效</b>标准目录文件（对应端内 16 处冗余的 <c>x.IsValid == 1</c> 全表写法）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesAsync();

    /// <summary>
    /// 取<b>非企业行</b>（<c>EnterpriseCode</c> 为空）的<b>全部有效</b>标准目录文件 ——
    /// 「标准文档清单 / 模板候选」专用（企业私有文件不参与）。
    /// <para>★ 2026-10-14：判据 <c>string.IsNullOrEmpty(EnterpriseCode)</c> <b>下推 SQL</b>
    /// （<c>EnterpriseCode IS NULL OR = ''</c>）；⛔ 端内不得再以「<see cref="GetDirectoryFilesAsync"/>
    /// 全表取数 + 内存过滤」承接该谓词（违 <b>铁律 L5</b>：端内只允许投影，不许自己写 WHERE）。</para>
    /// </summary>
    Task<List<StandardDirectoryFile>> GetNonEnterpriseDirectoryFilesAsync();

    /// <summary>按目录配置 <c>Code</c> 取该目录下文件。<paramref name="includeDisabled"/> = <c>true</c> 时含已禁用行（「已移除槽位」仍属对账清单）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByConfigCodeAsync(string configCode, bool includeDisabled = false);

    /// <summary>按文件 <c>Code</c> 单查标准目录文件（未命中返回 <c>null</c>）。</summary>
    Task<StandardDirectoryFile?> GetDirectoryFileByCodeAsync(string? fileCode);

    /// <summary>
    /// 按文件 <c>Code</c> 单查<b>指定企业</b>的文件行（租户卫：「Code + EnterpriseCode」
    /// 双键谓词收口在本方法，端内 ⛔ 不要拆开手写第二处）。
    /// </summary>
    Task<StandardDirectoryFile?> GetDirectoryFileByCodeAsync(string? fileCode, string enterpriseCode);

    /// <summary>按文件 <c>Code</c> 集合批量取有效文件（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByCodesAsync(IEnumerable<string> fileCodes);

    /// <summary>按目录配置 <c>Code</c> 集合批量取文件（<paramref name="includeDisabled"/> 语义同单数版）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByConfigCodesAsync(IEnumerable<string> configCodes, bool includeDisabled = false);

    /// <summary>
    /// 按目录配置 <c>Code</c> 集合取<b>非企业行</b>（机构行 / 平台标准级）的目录文件。
    ///
    /// <para><b>★ 判据</b> = <c>OrgScope.IsEnterpriseRow</c> 的 <b>SQL 等价式</b>
    /// <c>string.IsNullOrEmpty(x.EnterpriseCode)</c>（该列 <c>NOT NULL</c>，见 <c>OrgScope</c> 类注释）。</para>
    ///
    /// <para><b>★ 用途</b>：树端点里「<b>该目录一共有几个文件</b>」的<b>分母</b>
    /// —— ⛔ 企业专属行不计入标准域分母（见 <c>EnterpriseNormalizeController.FillStandardTreeAsync</c>
    /// 的 <c>TotalFileCount</c>）。</para>
    ///
    /// <para>⚠️ 与 <see cref="GetDirectoryFilesByConfigCodesAsync"/> 的<b>唯一差别</b> = 是否限定非企业行：
    /// 后者<b>含</b>企业行（模板目录树 <c>GetTemplateDirectoryTreeAsync</c> 即用后者）。</para>
    /// </summary>
    Task<List<StandardDirectoryFile>> GetNonEnterpriseDirectoryFilesByConfigCodesAsync(IEnumerable<string> configCodes);

    /// <summary>按上传任务 <c>TaskId</c> 取文件（<paramref name="includeDisabled"/>：待激活/等转换行 <c>IsValid=0</c>，须显式带上）。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByTaskIdAsync(string taskId, bool includeDisabled = false);

    /// <summary>按企业（<c>EnterpriseCode</c>，文件表的机构维度）取文件行。</summary>
    Task<List<StandardDirectoryFile>> GetDirectoryFilesByEnterpriseCodeAsync(string enterpriseCode, bool includeDisabled = false);

    /// <summary>
    /// 按「企业 + <b>模板文档 Code</b>」定位企业上传行（企业行的 <c>StandardFileCode</c> = 模板行 <c>Code</c>）。
    /// <para>「文档全文」等工作流节点在运行期由模板 Code 反查企业实例行 ⇒ 收口在本方法
    /// （⛔ 端内不要另拼 <c>EnterpriseCode == … &amp;&amp; StandardFileCode == …</c> 的 WHERE）。</para>
    /// </summary>
    Task<StandardDirectoryFile?> FindEnterpriseDirectoryFileByTemplateAsync(string enterpriseCode, string standardFileCode);

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

    /// <summary>按企业 <c>Code</c> 单查企业（未命中返回 <c>null</c>）。</summary>
    Task<Enterprise?> GetEnterpriseByCodeAsync(string? enterpriseCode);

    /// <summary>按机构 <c>Code</c> 单查认证机构（未命中返回 <c>null</c>）。</summary>
    Task<CertificationBody?> GetCertificationBodyByCodeAsync(string? orgCode);

    /// <summary>
    /// 按机构 <c>Code</c> 单查认证机构（含已禁用行，<paramref name="includeDisabled"/> = <c>true</c>）。
    /// <para><b>★ 用途</b>：注册流程等场景须读禁用行（否则「已停用」错误消息无法显示机构名）。</para>
    /// </summary>
    Task<CertificationBody?> GetCertificationBodyByCodeAsync(string? orgCode, bool includeDisabled);

    /// <summary>按 <c>CbCode</c>（认证机构业务编号）单查认证机构（未命中返回 <c>null</c>）。</summary>
    Task<CertificationBody?> GetCertificationBodyByCbCodeAsync(string? cbCode);

    /// <summary>
    /// 取 <b>全部有效且 <c>Status == 'active'</c></b> 的认证机构（供注册页下拉使用）。
    /// <para><b>★ 语义</b>：等价于端内 <c>_db.Client.Queryable&lt;CertificationBody&gt;().Where(x => x.IsValid == 1 &amp;&amp; x.Status == "active")</c>。</para>
    /// </summary>
    Task<List<CertificationBody>> GetRegistrableCertificationBodiesAsync();

    /// <summary>按机构 <c>Code</c> 集合批量取认证机构（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<CertificationBody>> GetCertificationBodiesByCodesAsync(IEnumerable<string> orgCodes);

    // ─────────────── 目录配置 / 文件夹 / 模板 · 2026-10-13 阶段 1.B 扩充 ───────────────

    /// <summary>
    /// 取标准目录配置。<paramref name="orgCode"/> / <paramref name="standardCode"/> /
    /// <paramref name="stageCode"/> / <paramref name="enterpriseCode"/> 为 <c>null</c> = 该维<b>不限</b>。
    /// <para>⚠️ 全 <c>null</c> = 取全部有效行（配置数在十量级，全量取回优于逐阶段 N+1）。</para>
    /// <para><paramref name="includeDisabled"/> = <c>true</c> 时跳过 <c>IsValid</c> 过滤
    /// （「企业目录 ensure / 机构漂移兜底」须认出禁用行并复活，否则 INSERT 撞唯一键）。</para>
    /// </summary>
    Task<List<StandardDirectoryConfig>> GetDirectoryConfigsAsync(
        string? orgCode = null, string? standardCode = null, string? stageCode = null,
        string? enterpriseCode = null, bool includeDisabled = false);

    /// <summary>按目录配置 <c>Code</c> 单查（未命中返回 <c>null</c>）。</summary>
    Task<StandardDirectoryConfig?> GetDirectoryConfigByCodeAsync(string? configCode);

    /// <summary>按目录配置 <c>Code</c> 集合取文件夹（空集合直接返回空，⛔ 不发查询）。<b>不排序</b>。</summary>
    Task<List<StandardDirectoryFolder>> GetDirectoryFoldersAsync(IEnumerable<string> configCodes);

    /// <summary>
    /// 按文件夹 <c>Code</c> 单查（未命中返回 <c>null</c>）。
    /// <para><paramref name="includeDisabled"/> = <c>true</c> 时含已禁用行
    /// （仍排除软删）—— 目录读模型按 folderCode 反查 ConfigCode 时须用 <c>true</c>：
    /// 禁用文件夹下的有效文件仍要能定位（否则「停用再启用」期间文件消失）。</para>
    /// </summary>
    Task<StandardDirectoryFolder?> GetDirectoryFolderByCodeAsync(string? folderCode, bool includeDisabled = false);

    /// <summary>按文件夹 <c>Code</c> 集合批量取有效文件夹（空集合直接返回空，⛔ 不发查询）。</summary>
    Task<List<StandardDirectoryFolder>> GetDirectoryFoldersByCodesAsync(IEnumerable<string> folderCodes);

    /// <summary>按 <c>TaskId</c> 取文件夹（<c>includeDisabled: true</c> 语义 —— 草稿文件夹 <c>IsValid=0</c>
    /// 缺省过滤会永远查不到 ⇒ 激活空转。仍排除软删）。</summary>
    Task<List<StandardDirectoryFolder>> GetDirectoryFoldersByTaskIdAsync(string taskId);

    // ─────────── 写侧查重原始行（含软删 / 禁用 —— uk 不含 IsDeleted，复活语义需要全状态行）· 1.B.6d-3 ───────────

    /// <summary>
    /// 按「机构 × 标准 × 阶段」（可选企业维）取目录配置<b>原始行</b>（含软删 / 禁用）。
    /// <para>★ 写侧查重 / 就地复活专用（<c>uk_org_ent_std_stage</c> 不含 <c>IsDeleted</c>）；
    /// 读侧一律走 <see cref="GetDirectoryConfigsAsync"/>，⛔ 不要混用。</para>
    /// <param name="enterpriseCode"><c>null</c> = 该维<b>不限</b>（机构级 ensure 只按三键）；
    /// 空串 = 精确匹配 <c>EnterpriseCode = ''</c>。</param>
    /// </summary>
    Task<StandardDirectoryConfig?> FindDirectoryConfigRawAsync(
        string orgCode, string standardCode, string stageCode, string? enterpriseCode = null);

    /// <summary>
    /// 同层同名文件夹查重<b>原始行</b>（含软删 / 禁用 —— <c>uk_cfg_parent_name</c> 不含 <c>IsDeleted</c>，
    /// 命中已删行须就地复活）。
    /// <param name="excludeFolderCode">改名场景排除自己（<c>null</c> = 不排除）。</param>
    /// </summary>
    Task<StandardDirectoryFolder?> FindDirectoryFolderRawAsync(
        string configCode, string parentCode, string folderName, string? excludeFolderCode = null);

    /// <summary>
    /// 同目录同路径文件查重<b>原始行</b>（含软删 / 禁用 —— <c>uk_cfg_fullpath</c> 不含 <c>IsDeleted</c>，
    /// 任何状态的行都必须复用否则撞唯一键）。
    /// </summary>
    Task<StandardDirectoryFile?> FindDirectoryFileRawByFullPathAsync(string configCode, string fullPath);

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

    /// <summary>按模板 <c>Code</c> 单查（未命中返回 <c>null</c>）。</summary>
    Task<DocTemplate?> GetDocTemplateByCodeAsync(string? templateCode);

    // ─────────── 章节 / NC 检查规则（cert_validation_rule）· 2026-10-13 阶段 1.B ───────────

    /// <summary>
    /// 按 <c>Code</c> 取单条检查规则（空 <c>Code</c> 返回 <c>null</c>，⛔ 不发查询）。
    /// <para>⚠️ <paramref name="includeDisabled"/> = <c>true</c> 时跳过 <c>IsValid</c> 过滤 ——
    /// <b>规则快照 / 复活分支专用</b>（任务创建时冻结的规则，停用后仍须可按快照执行）；
    /// 其余场景一律走默认过滤，⛔ 不要滥用。</para>
    /// </summary>
    Task<ValidationRule?> GetClauseRuleByCodeAsync(string code, bool includeDisabled = false);

    /// <summary>
    /// 按「阶段」或「<c>Code</c> 集合」批量取<b>有效</b>检查规则
    /// （框架 <c>GetListAsync</c> 内含 <c>IsDeleted=0</c> + <c>IsValid=1</c> ⇒ 谓词处 ⛔ 不重复写）。
    /// <para>两维<b>都为空</b> = 取全部有效行；给了 <paramref name="codes"/> 时以它为准
    /// （空集合直接返回空，⛔ 不发查询）。</para>
    /// </summary>
    Task<List<ValidationRule>> GetClauseRulesAsync(string? phaseCode = null, IEnumerable<string>? codes = null);
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

    public async Task<List<ISOStandard>> GetLeafStandardsAsync()
    {
        // 等价于端内 `_db.Client.Queryable<ISOStandard>().Where(x => x.IsLeaf == true)`；
        // 框架默认 IsDeleted=0 + IsValid=1，调用方 ⛔ 不要重复写。
        var rows = await _db.GetListAsync<ISOStandard>(x => x.IsLeaf == true);
        return rows.Data ?? new List<ISOStandard>();
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

    // ─────────────── 机构×标准×阶段 关联链（视图）· 2026-10-13 阶段 1.B 试点 ───────────────

    public async Task<List<OrgStandardStageView>> GetOrgStandardStageChainAsync()
    {
        // ① 一次取数：视图已把 6 表 JOIN + 配置两级兜底做完（MERGE，谓词可下推）
        var rows = await _db.GetListAsync<OrgStandardStageView>();
        var list = rows.Data ?? new List<OrgStandardStageView>();

        // ② 逐级有效性归一（视图不固化 IsValid ⇒ 判据只在这里）
        //    ⚠️ 标准/阶段/配置 无效时「置空」而非「删行」—— 否则上级维度会跟着消失。
        foreach (var r in list)
        {
            if (r.StandardIsValid != 1 || r.OrgStandardIsValid != 1) r.StandardCode = null;
            if (r.StandardCode == null || r.StageIsValid != 1 || r.OrgStageIsValid != 1) r.StageCode = null;
            if (r.StageCode == null || r.ConfigIsValid != 1) r.ConfigCode = null;
        }
        list.RemoveAll(r => r.OrgIsValid != 1);

        return list;
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

    public async Task<List<DocExtractionRule>> GetDocExtractionRulesByStandardFileCodesAsync(IEnumerable<string> standardFileCodes)
    {
        var codes = new List<string>();
        foreach (var c in standardFileCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<DocExtractionRule>();

        var rows = await _db.GetListAsync<DocExtractionRule>(x => codes.Contains(x.StandardFileCode!));
        return rows.Data ?? new List<DocExtractionRule>();
    }

    // ─────────────────────── 标准目录文件 ───────────────────────

    public async Task<List<StandardDirectoryFile>> GetDirectoryFilesAsync()
    {
        var rows = await _db.GetListAsync<StandardDirectoryFile>();
        return rows.Data ?? new List<StandardDirectoryFile>();
    }

    public async Task<List<StandardDirectoryFile>> GetNonEnterpriseDirectoryFilesAsync()
    {
        // 非企业行 = EnterpriseCode 为空（OrgScope.IsEnterpriseRow 的 SQL 等价式；该列 NOT NULL）
        var rows = await _db.GetListAsync<StandardDirectoryFile>(
            x => string.IsNullOrEmpty(x.EnterpriseCode));
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

    public async Task<StandardDirectoryFile?> GetDirectoryFileByCodeAsync(string? fileCode, string enterpriseCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode) || string.IsNullOrWhiteSpace(enterpriseCode)) return null;
        var fc = fileCode;
        var ec = enterpriseCode;
        var r = await _db.GetOneAsync<StandardDirectoryFile>(x => x.Code == fc && x.EnterpriseCode == ec);
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

    public async Task<List<StandardDirectoryFile>> GetNonEnterpriseDirectoryFilesByConfigCodesAsync(IEnumerable<string> configCodes)
    {
        var codes = new List<string>();
        foreach (var c in configCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<StandardDirectoryFile>();

        // 非企业行 = EnterpriseCode 为空（OrgScope.IsEnterpriseRow 的 SQL 等价式；该列 NOT NULL）
        // ⚠️ 谓词逐字沿用端内原写法；includeDisabled 默认 false ⇒ 追加 IsValid == 1（与原 x.IsValid == 1 等价）
        var rows = await _db.GetListAsync<StandardDirectoryFile>(
            x => codes.Contains(x.ConfigCode!) && string.IsNullOrEmpty(x.EnterpriseCode));
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

    public async Task<StandardDirectoryFile?> FindEnterpriseDirectoryFileByTemplateAsync(string enterpriseCode, string standardFileCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(standardFileCode)) return null;

        var ec = enterpriseCode;
        var sfc = standardFileCode;
        var r = await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.EnterpriseCode == ec && x.StandardFileCode == sfc);
        return r.Data;
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

    public async Task<Enterprise?> GetEnterpriseByCodeAsync(string? enterpriseCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode)) return null;
        var ec = enterpriseCode;
        var r = await _db.GetOneAsync<Enterprise>(x => x.Code == ec);
        return r.Data;
    }

    public async Task<CertificationBody?> GetCertificationBodyByCodeAsync(string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(orgCode)) return null;
        var oc = orgCode;
        var r = await _db.GetOneAsync<CertificationBody>(x => x.Code == oc);
        return r.Data;
    }

    public async Task<CertificationBody?> GetCertificationBodyByCbCodeAsync(string? cbCode)
    {
        if (string.IsNullOrWhiteSpace(cbCode)) return null;
        var cb = cbCode;
        var r = await _db.GetOneAsync<CertificationBody>(x => x.CbCode == cb);
        return r.Data;
    }

    public async Task<List<CertificationBody>> GetRegistrableCertificationBodiesAsync()
    {
        // 等价于端内 `_db.Client.Queryable<CertificationBody>().Where(x => x.IsValid == 1 && x.Status == "active")`；
        // 框架默认 IsDeleted=0，调用方 ⛔ 不要重复写。
        var rows = await _db.GetListAsync<CertificationBody>(x => x.Status == "active");
        return rows.Data ?? new List<CertificationBody>();
    }

    public async Task<List<CertificationBody>> GetCertificationBodiesByCodesAsync(IEnumerable<string> orgCodes)
    {
        var codes = new List<string>();
        foreach (var c in orgCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<CertificationBody>();

        var rows = await _db.GetListAsync<CertificationBody>(x => codes.Contains(x.Code!));
        return rows.Data ?? new List<CertificationBody>();
    }

    public async Task<CertificationBody?> GetCertificationBodyByCodeAsync(string? orgCode, bool includeDisabled)
    {
        if (string.IsNullOrWhiteSpace(orgCode)) return null;
        var oc = orgCode;
        if (includeDisabled)
        {
            // 只滤软删、不过滤 IsValid —— 语义 = includeDisabled（见接口注释）
            var ignored = await _db.GetOneIgnoreValidAsync<CertificationBody>(x => x.Code == oc);
            return ignored.Data;
        }
        var r = await _db.GetOneAsync<CertificationBody>(x => x.Code == oc);
        return r.Data;
    }

    // ─────────────── 目录配置 / 文件夹 / 模板 · 2026-10-13 阶段 1.B 扩充 ───────────────

    public async Task<List<StandardDirectoryConfig>> GetDirectoryConfigsAsync(
        string? orgCode = null, string? standardCode = null, string? stageCode = null,
        string? enterpriseCode = null, bool includeDisabled = false)
    {
        // ⚠️ 4 维 ⇒ 16 种组合，用显式分支会爆炸 ⇒ 这里走链式 Where。
        //    判据与 GetListAsync 口径一致：!IsDeleted（恒有）+ IsValid == 1（includeDisabled=false 时）。
        var q = _db.Client.Queryable<StandardDirectoryConfig>()
            .Where(x => !x.IsDeleted);
        if (!includeDisabled) q = q.Where(x => x.IsValid == 1);

        if (orgCode != null) { var v = orgCode; q = q.Where(x => x.OrgCode == v); }
        if (standardCode != null) { var v = standardCode; q = q.Where(x => x.StandardCode == v); }
        if (stageCode != null) { var v = stageCode; q = q.Where(x => x.StageCode == v); }
        if (enterpriseCode != null) { var v = enterpriseCode; q = q.Where(x => x.EnterpriseCode == v); }

        return await q.ToListAsync() ?? new List<StandardDirectoryConfig>();
    }

    public async Task<StandardDirectoryConfig?> GetDirectoryConfigByCodeAsync(string? configCode)
    {
        if (string.IsNullOrWhiteSpace(configCode)) return null;
        var cc = configCode;
        var r = await _db.GetOneAsync<StandardDirectoryConfig>(x => x.Code == cc);
        return r.Data;
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

    public async Task<StandardDirectoryFolder?> GetDirectoryFolderByCodeAsync(string? folderCode, bool includeDisabled = false)
    {
        if (string.IsNullOrWhiteSpace(folderCode)) return null;
        var fc = folderCode;
        if (includeDisabled)
        {
            // 只滤软删、不过滤 IsValid —— 语义 = includeDisabled（见接口注释）
            var ignored = await _db.GetOneIgnoreValidAsync<StandardDirectoryFolder>(x => x.Code == fc);
            return ignored.Data;
        }
        var r = await _db.GetOneAsync<StandardDirectoryFolder>(x => x.Code == fc);
        return r.Data;
    }

    public async Task<List<StandardDirectoryFolder>> GetDirectoryFoldersByCodesAsync(IEnumerable<string> folderCodes)
    {
        var codes = new List<string>();
        foreach (var c in folderCodes)
            if (!string.IsNullOrWhiteSpace(c)) codes.Add(c);
        if (codes.Count == 0) return new List<StandardDirectoryFolder>();

        var rows = await _db.GetListAsync<StandardDirectoryFolder>(x => codes.Contains(x.Code!));
        return rows.Data ?? new List<StandardDirectoryFolder>();
    }

    public async Task<List<StandardDirectoryFolder>> GetDirectoryFoldersByTaskIdAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return new List<StandardDirectoryFolder>();

        var tid = taskId;
        // includeDisabled：草稿文件夹 IsValid=0，缺省过滤会永远查不到 ⇒ 激活空转
        var rows = await _db.GetListAsync<StandardDirectoryFolder>(x => x.TaskId == tid, includeDisabled: true);
        return rows.Data ?? new List<StandardDirectoryFolder>();
    }

    public async Task<StandardDirectoryConfig?> FindDirectoryConfigRawAsync(
        string orgCode, string standardCode, string stageCode, string? enterpriseCode = null)
    {
        // ⚠️ orgCode ⛔ 不能做空白守卫 —— 平台通用模板回退就是 OrgCode = ""（必须能查）。
        if (string.IsNullOrWhiteSpace(standardCode) || string.IsNullOrWhiteSpace(stageCode))
            return null;

        // 原始行 = ⛔ 不加 IsDeleted / IsValid 过滤（复活语义需要已删 / 禁用行）
        var q = _db.Client.Queryable<StandardDirectoryConfig>()
            .Where(x => x.OrgCode == orgCode
                     && x.StandardCode == standardCode && x.StageCode == stageCode);
        if (enterpriseCode != null) q = q.Where(x => x.EnterpriseCode == enterpriseCode);
        return await q.FirstAsync();
    }

    public async Task<StandardDirectoryFolder?> FindDirectoryFolderRawAsync(
        string configCode, string parentCode, string folderName, string? excludeFolderCode = null)
    {
        if (string.IsNullOrWhiteSpace(configCode) || string.IsNullOrWhiteSpace(folderName))
            return null;

        var q = _db.Client.Queryable<StandardDirectoryFolder>()
            .Where(x => x.ConfigCode == configCode && x.ParentCode == parentCode
                        && x.FolderName == folderName);
        if (excludeFolderCode != null) q = q.Where(x => x.Code != excludeFolderCode);
        return await q.FirstAsync();
    }

    public async Task<StandardDirectoryFile?> FindDirectoryFileRawByFullPathAsync(string configCode, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(configCode) || string.IsNullOrWhiteSpace(fullPath))
            return null;

        return await _db.Client.Queryable<StandardDirectoryFile>()
            .Where(x => x.ConfigCode == configCode && x.FullPath == fullPath)
            .FirstAsync();
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

    public async Task<DocTemplate?> GetDocTemplateByCodeAsync(string? templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode)) return null;
        var tc = templateCode;
        var r = await _db.GetOneAsync<DocTemplate>(x => x.Code == tc);
        return r.Data;
    }

    // ─────────── 章节 / NC 检查规则（cert_validation_rule）· 2026-10-13 阶段 1.B ───────────

    public async Task<ValidationRule?> GetClauseRuleByCodeAsync(string code, bool includeDisabled = false)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code;
        var rows = await _db.GetListAsync<ValidationRule>(x => x.Code == c, includeDisabled: includeDisabled);
        return rows.Data?.FirstOrDefault();
    }

    public async Task<List<ValidationRule>> GetClauseRulesAsync(string? phaseCode = null, IEnumerable<string>? codes = null)
    {
        if (codes != null)
        {
            var set = new List<string>();
            foreach (var c in codes)
                if (!string.IsNullOrWhiteSpace(c)) set.Add(c);
            if (set.Count == 0) return new List<ValidationRule>();

            var byCodes = await _db.GetListAsync<ValidationRule>(x => set.Contains(x.Code!));
            return byCodes.Data ?? new List<ValidationRule>();
        }

        if (phaseCode != null)
        {
            var p = phaseCode;
            var byPhase = await _db.GetListAsync<ValidationRule>(x => x.PhaseCode == p);
            return byPhase.Data ?? new List<ValidationRule>();
        }

        var all = await _db.GetListAsync<ValidationRule>();
        return all.Data ?? new List<ValidationRule>();
    }
}
