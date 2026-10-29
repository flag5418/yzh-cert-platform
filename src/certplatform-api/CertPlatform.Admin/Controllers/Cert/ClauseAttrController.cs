using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Services.Cert;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Entities.Cert;

namespace CertPlatform.Admin.Controllers.Cert;

/// <summary>
/// 条款属性配置控制器（L1 · 后台管理）
///
/// <para><b>业务定位</b>（<c>docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md</c> §四）：
/// 一条 = 某条款上的某个属性。<b>没配 = 没行</b>（稀疏），⛔ 不需要"适用条款"列。</para>
///
/// <para><b>★ <c>ValueSource</c> 只在这里定、只两档</b>：
/// <c>fixed</c>（条款已定，存值本身）/ <c>reference</c>（条款只给期望，存期望）。
/// 同一属性在不同条款下来源不同（`Artifacts` 在 7.1.5.2 = reference、在 7.3 = fixed 且值为空），
/// ⇒ ⛔ 绝不能把它放回属性池。</para>
///
/// <para><b>为什么不是 TreeTableControllerBase</b>：左树数据源是「标准 → 条款」（<c>cert_iso_clause</c>），
/// 不是本实体的树 —— 本实体没有父子关系。故保持 <c>YzhControllerBase</c>，
/// 树走自定义端点 <see cref="GetTreeRoot"/>，右表走基类 <c>/filter</c>
/// （前端注入 <c>ClauseCode eq node.Code</c> 单条件）。</para>
///
/// <para><b>路由前缀</b>：<c>/api/Admin/Cert/ClauseAttr</c>
/// <b>数据库</b>：<c>cert_clause_attr</c></para>
/// </summary>
[ApiController]
[Route("api/Admin/Cert/[controller]")]
public class ClauseAttrController : YzhControllerBase<ClauseAttr>
{
    private readonly IDbOrm _db;

    /// <summary>核心资源检索唯一入口（铁律 L5 / 守卫 A-R6）—— 标准与条款只走它，⛔ 不裸查</summary>
    private readonly ICoreResourceQuery _core;

    /// <summary>AI 预生成（P4）</summary>
    private readonly ClauseAttrGenerateService _generator;

    /// <summary>条款属性预生成提示词（存取 + 属性池注入段渲染）</summary>
    private readonly ClauseAttrPromptService _prompt;

    /// <summary>允许的值来源 —— <b>只 2 档</b>（24 号 §五）。⛔ 没有 runtime</summary>
    private static readonly HashSet<string> AllowedValueSources =
        new(StringComparer.Ordinal) { "fixed", "reference" };

    public ClauseAttrController(
        EntityService<ClauseAttr> entityService,
        IUserContext userContext,
        IDbOrm db,
        ICoreResourceQuery core,
        ClauseAttrGenerateService generator,
        ClauseAttrPromptService prompt)
        : base(entityService, userContext)
    {
        _db = db;
        _core = core;
        _generator = generator;
        _prompt = prompt;
    }

    /// <summary>开发期强制暴露缺配置问题</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>加载 EntityConfig（<c>Assets/EntityConfigs/Cert/ClauseAttr.json</c>）</summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<ClauseAttr>();
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、校验钩子
    // ════════════════════════════════════════════════════════════════════

    /// <summary>新增前：四键必填 + ValueSource 白名单 + 属性/条款存在性 + 唯一（含已删）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(ClauseAttr entity)
    {
        var shape = await ValidateShapeAsync(entity);
        if (!shape.ok) return shape;

        var dup = await _db.Client.Queryable<ClauseAttr>()
            .Where(p => p.StandardCode == entity.StandardCode
                        && p.ClauseCode == entity.ClauseCode
                        && p.AttrCode == entity.AttrCode)
            .FirstAsync();

        if (dup != null)
            return (false, dup.IsDeleted
                ? $"该条款已配置过属性【{entity.AttrCode}】（记录已删除但仍在库中），请先恢复该行再改值"
                : $"该条款已配置过属性【{entity.AttrCode}】，请直接编辑该行");

        if (entity.Sort == 0)
        {
            var maxSort = await _db.Client.Queryable<ClauseAttr>()
                .Where(p => p.StandardCode == entity.StandardCode
                            && p.ClauseCode == entity.ClauseCode
                            && !p.IsDeleted)
                .MaxAsync(p => (int?)p.Sort) ?? 0;
            entity.Sort = maxSort + 10;
        }

        return (true, null);
    }

    /// <summary>修改前：业务键（标准/条款/属性）不可改 + 同款校验</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(ClauseAttr entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "更新失败：缺少业务键 Code");

        var current = await Entity.GetOne(p => p.Code == entity.Code);
        if (current.Success && current.Data != null)
        {
            // 三键是唯一索引成员，改了等于"换一条记录"，会让已配数据静默变成另一条款的属性
            if (!string.Equals(current.Data.StandardCode, entity.StandardCode, StringComparison.Ordinal)
                || !string.Equals(current.Data.ClauseCode, entity.ClauseCode, StringComparison.Ordinal)
                || !string.Equals(current.Data.AttrCode, entity.AttrCode, StringComparison.Ordinal))
                return (false, "标准 / 条款 / 属性 三键不可修改；要换请删除后重新新增");
        }

