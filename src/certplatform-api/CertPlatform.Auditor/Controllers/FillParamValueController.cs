using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Fill.Resolvers;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业全局参数值控制器（专家端 —— 「企业全局参数定义」页的数据口）
///
/// <para><b>业务定位</b>：把「后台按机构×标准×阶段预定义的全局参数」与「企业已有基本信息」
/// <b>自动合并成一张待完善清单</b>，让企业只需填真正缺的那几项。
/// 这正是 05 册 <c>22</c> §七 去重裁定的落地形态：
/// <b>企业基本信息与全局参数不是两套数据，而是「同一份数据的两个视角」</b>。</para>
///
/// <para><b>★ 三条语义（决定本类全部行为，改动前必读）</b>：</para>
/// <list type="number">
///   <item><b>合并列表是纯读</b> —— <c>merge-list</c> 不写库。自动项（<c>MaintainMode=auto</c>）
///         的值<b>每次实时</b>从 <c>cert_enterprise</c> 取，不落 <c>cert_fill_param_value</c>。
///         理由：<c>auto</c> 的语义是「永远等于企业档案」，若快照进值表，
///         企业改了档案而值表没刷新 → 文档里印着旧地址，<b>两边都不报错</b>。</item>
///   <item><b>只有人工项才落库</b> —— <c>save</c> 只持久化 <c>manual</c> / <c>both</c> 两类；
///         <c>auto</c> 项即使客户端传了值也<b>被忽略</b>（语义不允许覆盖）。
///         故 <c>cert_fill_param_value</c> 里<b>不会有</b> auto 参数的行 —— 这是设计，不是缺陷。</item>
///   <item><b>定义冲突取「更具体」</b> —— 同一 <c>ParamCode</c> 可能同时存在
///         「不限标准」(<c>StandardCode=''</c>) 与「指定标准」两条定义。
///         取 specificity 高者（指定标准 > 指定阶段 > 通配），平手时取先创建者。
///         这是 <c>23</c> §四「标准 × 阶段裁剪」的执行点。</item>
/// </list>
///
/// <para><b>路由前缀</b>：<c>/api/Auditor/FillParamValue</c></para>
/// <para><b>工作区隔离</b>：<c>OrgCode = 当前工作区</c>（经 <see cref="WorkspaceContextService"/> 解析）；
/// 写端点额外校验企业归属，解析失败一律<b>抛错不静默放行</b>。</para>
/// </summary>
[ApiController]
[Route("api/Auditor/[controller]")]
public class FillParamValueController : YzhControllerBase<FillParamValue>
{
    private readonly IDbOrm _db;
    private readonly WorkspaceContextService _workspace;

    public FillParamValueController(
        EntityService<FillParamValue> entityService,
        IUserContext userContext,
        IDbOrm db,
        WorkspaceContextService workspace)
        : base(entityService, userContext)
    {
        _db = db;
        _workspace = workspace;
    }

    /// <summary>缺 EntityConfig 时仅告警（本页是自定义合并视图，不依赖配置驱动的列）</summary>
    protected override bool StrictConfigLoad => false;

    protected override EntityConfig LoadConfig() => EntityConfigHelper.GetConfig<FillParamValue>();

