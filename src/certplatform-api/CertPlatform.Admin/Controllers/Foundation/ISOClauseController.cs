
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
///     ISO 标准条款管理控制器
///     
///     功能：标准条款的增删改查（支持树形结构：标准 → 条款 → 子条款）
///     路由前缀：/api/Foundation/ISOClause
///     
///     继承 YzhControllerBase 获得能力：
///     - GET    /config              获取页面配置（EntityConfig）
///     - POST   /filter              分页查询
///     - POST   /add                 新增
///     - POST   /update              修改
///     - POST   /delete              软删除
///     - POST   /action/{methodName} 自定义行操作
///     
///     数据库：cert_iso_clause（delete_time IS NULL 过滤软删除）
///     
///     前端路由映射（certplatform-web/admin/src/router/）：
///     - path: '/iso-clause'
///     - component: '@/pages/cert/iso-clause/index.vue'
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class ISOClauseController : YzhControllerBase<ISOClause>
{
    public ISOClauseController(
        EntityService<ISOClause> entityService,
        IUserContext userContext)
        : base(entityService, userContext)
    {
    }

    // ========================================================
    // 自定义配置
    // ========================================================

    /// <summary>
    /// 加载配置（从 EntityConfigHelper 获取）
    /// 配置文件路径：Assets/EntityConfigs/ISOClause.json
    /// 如配置文件不存在，EntityConfigHelper 会自动返回默认空配置
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig<ISOClause>();
    }

    // ========================================================
    // 业务校验
    // ========================================================

    /// <summary>新增前校验：StandardCode 必填 + 同标准下条款编号唯一 + 父条款合法</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(ISOClause entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardCode))
            return (false, "所属标准编码不能为空");

        var exists = await Entity.ExistsAsync(c =>
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return await ValidateParentAsync(entity);
    }

    /// <summary>修改前校验：同标准下条款编号唯一（排除自身）+ 父条款合法/防环</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(ISOClause entity)
    {
        var exists = await Entity.ExistsAsync(c =>
            c.Code != entity.Code &&
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return await ValidateParentAsync(entity);
    }

    /// <summary>删除前校验：存在未同批删除的子条款则拒绝（有子禁删）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
    {
        return await ValidateDeleteHasChildrenAsync(codes);
    }

    /// <summary>
    ///     父条款校验：存在性 + 同标准 + 防环（不能挂到自身或子孙下）
    /// </summary>
    protected async Task<(bool ok, string? msg)> ValidateParentAsync(ISOClause entity)
    {
        if (string.IsNullOrEmpty(entity.ParentCode))
            return (true, null);

        if (entity.ParentCode == entity.Code)
            return (false, "父条款不能是自身");

        var parentResult = await Entity.GetListAsync(c => c.Code == entity.ParentCode);
        var parent = parentResult.Data?.FirstOrDefault();
        if (parent == null)
            return (false, "指定的父条款不存在");

        if (parent.StandardCode != entity.StandardCode)
            return (false, "父条款与子条款必须属于同一标准");

        // 防环：沿候选父级向上遍历，不得经过自身
        var visited = new HashSet<string>();
        var current = parent;
        while (current != null && !string.IsNullOrEmpty(current.ParentCode))
        {
            if (current.ParentCode == entity.Code)
                return (false, "不能将条款挂到自身或其子条款下");
            if (!visited.Add(current.ParentCode))
                break;
            var next = await Entity.GetListAsync(c => c.Code == current.ParentCode);
            current = next.Data?.FirstOrDefault();
        }

        return (true, null);
    }

    /// <summary>
    ///     有子禁删：若存在 ParentCode 指向待删节点、但自身不在本批删除集合中的条款，则拒绝
    /// </summary>
    protected async Task<(bool ok, string? msg)> ValidateDeleteHasChildrenAsync(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (true, null);

        var codeSet = new HashSet<string>(codes);
        var childrenResult = await Entity.GetListAsync(c => codes.Contains(c.ParentCode) && !c.IsDeleted);
        var blockers = (childrenResult.Data ?? new List<ISOClause>())
            .Where(c => !codeSet.Contains(c.Code))
            .ToList();

        if (blockers.Count > 0)
        {
            var first = blockers[0];
            return (false, $"条款【{first.ClauseNumber} {first.Title}】存在子条款，请先删除子条款");
        }

        return (true, null);
    }

    /// <summary>获取条款树（按标准筛选，返回扁平列表由前端构建树）</summary>
    /// <param name="standardCode">标准编码</param>
    /// <param name="includeDisabled">是否包含已禁用条款（默认 false，兼容 NC 配置页）</param>
    [HttpGet("getTree")]
    public async Task<IActionResult> GetTree(
        [FromQuery] string standardCode,
        [FromQuery] bool includeDisabled = false)
    {
        // includeDisabled 必须下传 EntityService：SqlSugarDbOrm.GetListAsync
        // 在 includeDisabled=false 时会强制追加 IsValid=1 过滤，仅靠 predicate 无法绕过。
        var result = await Entity.GetListAsync(
            c => c.StandardCode == standardCode,
            includeDisabled);

        if (result.Error != null)
            return BadRequest(ApiResponse.Fail(result.Error));

        var list = result.Data ?? new List<ISOClause>();
        return Ok(ApiResponse<List<ISOClause>>.Ok(list));
    }
}
