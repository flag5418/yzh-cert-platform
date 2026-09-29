
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Controllers.Workflow;

/// <summary>
/// 技能管理 — 左树右表控制器（TreeTable 架构）
///
/// 左树：技能分类（扁平一级，<b>只读</b>）—— 数据源 = 字典「技能分类」(DicNo='skill_category') 的字典项
///       分类的增删改/启停一律在「字典管理」页面维护（2026-09-26 分类字典化，用户裁决方案 A）
/// 右表：技能（选中分类后分页加载，含启用/禁用行操作）
///
/// 关联：树节点 Code = 字典项 DicValue（data_access 等语义值）→ wf_skill.CategoryCode 同值关联
///       （原 wf_skill_category 表已废弃删除，GUID 关联值已迁移为 DicValue）
///
/// 继承 TreeTableControllerBase 获得能力：
/// - 树读取：/tree/root（override 过滤 skill_category 字典项） /tree/children（扁平恒空）
/// - 树写入：/tree/add|update|delete|toggle-valid <b>全部拦截</b> → 提示去字典管理维护
/// - 单表 CRUD：/filter /add /update /delete /toggle-valid
/// - 行操作：/action/disable /action/enable（RegisterRowAction，按行状态二选一按钮）
/// - 配置：/treepconfig /config
///
/// API 路由：
/// POST   /api/Workflow/SkillTreeTable/tree/root             获取根节点（skill_category 字典项）
/// POST   /api/Workflow/SkillTreeTable/filter                技能分页（自动注入分类过滤）
/// POST   /api/Workflow/SkillTreeTable/add|update|delete     技能增删改
/// POST   /api/Workflow/SkillTreeTable/toggle-valid          技能启用/禁用
/// POST   /api/Workflow/SkillTreeTable/action/disable|enable 技能禁用/启用（行按钮）
/// GET    /api/Workflow/SkillTreeTable/treepconfig           页面配置
/// </summary>
[ApiController]
[Route("api/Workflow/[controller]")]
public class SkillTreeTableController
    : TreeTableControllerBase<CertPlatform.Shared.Entities.Wf.SkillCategoryDict,
                             CertPlatform.Shared.Entities.Wf.Skill>
{
    /// <summary>技能分类字典的 DicNo（字典管理页面按此编码维护分类）</summary>
    private const string CategoryDictNo = "skill_category";

    /// <summary>字典服务（用于按 DicNo 解析字典 Code）</summary>
    private readonly EntityService<Sys_Dictionary> _dictionaryService;

    /// <summary>skill_category 字典 Code 进程内缓存（Code 插入后不可修改，缓存安全）</summary>
    private string? _categoryDictCode;

    public SkillTreeTableController(
        EntityService<CertPlatform.Shared.Entities.Wf.SkillCategoryDict> treeEntityService,
        EntityService<CertPlatform.Shared.Entities.Wf.Skill> tableEntityService,
        EntityService<Sys_Dictionary> dictionaryService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _dictionaryService = dictionaryService;

        // ──── 左树配置（技能分类，只读） ────
        TreeConfig.NameField = "DicName";             // 树显示名 = 字典项 DicName
        TreeConfig.CodeField = "DicValue";            // 树 Code = 业务编码 DicValue（关联 wf_skill.CategoryCode，非字典项 GUID）
        TreeConfig.ParentCodeField = "ParentCode";    // 扁平结构恒 null
        TreeConfig.RelateField = "CategoryCode";      // 右表通过 CategoryCode 关联左树
        TreeConfig.MaxLevel = 1;                      // 仅一级（扁平结构）
        // 分类维护在「字典管理」页面 —— 树只读，全部写操作按钮关闭
        TreeConfig.AllowAddChild = false;
        TreeConfig.AllowEdit = false;
        TreeConfig.AllowDelete = false;
        TreeConfig.AllowRename = false;
        TreeConfig.AllowToggle = false;               // 分类启停在字典页面做
        TreeConfig.NoSelectionBehavior = "all";       // 未选中分类（默认"全部"节点）时返回全量
        TreeConfig.EnableField = "IsValid";

        // 注册行操作（表格：启用/禁用技能）→ 自动注入 RowButtons.CustomButtons，前端按行状态二选一渲染
        RegisterRowAction("disable", DisableSkillAsync);
        RegisterRowAction("enable", EnableSkillAsync);
    }

    // ========================================================
    // 一、配置获取
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
    /// 行按钮配置：仅 Edit + Delete。
    /// 自定义禁用/启用按钮通过 RegisterRowAction 自动注入到 CustomButtons。
    /// 不设置 Enable = true，避免基类额外追加 toggle-valid 按钮造成重复。
    /// </summary>
    protected override RowButtonConfig GetRowButtons()
    {
        return new RowButtonConfig
        {
            Edit = true,
            Delete = true,
            Enable = false
        };
    }

    // ========================================================
    // 二、树数据加载（override：按 skill_category 字典项查询）
    // ========================================================

    /// <summary>
    /// 获取根节点 = skill_category 字典下的全部有效字典项（扁平一级）
    /// POST /api/Workflow/SkillTreeTable/tree/root
    /// </summary>
    [HttpPost("tree/root")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetRootNodes()
    {
        try
        {
            var dictCode = await ResolveCategoryDictCodeAsync();
            if (dictCode == null)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(
                    $"字典「技能分类」(DicNo={CategoryDictNo}) 未配置，请先在「字典管理」中创建"));

            var result = await TreeEntity.GetListAsync(x => x.DicCode == dictCode && x.IsValid == 1);
            if (!result.Success)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(result.Error!));

            // DicValue 为空的字典项无法作为关联值（wf_skill.CategoryCode），跳过防止脏节点
            var items = (result.Data ?? new List<CertPlatform.Shared.Entities.Wf.SkillCategoryDict>())
                .Where(x => !string.IsNullOrWhiteSpace(x.DicValue))
                .OrderBy(x => x.OrderNo ?? 0)
                .ThenBy(x => x.DicName)
                .ToList();

            var dtos = items.Select(item => MapToTreeItem(item, 0)).ToList();
            await FillIsLeafBatch(dtos);

            return Ok(ApiResponse<TreeItemDto[]>.Ok(dtos.ToArray()));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TreeItemDto[]>.Fail($"加载根节点失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 懒加载子节点 —— 分类扁平无层级，恒返回空
    /// POST /api/Workflow/SkillTreeTable/tree/children
    /// </summary>
    [HttpPost("tree/children")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetChildren(
        [FromBody] TreeChildrenRequest request)
    {
        await Task.CompletedTask;
        return Ok(ApiResponse<TreeItemDto[]>.Ok(Array.Empty<TreeItemDto>()));
    }

    /// <summary>批量 isLeaf —— 扁平分类恒为叶子（避免基类按 ParentCode 查字典项表）</summary>
    protected override Task FillIsLeafBatch(List<TreeItemDto> dtos)
    {
        foreach (var dto in dtos)
        {
            dto.IsLeaf = true;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 实体 → TreeItemDto 映射：补 Color（TreeMapper 自动提取 IsValid/Remark/OrderNo，不含 Color）
    /// </summary>
    protected override TreeItemDto MapToTreeItem(
        CertPlatform.Shared.Entities.Wf.SkillCategoryDict entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra ??= new Dictionary<string, object>();
        dto.Extra["Color"] = entity.Color ?? "";
        dto.Extra["IsValid"] = entity.IsValid;
        return dto;
    }

    // ========================================================
    // 三、树写操作拦截（分类维护在「字典管理」页面）
    //     前端 TreeConfig 全关（AllowEdit/AllowDelete/AllowToggle=false）已不发请求；
    //     此处钩子兜底，防直连接口误写字典项表
    // ========================================================

    /// <summary>拦截：不在技能页新增分类</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeAddTree(
        CertPlatform.Shared.Entities.Wf.SkillCategoryDict entity)
    {
        return Task.FromResult<(bool, string?)>(
            (false, "技能分类在「系统参数配置 → 数据字典」中维护，此处不允许新增"));
    }

    /// <summary>拦截：不在技能页修改分类</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeUpdateTree(
        CertPlatform.Shared.Entities.Wf.SkillCategoryDict entity)
    {
        return Task.FromResult<(bool, string?)>(
            (false, "技能分类在「系统参数配置 → 数据字典」中维护，此处不允许修改"));
    }

    /// <summary>拦截：不在技能页删除分类</summary>
    protected override Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        return Task.FromResult<(bool, string?)>(
            (false, "技能分类在「系统参数配置 → 数据字典」中维护，此处不允许删除"));
    }

    /// <summary>拦截：分类启停在字典页面做（字典项 IsValid=0 后自动从本树消失）</summary>
    [HttpPost("tree/toggle-valid")]
    public override Task<ActionResult<ApiResponse<object?>>> ToggleTreeNodeIsValid(
        [FromBody] JsonElement entityData)
    {
        return Task.FromResult<ActionResult<ApiResponse<object?>>>(
            Ok(ApiResponse<object?>.Fail("技能分类在「系统参数配置 → 数据字典」中维护，此处不允许启停")));
    }

    // ========================================================
    // 四、表格（技能）生命周期钩子
    // ========================================================

    /// <summary>
    /// 新增技能前校验：编码必填 + 唯一
    /// （2026-09-26 移除 Guid.NewGuid 兜底 —— 编码必须语义可读，禁止自动生成 GUID）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        CertPlatform.Shared.Entities.Wf.Skill entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "缺少业务键编码 Code，请填写技能编码");

        var exists = await Entity.ExistsAsync(s => s.Code == entity.Code);
        if (exists.Data)
            return (false, $"编码【{entity.Code}】已存在");

        return (true, null);
    }

    /// <summary>
    /// 修改技能前校验：名称唯一（排除自身）
    /// （Code 为定位键，Update 忽略 Code 列不可改；原实现条件自相矛盾恒 false，2026-09-26 修复）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(
        CertPlatform.Shared.Entities.Wf.Skill entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Code))
            return (false, "缺少业务键编码 Code");

        var exists = await Entity.ExistsAsync(s => s.Code != entity.Code && s.Name == entity.Name);
        if (exists.Data)
            return (false, $"名称【{entity.Name}】已存在");

        return (true, null);
    }

    // ========================================================
    // 五、技能 启用/禁用操作
    // ========================================================

    /// <summary>
    /// 禁用技能
    /// POST /api/Workflow/SkillTreeTable/action/disable
    /// 请求体 = 行实体 { Code: "技能编码" }
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> DisableSkillAsync(
        CertPlatform.Shared.Entities.Wf.Skill entity)
    {
        // GetByCode：内置 IsValid=1 过滤（当前为启用状态才可禁用）
        var result = await Entity.GetByCode(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("技能不存在或已被禁用");

        var skill = result.Data;
        skill.IsValid = 0;
        var updateResult = await Entity.Update(skill, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已禁用该技能"));
    }

    /// <summary>
    /// 启用技能
    /// POST /api/Workflow/SkillTreeTable/action/enable
    /// 请求体 = 行实体 { Code: "技能编码" }
    /// </summary>
    private async Task<Result<ApiResponse<object?>>> EnableSkillAsync(
        CertPlatform.Shared.Entities.Wf.Skill entity)
    {
        // GetByCodeAny：已禁用技能 IsValid=0，GetByCode 查不到
        var result = await Entity.GetByCodeAny(entity.Code);
        if (!result.Success || result.Data == null)
            return Result<ApiResponse<object?>>.Fail("技能不存在");

        var skill = result.Data;
        skill.IsValid = 1;
        var updateResult = await Entity.Update(skill, UserContext.ClientIp);
        if (!updateResult.Success)
            return Result<ApiResponse<object?>>.Fail(updateResult.Error);

        return Result<ApiResponse<object?>>.Ok(ApiResponse<object?>.Ok("已启用该技能"));
    }

    // ========================================================
    // 六、私有辅助
    // ========================================================

    /// <summary>按 DicNo 解析 skill_category 字典 Code（带进程内缓存）</summary>
    private async Task<string?> ResolveCategoryDictCodeAsync()
    {
        if (!string.IsNullOrEmpty(_categoryDictCode))
            return _categoryDictCode;

        var result = await _dictionaryService.GetListAsync(d => d.DicNo == CategoryDictNo, includeDisabled: true);
        var dict = (result.Data ?? new List<Sys_Dictionary>())
            .OrderBy(d => d.Id)
            .FirstOrDefault();
        if (dict == null)
            return null;

        _categoryDictCode = dict.Code;
        return _categoryDictCode;
    }
}
