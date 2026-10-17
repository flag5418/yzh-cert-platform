using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Shared.Domain.Scope;

namespace CertPlatform.Auditor.Controllers;

/// <summary>
/// 企业全局参数值控制器（专家端 —— 「企业全局参数定义」页的数据口）
///
/// <para><b>业务定位</b>：把「后台按标准预定义的全局参数」与「企业已有基本信息」
/// <b>自动合并成一张待完善清单</b>，让企业只需填真正缺的那几项。
/// 这正是 05 册 <c>22</c> §七 去重裁定的落地形态：
/// <b>企业基本信息与全局参数不是两套数据，而是「同一份数据的两个视角」</b>。</para>
///
/// <para><b>★ 本页是「简单填写页」</b>（2026-10-07 用户裁决）：左树 = 企业 → 标准（两级），
/// 右区 = 按后台定义逐项手填；<b>不接 AI</b> —— 原 <c>ai-prompt</c> 端点与
/// 「生成提示词」按钮已删除，<c>ValueSource</c> 恒 <c>manual</c>。</para>
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
    private readonly ICoreResourceQuery _core;
    private readonly WorkspaceContextService _workspace;

    /// <summary>★ 参数「查询 + 补录」的唯一入口（`66` V2.1 §二）—— 本控制器只做「鉴权 + 参数校验 + 包信封」。</summary>
    private readonly IFillParamService _fillParam;

    public FillParamValueController(
        EntityService<FillParamValue> entityService,
        IUserContext userContext,
        IDbOrm db,
        ICoreResourceQuery core,
        WorkspaceContextService workspace,
        IFillParamService fillParam)
        : base(entityService, userContext)
    {
        _db = db;
        _core = core;
        _workspace = workspace;
        _fillParam = fillParam;
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
        var standards = (await _core.GetLeafStandardsAsync())
            .OrderBy(x => x.Sort)
            .Select(x => new { x.Code, Name = x.StandardName, No = x.StandardCode })
            .ToList();

        var stages = (await _core.GetStagesAsync())
            .OrderBy(x => x.Sort)
            .Select(x => new { x.Code, Name = x.StageName, No = x.StageCode })
            .ToList();

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

        // ★ 1.B.6d-1：取数收口 CoreResourceQuery（等价：OrgCode == 入参；框架默认 IsDeleted==0 ∧ IsValid==1）
        var list = (await _core.GetEnterprisesAsync(ws.Data.Code))
            .OrderBy(x => x.EnterpriseNo)
            .Select(x => new { value = x.Code, label = x.Name, no = x.EnterpriseNo })
            .ToList();

        return Ok(ApiResponse<object>.Ok(list.Select(x => new { x.value, x.label, x.no }).ToList()));
    }

    // ════════════════════════════════════════════════════════════════════
    // 一b、★ 企业树（企业 → 标准）—— 左树的数据源
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <b>企业树</b>：企业 → 标准（<b>两级</b>）。供专家端「企业全局参数定义」左树使用 ——
    /// 选中标准后，右区按 <c>(EnterpriseCode, StandardCode)</c> 拉 <c>merge-list</c>。
    ///
    /// <para><b>★ 为什么没有阶段层（2026-10-07 用户裁决）</b>：本页是「按后台定义的信息
    /// 逐项填写」的<b>简单填写页</b>，而后台「企业资料参数」的定义<b>只按标准组织</b>
    /// （<c>FillParamDefController</c> 强制 <c>StageCode=string.Empty</c>，不分阶段）。
    /// 树里再挂一层阶段，只会让同一份清单重复出现 N 次、还多一层「点了看什么」的困惑。
    /// 阶段维度仍由「阶段标准关联」与企业资料规范化册承载，本页不重复暴露。</para>
    ///
    /// <para><b>为什么不复用机构的 <c>organization-tree</c></b>：那棵树的根是
    /// 「体系认证机构」，本页要按<b>企业</b>组织 —— 两者维度不同。强行把机构树改造成企业树，
    /// 会让「标准目录 / 文档提取规则」等页面失去原有语义；后端各自出树、前端各自转换，
    /// 比在一棵树里塞两套语义更可控。</para>
    ///
    /// <para><b>数据源</b>：<c>cert_enterprise</c>（本工作区）+ <c>cert_enterprise_stage</c>
    /// （三元组<b>按标准去重</b>降为两级 —— 同一企业同一标准关联了多个阶段也只出一个标准节点）。
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
        // ★ 1.B.6d-1：取数收口 CoreResourceQuery（等价：OrgCode == 入参；框架默认 IsDeleted==0 ∧ IsValid==1）
        var enterprises = (await _core.GetEnterprisesAsync(workspaceCode))
            .OrderBy(x => x.EnterpriseNo)
            .ToList();

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

        // ── 3. 标准名称：只查真正出现的 Code（避免全表扫）──
        var stdCodes = links.Select(x => x.StandardCode)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

        var stdMap = stdCodes.Count == 0
            ? new Dictionary<string, ISOStandard>(StringComparer.Ordinal)
            : (await _core.GetStandardsByCodesAsync(stdCodes))
              .ToDictionary(x => x.Code, x => x, StringComparer.Ordinal);

        // ── 4. 组装树：企业 → 标准（三元组按标准去重降为两级，⛔ 不再产 stage 节点）──
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
                entNode.Children.Add(new EnterpriseTreeNode
                {
                    Code = $"std:{ent.Code}|{g.Key}",
                    // 与机构树同款命名（`{编号} - {名称}`），两棵树的阅读习惯保持一致
                    Name = std == null ? g.Key : $"{std.StandardCode} - {std.StandardName}",
                    NodeType = "standard",
                    EnterpriseCode = ent.Code,
                    StandardCode = g.Key,
                    Subtitle = std?.StandardName ?? string.Empty,
                });
            }

            nodes.Add(entNode);
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            Nodes = nodes,
            EnterpriseCount = enterprises.Count,
            LinkCount = links.Count,
            Hint = links.Count == 0
                ? "企业还没有关联标准，请先到「阶段标准关联」配置"
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
    ///
    /// <para>★ 2026-10-14 第 0 批通用 Service 抽取：实现已下沉
    /// <see cref="IFillParamService.GetMergedListAsync"/>（`66` V2.1 §2.5），
    /// 本端点只保留「工作区解析 + 入参校验 + 包信封」。</para>
    /// </summary>
    [HttpPost("merge-list")]
    public async Task<IActionResult> MergeList([FromBody] MergeListRequest req)
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
            return Ok(ApiResponse<object>.Fail("请先选择企业"));

        // ★ 值归属 = 工作区 Code（数据归属）；参数定义归属 = 平台标准级 ''（服务内部固定）
        var ps = new FillParamScope(
            WorkspaceCode: scope.Data.WorkspaceCode,
            EnterpriseCode: req.EnterpriseCode,
            StandardCode: req.StandardCode ?? string.Empty,
            StageCode: req.StageCode ?? string.Empty);

        var r = await _fillParam.GetMergedListAsync(ps);
        if (!r.Success || r.Data == null)
            return Ok(ApiResponse<object>.Fail(r.Error ?? "查询失败"));

        return Ok(ApiResponse<object>.Ok(r.Data));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、保存（只落人工项）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 批量保存企业完善结果。
    /// <para><b>只持久化 <c>manual</c> / <c>both</c> 两类</b>；<c>auto</c> 项被忽略并在
    /// <c>ignoredAuto</c> 中回报（前端可据此提示「该项由企业档案自动带出，请到企业管理修改」）。</para>
    /// <para>新增 vs 更新一律按 <c>Code</c> 分流（双关键字准则 A）；撞唯一键时走「复活」而非 INSERT。</para>
    ///
    /// <para>★ 2026-10-14 第 0 批通用 Service 抽取：实现已下沉
    /// <see cref="IFillParamService.SaveAsync"/>（`66` V2.1 §2.5）。</para>
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] SaveRequest req)
    {
        var scope = await _workspace.ResolveScopeAsync(UserContext.UserCode);
        if (!scope.Success || scope.Data == null)
            return Ok(ApiResponse<object>.Fail(scope.Error ?? "无法定位当前工作区"));

        if (req == null || string.IsNullOrWhiteSpace(req.EnterpriseCode))
            return Ok(ApiResponse<object>.Fail("请先选择企业"));

        // ⚠️ 校验顺序与改前一致：企业为空 → 无内容 → 企业不存在 → 越权。
        //    「无内容」与「企业不存在」的判定都在 Service 内，顺序未变。
        var ps = new FillParamScope(
            WorkspaceCode: scope.Data.WorkspaceCode,
            EnterpriseCode: req.EnterpriseCode,
            StandardCode: req.StandardCode ?? string.Empty,
            StageCode: req.StageCode ?? string.Empty);

        var items = (req.Items ?? new List<SaveItem>())
            .Select(i => new FillParamSaveItem
            {
                ParamCode = i.ParamCode,
                ParamValue = i.ParamValue,
                // ⛔ 刻意不传 ValueSource：本页值来源恒 manual，由服务端写死
            })
            .ToList();

        var r = await _fillParam.SaveAsync(ps, items, UserContext.UserCode);
        if (!r.Success || r.Data == null)
            return Ok(ApiResponse<object>.Fail(r.Error ?? "保存失败"));

        return Ok(ApiResponse<object>.Ok(r.Data));
    }

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

        // ⛔ 这里刻意**没有** `ValueSource`：本页值来源恒 `manual`
        //    （2026-10-07 用户裁决：信息由人填写，不由 AI 分析产出），
        //    由服务端写死，不接受客户端指定。
    }

    // ⛔ 原嵌套类 `MergedItem` 已下沉 `Services/Ent/FillParamModels.cs`（2026-10-14 第 0 批）——
    //    它现在是 `IFillParamService.GetMergedListAsync` 的出参元素，⛔ 不要在本类里再建一份同形类型。

    /// <summary>
    /// 企业树节点（<b>企业 → 标准</b> 两级）—— <c>enterprise-tree</c> 的载荷。
    /// <para>⛔ 不是内核 <c>TreeNode</c>：这里只出业务字段，前端负责补
    /// <c>IsLeaf</c> / <c>Extra.Icon</c> 等渲染所需信息 —— 后端不耦合前端组件契约。</para>
    /// <para>★ <b>没有阶段字段</b>（2026-10-07 用户裁决）：本页树只到标准一级，
    /// 后台「企业资料参数」的定义也只按标准组织（<c>StageCode</c> 恒空串）。</para>
    /// </summary>
    public sealed class EnterpriseTreeNode
    {
        /// <summary>节点唯一键（el-tree node-key）：<c>ent:{企业}</c> / <c>std:{企业}|{标准}</c></summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>显示名（企业名 / <c>{标准编号} - {标准名}</c>）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>enterprise | standard</summary>
        public string NodeType { get; set; } = "enterprise";

        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>标准 Code（GUID = <c>cert_iso_standard.Code</c>）；企业节点为空</summary>
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>副标题（企业编号 / 标准名），仅作展示</summary>
        public string Subtitle { get; set; } = string.Empty;

        public List<EnterpriseTreeNode> Children { get; set; } = new();
    }
}