    // ════════════════════════════════════════════════════════════════════
    // 一、作用域下拉（标准 / 阶段）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 标准 / 阶段下拉。
    /// <para>企业端<b>不提供机构下拉</b> —— 机构 = 当前工作区，由服务端解析，
    /// 让用户选机构等于给越权留口子。</para>
    /// </summary>
    [HttpGet("scopes")]
    public async Task<IActionResult> Scopes()
    {
        // 标准是树形表 → 只取叶子，否则目录节点会混进选项
        var standards = await _db.Client.Queryable<ISOStandard>()
            .Where(x => x.IsDeleted == false && x.IsValid == 1 && x.IsLeaf == true)
            .OrderBy(x => x.Sort)
            .Select(x => new { x.Code, Name = x.StandardName, No = x.StandardCode })
            .ToListAsync();

        var stages = await _db.Client.Queryable<CertStage>()
            .Where(x => x.IsDeleted == false && x.IsValid == 1)
            .OrderBy(x => x.SortOrder)
            .Select(x => new { x.Code, Name = x.StageName, No = x.StageCode })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            standards = standards.Select(x => new { value = x.Code, label = x.Name, no = x.No }).ToList(),
            stages = stages.Select(x => new { value = x.Code, label = x.Name, no = x.No }).ToList(),
        }));
    }

    /// <summary>本工作区企业下拉（合并列表的「对象选择器」）</summary>
    [HttpGet("enterprises")]
    public async Task<IActionResult> Enterprises()
    {
        var ws = _workspace.Resolve(UserContext.UserCode);
        if (!ws.Success || ws.Data == null)
            return Ok(ApiResponse<object>.Fail(ws.Error ?? "无法定位当前工作区"));

        var list = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.OrgCode == ws.Data.Code && x.IsDeleted == false && x.IsValid == 1)
            .OrderBy(x => x.EnterpriseNo)
            .Select(x => new { value = x.Code, label = x.Name, no = x.EnterpriseNo })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(list.Select(x => new { x.value, x.label, x.no }).ToList()));
    }

    // ════════════════════════════════════════════════════════════════════
    // 一b、★ 企业树（企业 → 标准 → 阶段）—— 左树的数据源
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>企业树</b>：企业 → 标准 → 阶段。供专家端「企业全局参数定义」左树使用 ——
    /// 选中阶段后，右区按 <c>(EnterpriseCode, StandardCode, StageCode)</c> 拉 <c>merge-list</c>。
    ///
    /// <para><b>为什么不复用机构的 <c>organization-tree</c></b>：那棵树的根是
    /// 「体系认证机构」，本页要按<b>企业</b>组织 —— 两者维度不同。强行把机构树改造成企业树，
    /// 会让「标准目录 / 文档提取规则」等页面失去原有语义；后端各自出树、前端各自转换，
    /// 比在一棵树里塞两套语义更可控。</para>
    ///
    /// <para><b>数据源</b>：<c>cert_enterprise</c>（本工作区）+ <c>cert_enterprise_stage</c>（三元组）。
    /// ⚠️ 三元组无数据时，树里只有企业节点、没有子节点 —— 前端须给「去『阶段标准关联』配置」
    /// 的兜底提示（本端点用 <c>Hint</c> 字段下发），而不是显示一棵点不开的空白树。</para>
    ///
    /// <para><b>返回的是业务字段，不是内核 <c>TreeNode</c></b>：<c>NodeType</c> / <c>Children</c>
    /// 等由前端转成 <c>YzhTree</c> 的形状（补 <c>IsLeaf</c> / <c>Extra.Icon</c>）——
    /// 后端不耦合前端组件契约。</para>
    /// </summary>
    [HttpPost("enterprise-tree")]
    public async Task<IActionResult> EnterpriseTree()
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        var workspaceCode = scope.Data.WorkspaceCode;

        // ── 1. 本工作区企业（树根）──
        var enterprises = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.OrgCode == workspaceCode && x.IsDeleted == false && x.IsValid == 1)
            .OrderBy(x => x.EnterpriseNo)
            .ToListAsync();

        if (enterprises.Count == 0)
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                Nodes = new List<EnterpriseTreeNode>(),
                EnterpriseCount = 0,
                LinkCount = 0,
                Hint = "当前工作区还没有企业，请先到「企业管理」建档",
            }));
        }

        var entCodes = enterprises.Select(x => x.Code).ToList();

        // ── 2. 关联三元组 ──
        var links = await _db.Client.Queryable<CertEnterpriseStage>()
            .Where(x => entCodes.Contains(x.EnterpriseCode) && x.IsDeleted == false && x.IsValid == 1)
            .ToListAsync();

        // ── 3. 标准 / 阶段名称：只查真正出现的 Code（避免全表扫）──
        var stdCodes = links.Select(x => x.StandardCode)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var stageCodes = links.Select(x => x.StageCode)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

        var stdMap = stdCodes.Count == 0
            ? new Dictionary<string, ISOStandard>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<ISOStandard>()
                .Where(x => stdCodes.Contains(x.Code)).ToListAsync())
              .ToDictionary(x => x.Code, x => x, StringComparer.Ordinal);

        var stageMap = stageCodes.Count == 0
            ? new Dictionary<string, CertStage>(StringComparer.Ordinal)
            : (await _db.Client.Queryable<CertStage>()
                .Where(x => stageCodes.Contains(x.Code)).ToListAsync())
              .ToDictionary(x => x.Code, x => x, StringComparer.Ordinal);

        // ── 4. 组装树：企业 → 标准 → 阶段（三元组按标准分组降为两级）──
        var nodes = new List<EnterpriseTreeNode>();
        foreach (var ent in enterprises)
        {
            var entNode = new EnterpriseTreeNode
            {
                Code = $"ent:{ent.Code}",
                Name = ent.Name,
                NodeType = "enterprise",
                EnterpriseCode = ent.Code,
                Subtitle = ent.EnterpriseNo ?? string.Empty,
            };

            var entLinks = links.Where(l => l.EnterpriseCode == ent.Code).ToList();
            foreach (var g in entLinks.GroupBy(l => l.StandardCode))
            {
                stdMap.TryGetValue(g.Key, out var std);
                var stdNode = new EnterpriseTreeNode
                {
                    Code = $"std:{ent.Code}|{g.Key}",
                    // 与机构树同款命名（`{编号} - {名称}`），两棵树的阅读习惯保持一致
                    Name = std == null ? g.Key : $"{std.StandardCode} - {std.StandardName}",
                    NodeType = "standard",
                    EnterpriseCode = ent.Code,
                    StandardCode = g.Key,
                    Subtitle = std?.StandardName ?? string.Empty,
                };

                foreach (var l in g.OrderBy(x => x.StageCode, StringComparer.Ordinal))
                {
                    stageMap.TryGetValue(l.StageCode, out var stage);
                    stdNode.Children.Add(new EnterpriseTreeNode
                    {
                        Code = $"stage:{ent.Code}|{l.StandardCode}|{l.StageCode}",
                        Name = stage?.StageName ?? l.StageCode,
                        NodeType = "stage",
                        EnterpriseCode = ent.Code,
                        StandardCode = l.StandardCode,
                        StageCode = l.StageCode,
                        Subtitle = stage?.StageCode ?? string.Empty,
                    });
                }

                stdNode.StageCount = stdNode.Children.Count;
                entNode.Children.Add(stdNode);
            }

            entNode.StageCount = entNode.Children.Sum(x => x.StageCount);
            nodes.Add(entNode);
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            Nodes = nodes,
            EnterpriseCount = enterprises.Count,
            LinkCount = links.Count,
            Hint = links.Count == 0
                ? "企业还没有关联标准 / 阶段，请先到「阶段标准关联」配置"
                : string.Empty,
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、★ 合并列表 —— 本模块的核心端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>合并待完善清单</b>：后台定义的全局参数 × 企业已有基本信息 → 一张可完善的列表。
    ///
    /// <para>返回按 <c>GroupName</c> 分组的条目；每条都带
    /// <c>paramValue</c>（当前值）、<c>valueSource</c>（值来源）、<c>sourceRef</c>（来源说明）、
    /// <c>editable</c>（企业能否改）。前端只需按 <c>editable</c> 决定输入框只读与否。</para>
    /// </summary>
    [HttpPost("merge-list")]
    public async Task<IActionResult> MergeList([FromBody] MergeListRequest req)
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
            return Ok(ApiResponse<object>.Fail("请先选择企业"));

        var ent = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.Code == req.EnterpriseCode && x.IsDeleted == false)
            .FirstAsync();

        if (ent == null)
            return Ok(ApiResponse<object>.Fail("企业不存在或已删除"));

        // ★ 越权防护：企业必须属于当前工作区（企业归属 = 工作区 Code）
        if (!string.Equals(ent.OrgCode, scope.Data.WorkspaceCode, StringComparison.Ordinal))
            return Ok(ApiResponse<object>.Fail("无权查看其他工作区的企业参数"));

        // ★ 参数定义归属 = 体系认证机构 Code（⛔ 不是工作区 Code —— 见 WorkspaceContextService 的对照表）
        var orgCode = scope.Data.CertBodyCode;
        var stdCode = req.StandardCode ?? string.Empty;
        var stageCode = req.StageCode ?? string.Empty;

        // ── 1. 取定义（含通配），按 ParamCode 去重取「更具体」的那条 ──
        var defs = await _db.Client.Queryable<FillParamDef>()
            .Where(d => d.OrgCode == orgCode && d.IsDeleted == false && d.IsValid == 1
                        && (d.StandardCode == "" || d.StandardCode == stdCode)
                        && (d.StageCode == "" || d.StageCode == stageCode))
            .OrderBy(d => d.SortOrder)
            .ToListAsync();

        var picked = ParamValueResolver.PickMostSpecific(defs);

        // ── 2. 取已落库的企业填写值 ──
        var paramCodes = picked.Select(d => d.ParamCode).ToList();
        var saved = paramCodes.Count == 0
            ? new List<FillParamValue>()
            : await _db.Client.Queryable<FillParamValue>()
                .Where(v => v.EnterpriseCode == ent.Code && v.IsDeleted == false
                            && paramCodes.Contains(v.ParamCode))
                .ToListAsync();

        // 值行的 (StandardCode, StageCode) 镜像定义的；用「定义键 → 值」精确匹配，
        // 避免「同一 ParamCode 在不同标准下各有值」时串行
        var valueMap = saved
            .GroupBy(v => ParamValueResolver.ValueKey(v.StandardCode, v.StageCode, v.ParamCode))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var entInfo = ToEnterpriseInfo(ent);

        // ── 3. 逐条合并（取值决策统一走 ParamValueResolver，⛔ 不要在此另写一份）──
        var items = new List<MergedItem>();
        foreach (var def in picked)
        {
            valueMap.TryGetValue(
                ParamValueResolver.ValueKey(def.StandardCode, def.StageCode, def.ParamCode), out var row);

            var d = ParamValueResolver.Resolve(def, entInfo, row?.ParamValue, row?.ValueSource,
                row?.IsManualEdited ?? false);

            items.Add(new MergedItem
            {
                ParamCode = def.ParamCode,
                ParamName = def.ParamName,
                GroupName = string.IsNullOrWhiteSpace(def.GroupName) ? "其他" : def.GroupName!,
                ValueType = def.ValueType,
                EnumOptions = def.EnumOptions,
                MaintainMode = def.MaintainMode,
                SourceKind = def.SourceKind,
                SourceExpr = def.SourceExpr,
                IsRequired = def.IsRequired,
                IsBuiltin = def.IsBuiltin,
                SortOrder = def.SortOrder,
                Description = def.Description,
                Placeholder = def.Placeholder,

                ParamValue = d.Value,
                ValueSource = d.ValueSource,
                SourceRef = d.SourceRef,
                IsManualEdited = d.IsManualEdited,
                Editable = d.Editable,
                IsFilled = !string.IsNullOrWhiteSpace(d.Value),
            });
        }

        // ── 4. 分组 + 完成度 ──
        var groups = items
            .GroupBy(i => i.GroupName)
            .Select(g => new
            {
                GroupName = g.Key,
                Items = g.OrderBy(i => i.SortOrder).ThenBy(i => i.ParamCode).ToList(),
            })
            .ToList();

        var total = items.Count;
        var filled = items.Count(i => i.IsFilled);
        var requiredItems = items.Where(i => i.IsRequired).ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            // ★ 整个响应一律 PascalCase（含外层与 Enterprise 匿名对象）。
            //   ⛔ 本端点曾用 camelCase：前端按 `enterprise.Name` 读，恒得 undefined
            //      ⇒「企业名永远显示为空且不报错」（铁律③的典型症状）；
            //      且与同响应里的 MergedItem（PascalCase）风格不一致，更难排查。
            OrgCode = orgCode,
            WorkspaceCode = scope.Data.WorkspaceCode,
            StandardCode = stdCode,
            StageCode = stageCode,
            Enterprise = new
            {
                Code = ent.Code,
                Name = ent.Name,
                ShortName = ent.ShortName,
                CreditCode = ent.CreditCode,
                LegalPerson = ent.LegalPerson,
                Address = ent.Address,
                IndustryType = ent.IndustryType,
                EmployeeCount = ent.EmployeeCount,
                CertScope = ent.CertScope,
                ContactName = ent.ContactName,
                ContactPhone = ent.ContactPhone,
                ContactEmail = ent.ContactEmail,
                EnterpriseNo = ent.EnterpriseNo,
                Province = ent.Province,
                City = ent.City,
            },
            Groups = groups,
            Progress = new
            {
                Total = total,
                Filled = filled,
                Empty = total - filled,
                // ★ 完成度是 **0~1 的比值**（不是百分数），前端负责 ×100。
                // ⛔ Total=0 记 0 而不是 1：与前端 liveCompletion 口径一致，
                //    且「没有参数定义」应由界面显示空态，不该显示「100% 已完善」。
                Completion = total == 0 ? 0.0 : Math.Round((double)filled / total, 4),
                Required = requiredItems.Count,
                RequiredFilled = requiredItems.Count(i => i.IsFilled),
            },
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、保存（只落人工项）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 批量保存企业完善结果。
    /// <para><b>只持久化 <c>manual</c> / <c>both</c> 两类</b>；<c>auto</c> 项被忽略并在
    /// <c>ignoredAuto</c> 中回报（前端可据此提示「该项由企业档案自动带出，请到企业管理修改」）。</para>
    /// <para>新增 vs 更新一律按 <c>Code</c> 分流（双关键字准则 A）；撞唯一键时走「复活」而非 INSERT。</para>
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveRequest req)
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
            return Ok(ApiResponse<object>.Fail("请先选择企业"));

        if (req.Items == null || req.Items.Count == 0)
            return Ok(ApiResponse<object>.Fail("没有需要保存的内容"));

        var ent = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.Code == req.EnterpriseCode && x.IsDeleted == false)
            .FirstAsync();

        if (ent == null)
            return Ok(ApiResponse<object>.Fail("企业不存在或已删除"));

        if (!string.Equals(ent.OrgCode, scope.Data.WorkspaceCode, StringComparison.Ordinal))
            return Ok(ApiResponse<object>.Fail("无权修改其他工作区的企业参数"));

        // ★ 两个作用域 Code 分开命名，避免再次混淆（见 WorkspaceContextService 对照表）：
        //   certBodyCode  → 查参数定义（配置归属 = 体系认证机构）
        //   workspaceCode → 写参数值行 OrgCode（数据归属 = 工作区）
        var certBodyCode = scope.Data.CertBodyCode;
        var workspaceCode = scope.Data.WorkspaceCode;
        var stdCode = req.StandardCode ?? string.Empty;
        var stageCode = req.StageCode ?? string.Empty;

        // 定义全集（校验 paramCode 合法性 —— 不认识的一律报错，不静默丢弃）
        var defs = await _db.Client.Queryable<FillParamDef>()
            .Where(d => d.OrgCode == certBodyCode && d.IsDeleted == false && d.IsValid == 1
                        && (d.StandardCode == "" || d.StandardCode == stdCode)
                        && (d.StageCode == "" || d.StageCode == stageCode))
            .ToListAsync();

        var defMap = defs
            .GroupBy(d => d.ParamCode)
            .ToDictionary(g => g.Key,
                g => g.OrderByDescending(ParamValueResolver.Specificity).ThenBy(d => d.Id).First(),
                StringComparer.Ordinal);

        var unknown = req.Items
            .Where(i => !defMap.ContainsKey(i.ParamCode))
            .Select(i => i.ParamCode)
            .ToList();
        if (unknown.Count > 0)
            return Ok(ApiResponse<object>.Fail(
                $"以下参数编码未在当前「机构 × 标准 × 阶段」下定义：{string.Join("、", unknown)}"));

        var now = DateTime.Now;
        var userCode = UserContext.UserCode;
        var savedCount = 0;
        var ignoredAuto = new List<string>();

        using var tx = _db.BeginTransaction();
        try
        {
            foreach (var item in req.Items)
            {
                var def = defMap[item.ParamCode];

                // ★ auto 项不接受覆盖：语义是「永远等于企业档案」
                if (string.Equals(def.MaintainMode, ParamValueResolver.ModeAuto, StringComparison.Ordinal))
                {
                    ignoredAuto.Add(def.ParamName);
                    continue;
                }

                // 值行的 (StandardCode, StageCode) 镜像定义，与唯一键 uk_ent_std_stage_param 对齐
                var rowStd = def.StandardCode;
                var rowStage = def.StageCode;

                // ★ 唯一键不含 IsDeleted ⇒ 查重必须「含已删」，命中已删行则复活
                var row = await _db.GetOneIgnoreValidAsync<FillParamValue>(v =>
                    v.EnterpriseCode == ent.Code
                    && v.StandardCode == rowStd
                    && v.StageCode == rowStage
                    && v.ParamCode == def.ParamCode);

                var value = item.ParamValue ?? string.Empty;
                var isAi = string.Equals(item.ValueSource, "ai", StringComparison.Ordinal);

                if (row.Success && row.Data != null)
                {
                    var existing = row.Data;
                    existing.ParamValue = value;
                    existing.ValueSource = isAi ? "ai" : "manual";
                    existing.IsManualEdited = true;
                    existing.IsFilled = !string.IsNullOrWhiteSpace(value);
                    existing.IsDeleted = false;
                    existing.DeleteBy = null;
                    existing.DeleteTime = null;
                    existing.UpdateTime = now;
                    existing.UpdateBy = userCode;
                    await _db.UpdateAsync(existing);
                }
                else
                {
                    var entity = new FillParamValue
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        // ★ 值行 OrgCode = 工作区 Code（数据归属），⛔ 不是认证机构 Code
                        OrgCode = workspaceCode,
                        EnterpriseCode = ent.Code,
                        EnterpriseName = ent.Name,
                        StandardCode = rowStd,
                        StageCode = rowStage,
                        ParamCode = def.ParamCode,
                        ParamName = def.ParamName,
                        GroupName = def.GroupName,
                        ValueType = def.ValueType,
                        EnumOptions = def.EnumOptions,
                        ParamValue = value,
                        ValueSource = isAi ? "ai" : "manual",
                        SourceRef = $"全局参数 · {def.ParamName}",
                        IsManualEdited = true,
                        MaintainMode = def.MaintainMode,
                        IsRequired = def.IsRequired,
                        IsFilled = !string.IsNullOrWhiteSpace(value),
                        SortOrder = def.SortOrder,
                        IsValid = 1,
                        IsDeleted = false,
                        CreateTime = now,
                        CreateBy = userCode,
                    };
                    await _db.InsertAsync(entity);
                }

                savedCount++;
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Ok(ApiResponse<object>.Fail($"保存失败：{ex.Message}"));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            savedCount,
            ignoredAuto,
            message = ignoredAuto.Count == 0
                ? $"已保存 {savedCount} 项"
                : $"已保存 {savedCount} 项；{ignoredAuto.Count} 项由企业档案自动带出，未保存（如需修改请到「企业管理」）",
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、AI 生成（产出提示词，不直连模型）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>AI 生成提示词</b>：为 <c>SourceKind='ai'</c> 的参数产出一段可直接投喂模型的提示词。
    ///
    /// <para><b>为什么本期只产提示词、不直连模型</b>：</para>
    /// <list type="number">
    ///   <item><b>可离线</b>：文档填充引擎必须能在无网、无计费的环境下重跑 167 份文档；
    ///         模型调用放进去会让批量填充变成「跑一次花一次钱、结果不可复现」；</item>
    ///   <item><b>可覆盖</b>：生成结果落 <c>cert_fill_param_value</c> 后企业可人工改，
    ///         这是「AI 辅助、人负责」的前提；</item>
    ///   <item><b>可替换</b>：接真模型 = 在本端点后追加一次调用，引擎与前端都不动。</item>
    /// </list>
    /// <para>产出物即「能力已就位」的证据：提示词里已经拼好了企业全部相关属性，
    /// 机构/专家看到的是<b>真实可用的生成请求</b>，而不是一句「支持 AI」。</para>
    /// </summary>
    [HttpPost("ai-prompt")]
    public async Task<IActionResult> AiPrompt([FromBody] AiPromptRequest req)
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
            return Ok(ApiResponse<object>.Fail("请先选择企业"));

        var ent = await _db.Client.Queryable<Enterprise>()
            .Where(x => x.Code == req.EnterpriseCode && x.IsDeleted == false)
            .FirstAsync();

        if (ent == null)
            return Ok(ApiResponse<object>.Fail("企业不存在或已删除"));
        if (!string.Equals(ent.OrgCode, scope.Data.WorkspaceCode, StringComparison.Ordinal))
            return Ok(ApiResponse<object>.Fail("无权访问其他工作区的企业参数"));

        // 参数定义归属 = 体系认证机构 Code
        var def = await _db.Client.Queryable<FillParamDef>()
            .Where(d => d.OrgCode == scope.Data.CertBodyCode && d.IsDeleted == false && d.IsValid == 1
                        && d.ParamCode == req.ParamCode)
            .OrderBy(d => d.SortOrder)
            .FirstAsync();

        if (def == null)
            return Ok(ApiResponse<object>.Fail($"未找到参数「{req.ParamCode}」的定义"));

        if (!string.Equals(def.SourceKind, "ai", StringComparison.Ordinal))
            return Ok(ApiResponse<object>.Fail(
                $"参数「{def.ParamName}」的来源类别是「{def.SourceKind}」，不需要 AI 生成"));

        // 拼装上下文：把企业全部已知属性给模型，避免它编造
        var info = ToEnterpriseInfo(ent);
        var ctxLines = ReplaceResolver.EnterpriseAttrLabels
            .Select(kv => (Attr: kv.Key, Label: kv.Value, Value: info.Get(kv.Key)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"- {x.Label}：{x.Value}")
            .ToList();

        var prompt =
            $"你是 ISO 体系认证文件编写助手。请为下列企业撰写【{def.ParamName}】。\n\n" +
            $"【企业已知信息】\n{string.Join("\n", ctxLines)}\n\n" +
            $"【写作要求】\n" +
            $"- {(string.IsNullOrWhiteSpace(def.Description) ? "符合 ISO 9001 体系文件的行文习惯" : def.Description)}\n" +
            $"- 只依据上方已知信息撰写，不得编造未提供的资质、数据或客户名称\n" +
            $"- 输出 150~300 字，直接给出正文，不要标题、不要解释\n" +
            $"- 若已知信息不足以支撑，请只输出「信息不足：<缺什么>」，不要凑字数";

        return Ok(ApiResponse<object>.Ok(new
        {
            paramCode = def.ParamCode,
            paramName = def.ParamName,
            valueType = def.ValueType,
            prompt,
            contextCount = ctxLines.Count,
            note = "本端点只产出提示词。生成结果请通过 save 端点以 valueSource='ai' 回填，之后文档填充即可取用。",
        }));
    }

    // ════════════════════════════════════════════════════════════════════
    // 私有工具
    // ════════════════════════════════════════════════════════════════════

    // ⛔ 此处刻意**不**再放 Specificity / ValueKey / ResolveAuto ——
    //    它们已上提为 ParamValueResolver 的公开成员。取值口径只能有一份：
    //    「企业端显示的值」与「文档里填的值」必须永远一致，各写一份必然漂移且都不报错。

    private static EnterpriseInfo ToEnterpriseInfo(Enterprise e) => new()
    {
        Code = e.Code,
        Name = e.Name,
        ShortName = e.ShortName,
        CreditCode = e.CreditCode,
        LegalPerson = e.LegalPerson,
        Province = e.Province,
        City = e.City,
        Address = e.Address,
        IndustryType = e.IndustryType,
        EmployeeCount = e.EmployeeCount,
        CertScope = e.CertScope,
        ContactName = e.ContactName,
        ContactPhone = e.ContactPhone,
        ContactEmail = e.ContactEmail,
        EnterpriseNo = e.EnterpriseNo,
        ArchiveDate = e.ArchiveDate,
    };

    // ════════════════════════════════════════════════════════════════════
    // 请求体
    // ════════════════════════════════════════════════════════════════════

    public sealed class MergeListRequest
    {
        public string EnterpriseCode { get; set; } = string.Empty;
        public string? StandardCode { get; set; }
        public string? StageCode { get; set; }
    }

    public sealed class SaveRequest
    {
        public string EnterpriseCode { get; set; } = string.Empty;
        public string? StandardCode { get; set; }
        public string? StageCode { get; set; }
        public List<SaveItem> Items { get; set; } = new();
    }

    public sealed class SaveItem
    {
        public string ParamCode { get; set; } = string.Empty;
        public string? ParamValue { get; set; }

        /// <summary>值来源：manual（默认）| ai</summary>
        public string? ValueSource { get; set; }
    }

    public sealed class AiPromptRequest
    {
        public string EnterpriseCode { get; set; } = string.Empty;
        public string ParamCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// 合并后的条目。
    /// <para>⚠️ 序列化为 <b>PascalCase</b>（<c>Program.cs</c> 明确 <c>PropertyNamingPolicy = null</c>），
    /// 前端按 <c>item.ParamCode</c> 读，⛔ 不要写成 <c>item.paramCode</c>（会渲染成空且不报错）。</para>
    /// </summary>
    public sealed class MergedItem
    {
        public string ParamCode { get; set; } = string.Empty;
        public string ParamName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string ValueType { get; set; } = "text";
        public string? EnumOptions { get; set; }
        public string MaintainMode { get; set; } = ParamValueResolver.ModeAuto;
        public string SourceKind { get; set; } = "global";
        public string? SourceExpr { get; set; }
        public bool IsRequired { get; set; }
        public bool IsBuiltin { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public string? Placeholder { get; set; }

        public string? ParamValue { get; set; }

        /// <summary>auto | manual | ai | default | empty</summary>
        public string ValueSource { get; set; } = "empty";

        public string SourceRef { get; set; } = string.Empty;
        public bool IsManualEdited { get; set; }
        public bool IsFilled { get; set; }
        public bool Editable { get; set; }
    }

    /// <summary>
    /// 企业树节点（企业 → 标准 → 阶段）—— <c>enterprise-tree</c> 的载荷。
    /// <para>⛔ 不是内核 <c>TreeNode</c>：这里只出业务字段，前端负责补
    /// <c>IsLeaf</c> / <c>Extra.Icon</c> 等渲染所需信息 —— 后端不耦合前端组件契约。</para>
    /// </summary>
    public sealed class EnterpriseTreeNode
    {
        /// <summary>节点唯一键（el-tree node-key）：<c>ent:{企业}</c> / <c>std:{企业}|{标准}</c> / <c>stage:{企业}|{标准}|{阶段}</c></summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>显示名（企业名 / <c>{标准编号} - {标准名}</c> / 阶段名）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>enterprise | standard | stage</summary>
        public string NodeType { get; set; } = "enterprise";

        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>标准 Code（GUID = <c>cert_iso_standard.Code</c>）；企业节点为空</summary>
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（GUID = <c>cert_cert_stage.Code</c>）；企业 / 标准节点为空</summary>
        public string StageCode { get; set; } = string.Empty;

        /// <summary>副标题（企业编号 / 标准名 / 阶段业务码），仅作展示</summary>
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>后代阶段数（企业 / 标准节点上有值）</summary>
        public int StageCount { get; set; }

        public List<EnterpriseTreeNode> Children { get; set; } = new();
    }
}
