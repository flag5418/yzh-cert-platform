extern alias SharedEntities;

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
///     2. 标准编号 + 版本年份 唯一
///     3. 条款编号在同一标准下唯一
///     4. 删除标准前校验：无条款关联才可删除
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
    : TreeTableControllerBase<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard,
                             SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause>
{
    public ISOStandardTreeTableController(
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard> treeEntityService,
        EntityService<SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause> tableEntityService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        // ──── 左树配置（ISO 标准） ────
        TreeConfig.NameField = "StandardName";
        TreeConfig.CodeField = "Code";
        TreeConfig.ParentCodeField = "ParentCode";
        TreeConfig.RelateField = "StandardCode";   // 右表通过 StandardCode 关联左树
        TreeConfig.MaxLevel = 1;                   // 仅一级，不允许增加下级
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

    // ========================================================
    // 树节点（标准）生命周期钩子
    // ========================================================

    /// <summary>新增标准前校验：同版本下编号唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");

        var exists = await TreeEntity.ExistsAsync(s =>
            s.StandardCode == entity.StandardCode &&
            s.VersionYear == entity.VersionYear);

        if (exists.Data)
            return (false, $"版本 {entity.VersionYear} 下标准编号【{entity.StandardCode}】已存在");

        return (true, null);
    }

    /// <summary>修改标准前校验：同版本下编号唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOStandard entity)
    {
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

    /// <summary>新增条款前校验：同标准下条款编号唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause entity)
    {
        var exists = await Entity.ExistsAsync(c =>
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return (true, null);
    }

    /// <summary>修改条款前校验：同标准下条款编号唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SharedEntities::YZH.Entity.Admin.Platform.Cert.ISOClause entity)
    {
        var exists = await Entity.ExistsAsync(c =>
            c.Code != entity.Code &&
            c.StandardCode == entity.StandardCode &&
            c.ClauseNumber == entity.ClauseNumber);

        if (exists.Data)
            return (false, $"该标准下条款编号【{entity.ClauseNumber}】已存在");

        return (true, null);
    }
}