        return await ValidateShapeAsync(entity);
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、自定义端点
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 左树：<b>标准 → 章 → 节 → 叶子条款</b>（完整层次，最多 4 层）。
    ///
    /// <para>返回完整树（非叶 + 叶子）：用户可逐层展开定位条款，不再 208 条叶子铺成平面。
    /// 非叶子节点（章/节） NodeType = "chapter" / "section"，不能配属性（点了右表空 +
    /// 给出引导文案），仅叶子节点（NodeType = "clause"）能配属性。</para>
    /// </summary>
    [HttpPost("tree/root")]
    public async Task<IActionResult> GetTreeRoot()
    {
        // ★ 取数收口 CoreResourceQuery（铁律 L5 / 守卫 A-R6）：标准与条款都是核心资源，⛔ 不裸查
        var standards = (await _core.GetValidStandardsAsync())
            .OrderBy(s => s.Sort)
            .ThenBy(s => s.StandardCode, StringComparer.Ordinal)
            .ToList();

        var nodes = new List<TreeItemDto>();
        foreach (var s in standards)
        {
            // 每个标准一次（标准数个位级，4 个）—— 避免跨标准一次性捞全表
            var all = await _core.GetClausesAsync(s.Code);
            var leafCount = all.Count(c => !all.Any(x => x.ParentCode == c.Code));

            nodes.Add(new TreeItemDto
            {
                Code = s.StandardCode,
                Name = $"{s.StandardName}（{leafCount}）",
                ParentCode = null,
                NodeType = "standard",
                IsLeaf = false,
                Level = 1,
                Sort = s.Sort,
                Children = BuildClauseTree(all, s.StandardCode),
                Extra = new Dictionary<string, object> { ["StandardCode"] = s.StandardCode }
            });
        }

        return Ok(ApiResponse<List<TreeItemDto>>.Ok(nodes));
    }

    /// <summary>
    /// 递归构建条款树（标准下一级的完整层次）。
    /// <para>ParentCode 空 或 不在本集合 Code 内 ⇒ 当根级（章）；其余按父子递归。</para>
    /// </summary>
    private static List<TreeItemDto> BuildClauseTree(List<ISOClause> all, string standardCode)
    {
        var byCode = all.ToDictionary(c => c.Code, StringComparer.Ordinal);
        // 找根级条款：ParentCode 为空 或 指向不在集合内的节点（即标准直接子节点 = 章）
        var roots = all
            .Where(c => string.IsNullOrEmpty(c.ParentCode) || !byCode.ContainsKey(c.ParentCode))
            .OrderBy(c => c.Sort)
            .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
            .ToList();

        return roots.Select(r => BuildNode(r, all, standardCode, 2)).ToList();
    }

    private static TreeItemDto BuildNode(ISOClause clause, List<ISOClause> all, string standardCode, int level)
    {
        var children = all
            .Where(c => string.Equals(c.ParentCode, clause.Code, StringComparison.Ordinal))
            .OrderBy(c => c.Sort)
            .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
            .Select(c => BuildNode(c, all, standardCode, level + 1))
            .ToList();

        var isLeaf = children.Count == 0;
        var nodeType = isLeaf ? "clause" : (level <= 2 ? "chapter" : "section");

        return new TreeItemDto
        {
            Code = clause.Code,
            Name = $"{clause.ClauseNumber} {clause.Title}",
            ParentCode = string.IsNullOrEmpty(clause.ParentCode) ? null : clause.ParentCode,
            NodeType = nodeType,
            IsLeaf = isLeaf,
            Level = level,
            Sort = clause.Sort,
            Children = children.Count > 0 ? children : null,
            Extra = new Dictionary<string, object>
            {
                ["ClauseNumber"] = clause.ClauseNumber,
                ["Title"] = clause.Title ?? string.Empty,
                ["StandardCode"] = standardCode,
                ["Description"] = clause.Description ?? string.Empty,
                ["IsLeaf"] = isLeaf,
            }
        };
    }

    /// <summary>
    /// 某条款已配属性数（给树节点打角标用，可选）。
    /// <para>⚠️ 只统计未删除行；已停用（<c>IsValid=0</c>）也算"配过"。</para>
    /// </summary>
    [HttpPost("tree/counts")]
    public async Task<IActionResult> GetAttrCounts()
    {
        var rows = await _db.Client.Queryable<ClauseAttr>()
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.ClauseCode)
            .Select(p => new { ClauseCode = p.ClauseCode, Cnt = SqlSugar.SqlFunc.AggregateCount(p.Id) })
            .ToListAsync();

