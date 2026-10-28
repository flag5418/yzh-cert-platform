using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using CertPlatform.Admin.Entities.Cert;
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

    /// <summary>允许的值来源 —— <b>只 2 档</b>（24 号 §五）。⛔ 没有 runtime</summary>
    private static readonly HashSet<string> AllowedValueSources =
        new(StringComparer.Ordinal) { "fixed", "reference" };

    public ClauseAttrController(
        EntityService<ClauseAttr> entityService,
        IUserContext userContext,
        IDbOrm db,
        ICoreResourceQuery core)
        : base(entityService, userContext)
    {
        _db = db;
        _core = core;
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
    /// 左树：<b>标准 → 叶子条款</b>（只列叶子 —— 配置粒度默认叶子，合并是例外）。
    ///
    /// <para>为什么只列叶子：非叶子条款本身不产生检查要求，给它配属性等于配了也不会被消费；
    /// 而 208 条叶子平铺在标准下，按 ClauseNumber 排序天然按章节聚集，比嵌套树更好用。</para>
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
            var parentCodes = all
                .Where(c => !string.IsNullOrEmpty(c.ParentCode))
                .Select(c => c.ParentCode!)
                .ToHashSet(StringComparer.Ordinal);

            var children = all
                .Where(c => !parentCodes.Contains(c.Code))     // 只列叶子
                .OrderBy(c => c.Sort)
                .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
                .Select(c => new TreeItemDto
                {
                    Code = c.Code,
                    Name = $"{c.ClauseNumber} {c.Title}",
                    ParentCode = s.StandardCode,
                    NodeType = "clause",
                    IsLeaf = true,
                    Level = 2,
                    Extra = new Dictionary<string, object>
                    {
                        ["ClauseNumber"] = c.ClauseNumber,
                        ["Title"] = c.Title ?? string.Empty,
                        ["StandardCode"] = s.StandardCode
                    }
                })
                .ToList();

            nodes.Add(new TreeItemDto
            {
                Code = s.StandardCode,
                Name = $"{s.StandardName}（{children.Count}）",
                ParentCode = null,
                NodeType = "standard",
                IsLeaf = false,
                Level = 1,
                Sort = s.Sort,
                Children = children,
                Extra = new Dictionary<string, object> { ["StandardCode"] = s.StandardCode }
            });
        }

        return Ok(ApiResponse<List<TreeItemDto>>.Ok(nodes));
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
    // 三、私有校验
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
