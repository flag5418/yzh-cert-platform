extern alias SharedEntities;

using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 技能管理 — 左树右表控制器（TreeTable 架构）
///
/// 左树：技能分类（扁平结构，所有分类为根节点）
/// 右表：技能（选中分类后，分页加载该分类下的技能）
///
/// 继承 TreeTableControllerBase 获得能力：
/// - 树能力：/tree/root /tree/children /tree/add /tree/update /tree/delete
/// - 单表 CRUD：/filter /add /update /delete /toggle-valid
/// - 配置：/treepconfig（返回 TreeTableConfig = TableConfig + TreeFormConfig）
/// - 树→表格联动：选中分类后自动注入 CategoryCode 过滤
///
/// API 路由：
/// --- 树（分类） ---
/// POST   /api/Workflow/SkillTreeTable/tree/root             获取根节点（所有分类）
/// POST   /api/Workflow/SkillTreeTable/tree/add              新增分类
/// POST   /api/Workflow/SkillTreeTable/tree/update            修改分类
/// POST   /api/Workflow/SkillTreeTable/tree/delete            删除分类
/// ---
/// GET    /api/Workflow/SkillTreeTable/treepconfig            获取页面配置
/// POST   /api/Workflow/SkillTreeTable/fiter                  技能分页（自动注入分类过滤）
/// POST   /api/Workflow/SkillTreeTable/add                    新增技能
/// POST   /api/Workflow/SkillTreeTable/update                 修改技能
/// POST   /api/Workflow/SkillTreeTable/delete                 删除技能
/// </summary>
[ApiController]
[Route("api/Workflow/[controller]")]
public class SkillTreeTableController
    : TreeTableControllerBase<SharedEntities::CertPlatform.Shared.Entities.Wf.WfSkillCategory,
                             SharedEntities::CertPlatform.Shared.Entities.Wf.Skill>
{
    public SkillTreeTableController(
        EntityService<SharedEntities::CertPlatform.Shared.Entities.Wf.WfSkillCategory> treeEntityService,
        EntityService<SharedEntities::CertPlatform.Shared.Entities.Wf.Skill> tableEntityService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        // ──── 左树配置（技能分类） ────
        TreeConfig.NameField = "Name";
        TreeConfig.CodeField = "Code";
        TreeConfig.ParentCodeField = "ParentCode";
        TreeConfig.RelateField = "CategoryCode";     // 右表通过 CategoryCode 关联左树
        TreeConfig.MaxLevel = 1;                     // 仅一级（扁平结构）
        TreeConfig.AllowEdit = true;                 // 允许编辑分类
        TreeConfig.AllowDelete = true;               // 允许删除分类
        TreeConfig.NoSelectionBehavior = "empty";    // 未选中分类时右表为空
        TreeConfig.EnableField = "IsValid";          // 启用/禁用字段

        // 树节点表单配置（弹窗新增/编辑分类时的表单字段）
        TreeFormConfigName = "Workflow/SkillCategoryForm";
    }

    // ========================================================
    // 配置获取
    // ========================================================

    /// <summary>
    /// 加载右表（Skill）的 EntityConfig
    /// 配置文件路径：Assets/EntityConfigs/Workflow/Skill.json
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        return EntityConfigHelper.GetConfig("Workflow/Skill");
    }

    /// <summary>
    /// 实体 → TreeItemDto 映射：将 WfSkillCategory 特有字段加入 Extra
    /// </summary>
    protected override TreeItemDto MapToTreeItem(
        SharedEntities::CertPlatform.Shared.Entities.Wf.WfSkillCategory entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra["Icon"] = entity.Icon ?? "";
        dto.Extra["Color"] = entity.Color ?? "";
        dto.Extra["SortOrder"] = entity.SortOrder;
        dto.Extra["IsValid"] = entity.IsValid;
        return dto;
    }

    // ========================================================
    // 树节点（分类）生命周期钩子
    // ========================================================

    /// <summary>新增分类前校验：编码唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(
        SharedEntities::CertPlatform.Shared.Entities.Wf.WfSkillCategory entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");
        entity.IsValid = 1;

        var exists = await TreeEntity.ExistsAsync(c => c.Code == entity.Code);
        if (exists.Data)
            return (false, $"编码【{entity.Code}】已存在");

        return (true, null);
    }

    /// <summary>修改分类前校验：编码唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(
        SharedEntities::CertPlatform.Shared.Entities.Wf.WfSkillCategory entity)
    {
        var exists = await TreeEntity.ExistsAsync(c => c.Code != entity.Code && c.Name == entity.Name);
        if (exists.Data)
            return (false, $"名称【{entity.Name}】已存在");

        return (true, null);
    }

    /// <summary>删除分类前校验：无技能关联才可删除</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        foreach (var code in codes)
        {
            var skillCount = await Entity.CountAsync(s => s.CategoryCode == code);
            if (skillCount.Data > 0)
                return (false, $"该分类下存在 {skillCount.Data} 个技能，请先移动或删除后再删除分类");
        }
        return (true, null);
    }

    // ========================================================
    // 表格（技能）生命周期钩子
    // ========================================================

    /// <summary>新增技能前校验：编码唯一</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        SharedEntities::CertPlatform.Shared.Entities.Wf.Skill entity)
    {
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString("N");

        var exists = await Entity.ExistsAsync(s => s.Code == entity.Code);
        if (exists.Data)
            return (false, $"编码【{entity.Code}】已存在");

        return (true, null);
    }

    /// <summary>修改技能前校验：编码唯一（排除自身）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        SharedEntities::CertPlatform.Shared.Entities.Wf.Skill entity)
    {
        var exists = await Entity.ExistsAsync(s => s.Code != entity.Code && s.Code == entity.Code);
        if (exists.Data)
            return (false, $"编码【{entity.Code}】已被其他记录使用");

        return (true, null);
    }
}
