using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Fill.Resolvers;

namespace CertPlatform.Admin.Controllers.Cert;

/// <summary>
/// 体系认证全局参数定义控制器（后台管理）
///
/// <para><b>业务定位</b>：认证机构按「机构 × 标准 × 阶段」预定义体系认证全局参数。
/// 这些参数是标准文档填充时的主要取值来源。</para>
///
/// <para><b>路由前缀</b>：<c>/api/Cert/FillParamDef</c></para>
/// <para><b>数据库</b>：<c>cert_fill_param_def</c></para>
///
/// <para><b>设计依据</b>：05 册 <c>22</c> §七（全局参数 2 表）、<c>23</c> §四（标准 × 阶段裁剪）；
/// 实测必要性见 <c>25-纸面实验实测报告-V1.md</c> §8.5。</para>
///
/// <para><b>★ 自定义端点</b>：<c>scopes</c>（机构/标准/阶段下拉）、
/// <c>enterprise-attrs</c>（企业属性目录 —— 后台配置取值表达式时「选企业属性」用）。</para>
/// </summary>
[ApiController]
/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Cert/[controller]")]
public class FillParamDefController : YzhControllerBase<FillParamDef>
{
    private readonly IDbOrm _db;

    public FillParamDefController(
        EntityService<FillParamDef> entityService,
        IUserContext userContext,
        IDbOrm db)
        : base(entityService, userContext)
    {
        _db = db;
    }

    /// <summary>★ 开发期强制暴露缺配置问题（缺 JSON 直接 throw，不静默空白）</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig 配置</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<FillParamDef>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、CRUD 校验钩子
    // ════════════════════════════════════════════════════════════════════

    /// <summary>新增前校验：同一「机构+标准+阶段」下 ParamCode 唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(FillParamDef entity)
    {
        if (string.IsNullOrWhiteSpace(entity.OrgCode))
            return (false, "所属机构不能为空");
        if (string.IsNullOrWhiteSpace(entity.ParamCode))
            return (false, "参数编码不能为空");

        // ★ 唯一键不含 IsDeleted ⇒ 查重必须「含已删」；命中已删行则复活它
        //   （GetOneIgnoreValidAsync 只过滤软删除、不过滤 IsValid —— 见 REFERENCE §二十 ㉖）
        var existing = await _db.GetOneIgnoreValidAsync<FillParamDef>(p =>
            p.OrgCode == entity.OrgCode
            && p.StandardCode == entity.StandardCode
            && p.StageCode == entity.StageCode
            && p.ParamCode == entity.ParamCode);

        if (existing.Success && existing.Data != null)
        {
            if (existing.Data.IsDeleted)
            {
                // 复活：把已删行交还给框架更新（Code 沿用，避免产生第二行）
                entity.Code = existing.Data.Code;
                entity.Id = existing.Data.Id;
                entity.IsDeleted = false;
                entity.DeleteBy = null;
                entity.DeleteTime = null;
                entity.CreateTime = existing.Data.CreateTime;
                entity.CreateBy = existing.Data.CreateBy;
                return (true, null);
            }
            return (false, $"同一「机构 + 标准 + 阶段」下参数编码【{entity.ParamCode}】已存在");
        }

        return (true, null);
    }