        var map = rows.ToDictionary(r => r.ClauseCode, r => r.Cnt);
        return Ok(ApiResponse<Dictionary<string, int>>.Ok(map));
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、提示词维护（对全部标准、全部条款生效的一条全局提示词）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>读取提示词（给前端编辑抽屉）：正文 + 属性池注入段 + 元信息</summary>
    [HttpGet("prompt")]
    public async Task<IActionResult> GetPrompt()
    {
        var dto = await _prompt.GetPromptAsync();
        return Ok(ApiResponse<ClauseAttrPromptDto>.Ok(dto));
    }

    /// <summary>保存提示词（幂等 upsert）：只改 ConfigValue，唯一键 = ConfigKey</summary>
    [HttpPost("prompt")]
    public async Task<IActionResult> SavePrompt([FromBody] ClauseAttrPromptSaveRequest request)
    {
        var (ok, message) = await _prompt.SaveAsync(
            request?.Template ?? string.Empty, UserContext.UserCode);

        return ok
            ? Ok(ApiResponse.Ok(message))
            : Ok(ApiResponse.BizFail(message));
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、AI 预生成（P4）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>AI 预生成条款属性</b>：给条款号 + 标题 + 正文，让 LLM 产出该条款 8 个属性的值并落库。
    ///
    /// <para><b>⚠️ 串行</b>：逐条调用模型（并发必 429），故用 <c>MaxCount</c> 控制单次批量（默认 5，上限 50）。
    /// 前端应分批调用并把 <c>Items</c> 逐条展示出来，让用户看到"第几条失败/为什么"。</para>
    ///
    /// <para><b>⛔ 不做全量一键跑</b>：273 条 × 8 属性 ≈ 2184 次写入，中途失败难以回滚；
    /// 先小批验证提示词质量，再逐批推进。</para>
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] ClauseAttrGenerateRequest request, CancellationToken ct)
    {
        var result = await _generator.GenerateAsync(request ?? new ClauseAttrGenerateRequest(), ct);

        // ★ 信封语义（守卫 R13 口径）：
        //   · 前置校验没过（一条都没跑：Total=0）⇒ **业务失败**，前端应弹红条
        //   · 真跑过了（Total>0）⇒ 请求本身成功，逐条成败看 `data.Success` / `data.Items`
        //   ⛔ 不能一律 Ok：否则「提示词没配 / AI 参数没填」也显示成功，用户以为跑过了
        if (result.Total == 0)
            return Ok(ApiResponse<ClauseAttrGenerateResult>.BizFail(result.Message));

        return Ok(ApiResponse<ClauseAttrGenerateResult>.Ok(result, result.Message));
    }

    // ════════════════════════════════════════════════════════════════════
    // 五、私有校验
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 形状 + 引用完整性校验（新增/修改共用，⛔ 不得各写一份）：
    /// ① 四键非空 ② ValueSource 白名单 ③ 属性码必须在属性池且启用 ④ 条款必须存在且属于该标准
    /// </summary>
    private async Task<(bool ok, string? msg)> ValidateShapeAsync(ClauseAttr entity)
    {
        entity.StandardCode = (entity.StandardCode ?? string.Empty).Trim();
        entity.ClauseCode = (entity.ClauseCode ?? string.Empty).Trim();
        entity.AttrCode = (entity.AttrCode ?? string.Empty).Trim();

        if (entity.StandardCode.Length == 0) return (false, "标准不能为空");
        if (entity.ClauseCode.Length == 0) return (false, "条款不能为空");
        if (entity.AttrCode.Length == 0) return (false, "属性不能为空");

        var vs = (entity.ValueSource ?? string.Empty).Trim().ToLowerInvariant();
        if (vs.Length == 0)
            return (false, "「值来源」不能为空：必须明确是固定(fixed)还是参考(reference) —— 否则下游无法判断该不该向企业索取");
        if (!AllowedValueSources.Contains(vs))
            return (false, $"「值来源」【{entity.ValueSource}】不合法，只能是 fixed（固定）或 reference（参考）");
        entity.ValueSource = vs;

        var attr = await _db.Client.Queryable<ClauseAttrDef>()
            .Where(p => p.Code == entity.AttrCode && !p.IsDeleted && p.IsValid == 1)
            .FirstAsync();
        if (attr == null)
            return (false, $"属性【{entity.AttrCode}】不在属性池中或已停用，请先在「条款属性池」维护");

        var clause = await _core.GetClauseByCodeAsync(entity.ClauseCode);
        if (clause == null)
            return (false, "条款不存在或已删除");

        // cert_iso_clause.StandardCode 存的是 ISO 标准行的 GUID；本表存短码 ⇒ 需要一次换算
        var std = await _core.GetStandardByCodeAsync(clause.StandardCode);

        if (std == null || !string.Equals(std.StandardCode, entity.StandardCode, StringComparison.Ordinal))
            return (false, $"条款【{clause.ClauseNumber}】不属于标准【{entity.StandardCode}】");

        return (true, null);
    }
}
