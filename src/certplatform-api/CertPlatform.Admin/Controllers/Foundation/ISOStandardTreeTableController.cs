
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.Foundation;

/// <summary>
///     ISO 标准 — 条款 左树右表管理控制器（TreeTable 架构）
///
///     左树：ISO 标准（扁平结构，所有标准都是根节点，不允许增加下级）
///     右表：ISO 条款（选中标准后，分页加载该标准下的条款）
///
///     继承 TreeTableControllerBase 获得能力：
///     - 树能力：/tree/root /tree/children /tree/add /tree/update /tree/delete
///     - 单表 CRUD：/filter /add /update /delete /toggle-valid
///     - 配置：/treepconfig（返回 TreeTableConfig = TreeConfig + TableConfig）
///     - 树→表格联动：选中标准后自动注入 StandardCode 过滤
///
///     业务规则：
///     1. 标准无层级（MaxLevel=1），不显示"新增下级"按钮
///     2. 标准名称全局唯一（ExistsAsync 自动排除 IsDeleted）
///     3. 标准编号 + 版本年份 唯一
///     4. 条款编号在同一标准下唯一
///     5. 删除标准前校验：无条款关联才可删除
///
///     API 路由：
///     --- 树（标准） ---
///     POST   /api/Foundation/ISOStandardTreeTable/tree/root             获取根节点（所有标准）
///     POST   /api/Foundation/ISOStandardTreeTable/tree/children          懒加载子节点（始终空）
///     POST   /api/Foundation/ISOStandardTreeTable/tree/add               新增标准
///     POST   /api/Foundation/ISOStandardTreeTable/tree/update            修改标准
///     POST   /api/Foundation/ISOStandardTreeTable/tree/delete            删除标准
///     --- 条款 ---
///     GET    /api/Foundation/ISOStandardTreeTable/treepconfig            获取页面配置
///     POST   /api/Foundation/ISOStandardTreeTable/filter                 条款分页（自动注入标准编码过滤）
///     POST   /api/Foundation/ISOStandardTreeTable/add                   新增条款
///     POST   /api/Foundation/ISOStandardTreeTable/update                修改条款
///     POST   /api/Foundation/ISOStandardTable/delete                     删除条款
/// </summary>
[ApiController]
[Route("api/Foundation/[controller]")]
public class ISOStandardTreeTableController
    : TreeTableControllerBase<ISOStandard,
                             ISOClause>
{
    public ISOStandardTreeTableController(
        EntityService<ISOStandard> treeEntityService,
        EntityService<ISOClause> tableEntityService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        // ──── 左树配置（ISO 标准） ────
        TreeConfig.NameField = "StandardName";
        TreeConfig.CodeField = "Code";
        TreeConfig.ParentCodeField = "ParentCode";
        TreeConfig.RelateField = "StandardCode";   // 右表通过 StandardCode 关联左树
        TreeConfig.MaxLevel = 1;                   // 仅一级，不允许增加下级
        TreeConfig.AllowAddChild = false;          // 扁平无层级：不显示「新增下级」（前端按钮由本配置驱动）
        TreeConfig.AllowEdit = true;               // 允许新增/编辑/删除标准
        TreeConfig.AllowDelete = true;
        TreeConfig.NoSelectionBehavior = "empty";  // 未选中标准时右表为空

        // 树节点表单配置（弹窗新增/编辑标准时的表单字段）
        TreeFormConfigName = "Foundation/ISOStandardForm";
    }

    // ========================================================
    // 配置获取
    // ========================================================

    /// <summary>
    /// 加载右表（ISOClause）的 EntityConfig
    /// 配置文件路径：Assets/EntityConfigs/Foundation/ISOClause.json
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("Foundation/ISOClause");
    }

    /// <summary>
    /// 实体 → TreeItemDto 映射：将 ISOStandard 特有字段加入 Extra，
    /// 使前端编辑标准时能回填 StandardCode、VersionYear、Category、Description、Remark。
    /// </summary>
    protected override TreeItemDto MapToTreeItem(
        ISOStandard entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra["StandardCode"] = entity.StandardCode ?? "";
        dto.Extra["VersionYear"] = entity.VersionYear;
        dto.Extra["Category"] = entity.Category ?? "";
        dto.Extra["Description"] = entity.Description ?? "";
        dto.Extra["Remark"] = entity.Remark ?? "";
        return dto;
    }

    // ========================================================
    // 树节点（标准）生命周期钩子
    // ========================================================

    /// <summary>新增标准前校验：标准名称唯一 + 同版本下编号唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(
        ISOStandard entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");
        entity.IsValid = 1;

        if (string.IsNullOrWhiteSpace(entity.StandardName))
            return (false, "标准名称不能为空");

        var nameExists = await TreeEntity.ExistsAsync(s =>
            s.StandardName == entity.StandardName);
        if (nameExists.Data)
            return (false, $"标准名称【{entity.StandardName}】已存在");

        var exists = await TreeEntity.ExistsAsync(s =>
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    /// <summary>修改标准前校验：标准名称唯一（排除自身）+ 同版本下编号唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(
        ISOStandard entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardName))
            return (false, "标准名称不能为空");

        var nameExists = await TreeEntity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardName == entity.StandardName);
        if (nameExists.Data)
            return (false, $"标准名称【{entity.StandardName}】已存在");

        var exists = await TreeEntity.ExistsAsync(s =>
            s.Code != entity.Code &&
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    /// <summary>删除标准前校验：无条款关联才可删除</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        foreach (var code in codes)
        {
            // 检查是否有条款关联（需要通过 StandardCode 查询，而不是 Code）
            var standard = await TreeEntity.GetByCode(code);
            if (!standard.Success || standard.Data == null)
                return (false, "标准不存在");

            var clauseCount = await Entity.CountAsync(c => c.StandardCode == standard.Data.Code);
            if (clauseCount.Data > 0)
                return (false, "该标准下存在条款，请先删除条款");
        }

        return (true, null);
    }

    // ========================================================
    // 表格（条款）生命周期钩子
    // ========================================================

    /// <summary>新增条款前校验：StandardCode 必填 + 同标准下条款编号唯一 + 父条款合法</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        ISOClause entity)
    {
        if (string.IsNullOrWhiteSpace(entity.StandardCode))
            return (false, "请先选中一个标准后再新增条款");

        var exists = await Entity.ExistsAsync(c =>
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return await ValidateClauseParentAsync(entity);
    }

    /// <summary>修改条款前校验：同标准下条款编号唯一（排除自身）+ 父条款合法/防环</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        ISOClause entity)
    {
        var exists = await Entity.ExistsAsync(c =>
            c.Code != entity.Code &&
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return await ValidateClauseParentAsync(entity);
    }

    /// <summary>删除条款前校验：有子禁删（子条款不在同批删除集合中则拒绝）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDelete(string[] codes)
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

    /// <summary>
    ///     父条款校验：存在性 + 同标准 + 防环（不能挂到自身或子孙下）
    /// </summary>
    private async Task<(bool ok, string? msg)> ValidateClauseParentAsync(ISOClause entity)
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
}