    /// <summary>修改前校验：ParamCode 唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(FillParamDef entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        var dup = await Entity.ExistsAsync(p =>
            p.Code != entity.Code
            && p.OrgCode == entity.OrgCode
            && p.StandardCode == entity.StandardCode
            && p.StageCode == entity.StageCode
            && p.ParamCode == entity.ParamCode);

        if (dup.Data)
            return (false, $"同一「机构 + 标准 + 阶段」下参数编码【{entity.ParamCode}】已存在");

        // ★ 内置参数的 ParamCode 不允许改（改了会断裂已配置的锚点引用）
        var current = await Entity.GetOne(p => p.Code == entity.Code);
        if (current.Success && current.Data != null && current.Data.IsBuiltin)
        {
            if (!string.Equals(current.Data.ParamCode, entity.ParamCode, StringComparison.Ordinal))
                return (false, "内置参数的「参数编码」不可修改（已配置的文档锚点依赖它）");
        }

        return (true, null);
    }

    /// <summary>删除前校验：内置参数不可删除</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (false, "未选择要删除的记录");

        // includeDisabled: true —— 已禁用的内置参数同样不可删
        var builtin = await Entity.GetListAsync(
            p => codes.Contains(p.Code) && p.IsBuiltin, includeDisabled: true);

        if (builtin.Success && builtin.Data != null && builtin.Data.Count > 0)
        {
            var names = string.Join("、", builtin.Data.Select(x => x.ParamName));
            return (false, $"内置参数不可删除（{names}）。如需停用，请把「状态」改为禁用。");
        }

        return (true, null);
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 作用域下拉数据：机构 / 标准 / 阶段。
    /// <para>供前端在「所属机构 / 所属标准 / 所属阶段」三个 ComboBox 上注入选项。</para>
    /// </summary>
    [HttpGet("scopes")]
    public async Task<IActionResult> Scopes()
    {
        var orgs = await _db.Client.Queryable<CertificationBody>()
            .Where(x => x.IsDeleted == false && x.IsValid == 1)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name })
            .ToListAsync();

        // 标准是树形表（iso-standard 左树右表样板）→ 下拉只取叶子，否则目录节点会混进选项
        var standards = await _db.Client.Queryable<ISOStandard>()
            .Where(x => x.IsDeleted == false && x.IsValid == 1 && x.IsLeaf == true)
            .OrderBy(x => x.Sort)
            .Select(x => new { x.Code, Name = x.StandardName, x.StandardCode })
            .ToListAsync();

        var stages = await _db.Client.Queryable<CertStage>()
            .Where(x => x.IsDeleted == false && x.IsValid == 1)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Code, Name = x.StageName, x.StageCode })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            orgs = orgs.Select(x => new { value = x.Code, label = x.Name }).ToList(),
            standards = standards.Select(x => new { value = x.Code, label = x.Name, no = x.StandardCode }).ToList(),
            stages = stages.Select(x => new { value = x.Code, label = x.Name, no = x.StageCode }).ToList(),
        }));
    }

    /// <summary>
    /// ★ 企业属性目录 —— 「自动形成带企业所有属性的列表，让用户选择」。
    ///
    /// <para>返回 <c>cert_enterprise</c> 的全部业务属性（字段名 + 中文名 + 示例值），
    /// 供后台在配置「取值表达式 SourceExpr」时点选，而不是让用户手敲字段名。</para>
    ///
    /// <para><b>返回的 <c>Expr</c> 即写入 <c>cert_fill_param_def.SourceExpr</c> 的值</b>，
    /// 形如 <c>enterprise.Name</c>，由填充引擎的 <c>GlobalParamResolver</c> 解析。</para>
    /// </summary>
    [HttpGet("enterprise-attrs")]
    public async Task<IActionResult> EnterpriseAttrs([FromQuery] string? enterpriseCode)
    {
        // ★ 属性中文名取 ReplaceResolver.EnterpriseAttrLabels（全项目唯一口径）——
        //   若在此另写一份，新增企业字段时两处会漂移：后台能选、填充引擎认不出，
        //   或者反之。这是本仓反复出现的「两套口径」缺陷，不要重犯。
        //   此处只补「值类型」与「分组」两项展示信息。
        var valueTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EmployeeCount"] = "number",
            ["ArchiveDate"] = "date",
        };

        // 取一台真实企业做「示例值」——让机构看得见每个属性长什么样
        Enterprise? sample = null;
        if (!string.IsNullOrWhiteSpace(enterpriseCode))
        {
            var r = await _db.Client.Queryable<Enterprise>()
                .Where(x => x.Code == enterpriseCode).FirstAsync();
            sample = r;
        }
        sample ??= await _db.Client.Queryable<Enterprise>()
            .Where(x => x.IsDeleted == false && x.IsValid == 1)
            .OrderBy(x => x.Id)
            .FirstAsync();

        var sampleInfo = sample == null
            ? new EnterpriseInfo()
            : new EnterpriseInfo
            {
                Code = sample.Code,
                Name = sample.Name,
                ShortName = sample.ShortName,
                CreditCode = sample.CreditCode,
                LegalPerson = sample.LegalPerson,
                Province = sample.Province,
                City = sample.City,
                Address = sample.Address,
                IndustryType = sample.IndustryType,
                EmployeeCount = sample.EmployeeCount,
                CertScope = sample.CertScope,
                ContactName = sample.ContactName,
                ContactPhone = sample.ContactPhone,
                ContactEmail = sample.ContactEmail,
                EnterpriseNo = sample.EnterpriseNo,
                ArchiveDate = sample.ArchiveDate,
            };

        var items = ReplaceResolver.EnterpriseAttrLabels.Select(kv => new
        {
            attr = kv.Key,
            label = kv.Value,
            group = "基础信息",
            valueType = valueTypes.TryGetValue(kv.Key, out var vt) ? vt : "text",
            expr = $"enterprise.{kv.Key}",
            sample = sampleInfo.Get(kv.Key),
        }).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            enterpriseCode = sample?.Code,
            enterpriseName = sample?.Name,
            items,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、★ 生效参数集（左树右表 —— 选中「机构 × 标准 × 阶段」后的右表数据）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>生效参数集</b>：选中一个「机构 × 标准 × 阶段」后，返回填充引擎<b>实际会用到</b>的那批参数。
    ///
    /// <para><b>为什么不能直接用基类的 <c>/filter</c></b>：参数的作用域可以「不限标准 / 不限阶段」，
    /// 生效条件是
    /// <c>OrgCode = X AND (StandardCode = '' OR StandardCode = S) AND (StageCode = '' OR StageCode = P)</c>
    /// —— 这是 <b>OR 组合</b>，<c>FilterItem</c> 的 AND 语义表达不了；且同一 <c>ParamCode</c>
    /// 可能有多条（通用 + 标准专属），必须按「更具体优先」去重取一条。</para>
    ///
    /// <para><b>去重口径 = <see cref="ParamValueResolver.PickMostSpecific"/></b>，与企业端
    /// 「合并清单」（<c>FillParamValueController.MergeList</c>）<b>共用同一实现</b>，
    /// ⛔ 不在此另写一份 —— 两套口径的后果是「后台看到 A 条生效、文档里填的却是 B 条的值」，
    /// 且两边都不报错。</para>
    ///
    /// <para><b>返回两类行</b>：① <b>生效行</b>（每个 ParamCode 一条，带 <c>ScopeKind</c>/<c>ScopeText</c>）；
    /// ② <b>未生效行</b>（<c>IncludeShadowed=true</c> 时返回，带 <c>ShadowReason</c>）——
    /// 让管理员看得见「我配的这条为什么没生效」（被更具体的覆写 / 已禁用）。</para>
    ///
    /// <para>⚠️ <b>不分页</b>：生效集是「一个作用域下的全部参数」，实测规模 &lt; 100 条；
    /// 前端在本地做关键字过滤，交互更快。若将来单作用域参数破千，再改为服务端分页。</para>
    /// </summary>
    [HttpPost("effective")]
    public async Task<IActionResult> Effective([FromBody] EffectiveRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.OrgCode))
            return Ok(ApiResponse<object>.Fail("请先选择机构"));

        var stdCode = req.StandardCode ?? string.Empty;
        var stageCode = req.StageCode ?? string.Empty;

        // ── 1. 取候选：标准 / 阶段留空 = 通配（与企业端 merge-list 完全同口径）──
        var candidates = await _db.Client.Queryable<FillParamDef>()
            .Where(d => d.OrgCode == req.OrgCode && d.IsDeleted == false
                        && (d.StandardCode == "" || d.StandardCode == stdCode)
                        && (d.StageCode == "" || d.StageCode == stageCode))
            .OrderBy(d => d.SortOrder)
            .ToListAsync();

        // ── 2. 生效集：只从「启用」行里选，同一 ParamCode 取更具体的那条 ──
        var picked = ParamValueResolver.PickMostSpecific(candidates.Where(d => d.IsValid == 1));
        var pickedCodes = new HashSet<string>(picked.Select(d => d.Code), StringComparer.Ordinal);

        // ── 3. 未生效行（被覆写 / 已禁用）—— 默认不下发 ──
        var shadowed = candidates.Where(d => !pickedCodes.Contains(d.Code)).ToList();

        // ── 4. 作用域名称：只查候选里真正出现的标准 / 阶段（避免全表扫）──
        var stdCodes = candidates
            .Where(d => !string.IsNullOrWhiteSpace(d.StandardCode))
            .Select(d => d.StandardCode).Distinct().ToList();
        var stageCodes = candidates
            .Where(d => !string.IsNullOrWhiteSpace(d.StageCode))
            .Select(d => d.StageCode).Distinct().ToList();

        var stdNames = stdCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<ISOStandard>()
                .Where(x => stdCodes.Contains(x.Code))
                .Select(x => new { x.Code, Name = x.StandardName })
                .ToListAsync())
              .ToDictionary(x => x.Code, x => x.Name, StringComparer.Ordinal);

        var stageNames = stageCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<CertStage>()
                .Where(x => stageCodes.Contains(x.Code))
                .Select(x => new { x.Code, Name = x.StageName })
                .ToListAsync())
              .ToDictionary(x => x.Code, x => x.Name, StringComparer.Ordinal);

        // 作用域 → (kind, 可读文字)。★ 局部函数，闭包捕获上面两个字典
        (string Kind, string Text) ScopeOf(FillParamDef d)
        {
            var hasStd = !string.IsNullOrWhiteSpace(d.StandardCode);
            var hasStage = !string.IsNullOrWhiteSpace(d.StageCode);

            var stdText = hasStd
                ? (stdNames.TryGetValue(d.StandardCode, out var sn) ? sn : d.StandardCode)
                : string.Empty;
            var stageText = hasStage
                ? (stageNames.TryGetValue(d.StageCode, out var jn) ? jn : d.StageCode)
                : string.Empty;

            return (hasStd, hasStage) switch
            {
                (true, true) => ("standardStage", $"{stdText} · {stageText}"),
                (true, false) => ("standard", stdText),
                (false, true) => ("stage", stageText),
                // ★ 文字里不再重复「通用」二字 —— 前端会并排渲染 ScopeKind 标签 + 本文字
                _ => ("common", "不限标准 / 阶段"),
            };
        }

        // ── 5. 关键字过滤（ParamCode / ParamName 模糊匹配）──
        var keyword = req.Keyword?.Trim() ?? string.Empty;
        bool Hit(FillParamDef d) =>
            keyword.Length == 0
            || (d.ParamCode?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (d.ParamName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);

        var effectiveRows = picked.Where(Hit).ToList();
        var visibleCodes = new HashSet<string>(effectiveRows.Select(d => d.ParamCode), StringComparer.Ordinal);

        var items = effectiveRows.Select(d =>
        {
            var (kind, text) = ScopeOf(d);
            return new EffectiveItem
            {
                Code = d.Code,
                OrgCode = d.OrgCode,
                StandardCode = d.StandardCode,
                StageCode = d.StageCode,
                ParamCode = d.ParamCode,
                ParamName = d.ParamName,
                GroupName = d.GroupName ?? string.Empty,
                ValueType = d.ValueType,
                EnumOptions = d.EnumOptions,
                SourceKind = d.SourceKind,
                SourceExpr = d.SourceExpr,
                MaintainMode = d.MaintainMode,
                DefaultValue = d.DefaultValue,
                Placeholder = d.Placeholder,
                IsRequired = d.IsRequired,
                IsBuiltin = d.IsBuiltin,
                SortOrder = d.SortOrder,
                Description = d.Description,
                IsValid = d.IsValid,
                ScopeKind = kind,
                ScopeText = text,
                ScopeLevel = ParamValueResolver.Specificity(d),
                IsEffective = true,
                ShadowReason = string.Empty,
                ShadowedBy = string.Empty,
                // ★ 「本行覆写掉了哪些更宽的定义」—— 让管理员知道改动的影响面
                ShadowedScopes = shadowed
                    .Where(s => string.Equals(s.ParamCode, d.ParamCode, StringComparison.Ordinal))
                    .Select(s => ScopeOf(s).Text)
                    .ToList(),
            };
        }).ToList();

        // ── 6. 未生效行（仅 IncludeShadowed）──
        var shadowItems = new List<EffectiveItem>();
        if (req.IncludeShadowed)
        {
            foreach (var s in shadowed.Where(s => visibleCodes.Contains(s.ParamCode)))
            {
                var winner = picked.FirstOrDefault(p =>
                    string.Equals(p.ParamCode, s.ParamCode, StringComparison.Ordinal));
                var (kind, text) = ScopeOf(s);
                shadowItems.Add(new EffectiveItem
                {
                    Code = s.Code,
                    OrgCode = s.OrgCode,
                    StandardCode = s.StandardCode,
                    StageCode = s.StageCode,
                    ParamCode = s.ParamCode,
                    ParamName = s.ParamName,
                    GroupName = s.GroupName ?? string.Empty,
                    ValueType = s.ValueType,
                    EnumOptions = s.EnumOptions,
                    SourceKind = s.SourceKind,
                    SourceExpr = s.SourceExpr,
                    MaintainMode = s.MaintainMode,
                    DefaultValue = s.DefaultValue,
                    Placeholder = s.Placeholder,
                    IsRequired = s.IsRequired,
                    IsBuiltin = s.IsBuiltin,
                    SortOrder = s.SortOrder,
                    Description = s.Description,
                    IsValid = s.IsValid,
                    ScopeKind = kind,
                    ScopeText = text,
                    ScopeLevel = ParamValueResolver.Specificity(s),
                    IsEffective = false,
                    // overridden = 被更具体的同名参数覆写；disabled = 本行已禁用
                    ShadowReason = s.IsValid == 1 ? "overridden" : "disabled",
                    ShadowedBy = winner?.Code ?? string.Empty,
                });
            }
        }

        var all = items.Concat(shadowItems).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            // ★ 整个响应一律 PascalCase（含外层匿名对象）—— 与项目契约一致，
            //   ⛔ 不要因为「别的端点用了小写」就跟风；契约一致性优先于局部模仿
            OrgCode = req.OrgCode,
            StandardCode = stdCode,
            StageCode = stageCode,
            Items = all,
            Stats = new
            {
                EffectiveCount = items.Count,
                ShadowedCount = shadowItems.Count,
                ByScope = new
                {
                    Common = items.Count(x => x.ScopeKind == "common"),
                    Standard = items.Count(x => x.ScopeKind == "standard"),
                    Stage = items.Count(x => x.ScopeKind == "stage"),
                    StandardStage = items.Count(x => x.ScopeKind == "standardStage"),
                },
            },
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、请求 / 响应 DTO
    // ════════════════════════════════════════════════════════════════════

    /// <summary>生效参数集请求</summary>
    public sealed class EffectiveRequest
    {
        /// <summary>机构编码（必填）</summary>
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准 Code（空 = 只看通用 + 阶段专属）</summary>
        public string? StandardCode { get; set; }

        /// <summary>阶段 Code（空 = 只看通用 + 标准专属）</summary>
        public string? StageCode { get; set; }

        /// <summary>关键字（按 ParamCode / ParamName 模糊匹配，大小写不敏感）</summary>
        public string? Keyword { get; set; }

        /// <summary>是否附带「未生效的定义」（被覆写 / 已禁用），默认 false</summary>
        public bool IncludeShadowed { get; set; }
    }

    /// <summary>
    /// 生效参数行。
    /// <para>前 18 个属性与 <c>cert_fill_param_def</c> 逐字段对应；后 6 个是<b>计算列</b>
    /// （DB 里没有，由 <see cref="Effective"/> 算出）。</para>
    /// </summary>
    public sealed class EffectiveItem
    {
        // ──── 定义本体 ────
        public string Code { get; set; } = string.Empty;
        public string OrgCode { get; set; } = string.Empty;
        public string StandardCode { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public string ParamCode { get; set; } = string.Empty;
        public string ParamName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string ValueType { get; set; } = "text";
        public string? EnumOptions { get; set; }
        public string SourceKind { get; set; } = "global";
        public string? SourceExpr { get; set; }
        public string MaintainMode { get; set; } = "auto";
        public string? DefaultValue { get; set; }
        public string? Placeholder { get; set; }
        public bool IsRequired { get; set; }
        public bool IsBuiltin { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public int IsValid { get; set; }

        // ──── ★ 计算列 ────
        /// <summary>作用域类别：common | standard | stage | standardStage</summary>
        public string ScopeKind { get; set; } = "common";
        /// <summary>作用域可读文字（如「ISO 9001:2015 · 复审」；通用时为「不限标准 / 阶段」）</summary>
        public string ScopeText { get; set; } = "不限标准 / 阶段";
        /// <summary>具体度 0..3（= <c>ParamValueResolver.Specificity</c>）：越大越具体</summary>
        public int ScopeLevel { get; set; }
        /// <summary>本行是否生效（false = 被覆写或已禁用）</summary>
        public bool IsEffective { get; set; } = true;
        /// <summary>未生效原因：overridden（被更具体覆写）| disabled（已禁用）</summary>
        public string ShadowReason { get; set; } = string.Empty;
        /// <summary>覆写本行的生效行 Code（仅未生效行有值）</summary>
        public string ShadowedBy { get; set; } = string.Empty;
        /// <summary>本行覆写掉的更宽定义的作用域文字（仅生效行有值）</summary>
        public List<string> ShadowedScopes { get; set; } = new();
    }
}
