
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
/// 左树：技能分类（扁平一级）—— 数据源 = 字典「技能分类」(DicNo='skill_category') 的字典项
///       分类的增删改/启停<b>在当前页面维护</b>（2026-10-09 改为就地维护，方便随时调整分类）
/// 右表：技能（选中分类后分页加载，含启用/禁用行操作）
///
/// 关联：树节点 Code = 字典项 DicValue（data_access 等语义值）→ wf_skill.CategoryCode 同值关联
///       （原 wf_skill_category 表已废弃删除，GUID 关联值已迁移为 DicValue）
///
/// 继承 TreeTableControllerBase 获得能力：
/// - 树读取：/tree/root（override 过滤 skill_category 字典项） /tree/children（扁平恒空）
/// - 树写入：/tree/add|update|delete（就地维护，OnBefore* 钩子校验）
/// - 单表 CRUD：/filter /add /update /delete /toggle-valid
/// - 行操作：/action/disable /action/enable（RegisterRowAction，按行状态二选一按钮）
/// - 配置：/treepconfig /config
///
/// ⚠️ GUID/DicValue 映射：
///   前端树节点 Code = DicValue（业务编码），后端实体 Code = GUID（行标识）。
///   Update 时前端发 {Code: DicValue, DicName, DicValue, ...} → 本控制器
///   先按 DicValue 查回 GUID 实体，再合并表单字段后 Update。
///   Delete 时前端发 [DicValue, ...] → 先批量查回 GUID 再调基类删除。
///
/// API 路由：
/// POST   /api/Workflow/SkillTreeTable/tree/root             获取根节点（skill_category 字典项）
/// POST   /api/Workflow/SkillTreeTable/tree/add              新增分类（字典项）
/// POST   /api/Workflow/SkillTreeTable/tree/update           修改分类
/// POST   /api/Workflow/SkillTreeTable/tree/delete           删除分类（含外键校验）
/// POST   /api/Workflow/SkillTreeTable/filter                技能分页（自动注入分类过滤）
/// POST   /api/Workflow/SkillTreeTable/add|update|delete     技能增删改
/// POST   /api/Workflow/SkillTreeTable/toggle-valid          技能启用/禁用
/// POST   /api/Workflow/SkillTreeTable/action/disable|enable 技能禁用/启用（行按钮）
/// GET    /api/Workflow/SkillTreeTable/treepconfig           页面配置
/// </summary>
[ApiController]

/// <para><b>★ 端标记（2026-10-03）</b>：路由加 <c>Admin/</c> 段，与专家端 <c>/api/Auditor/*</c> 对称。
/// <para>背景：后台端 20 个 Controller 此前零端标记，4 个连业务域前缀都没有（<c>api/AIUsage</c>
/// <c>api/PromptTemplate</c> <c>api/ValidationRule</c> <c>api/ReportDefinition</c>），
/// 且 <c>api/System/[controller]</c> 与框架层 <c>YZH.Core.Web</c> 的 <c>api/System/*</c> 撞前缀。</para>
/// <para><b>不影响授权</b>：<c>ApiCode = Sha256("{METHOD}|{路由末段}|{动作名}")</c>（ApiScanner.cs:326-331）
/// 只取路由<b>末段</b>作控制器名，本 Controller 的末段未变 ⇒ <c>ApiCode</c> 不变 ⇒
/// <b>角色-接口关联不断裂</b>，无需重跑 ApiSync。</para>
[Route("api/Admin/Workflow/SkillTreeTable")]
public class SkillTreeTableController
    : TreeTableControllerBase<CertPlatform.Admin.Entities.Wf.SkillCategoryDict,
                             CertPlatform.Admin.Entities.Wf.Skill>
{
    /// <summary>技能分类字典的 DicNo（字典管理页面按此编码维护分类）</summary>
    private const string CategoryDictNo = "skill_category";

    /// <summary>字典服务（用于按 DicNo 解析字典 Code）</summary>
    private readonly EntityService<Sys_Dictionary> _dictionaryService;

    /// <summary>skill_category 字典 Code 进程内缓存（Code 插入后不可修改，缓存安全）</summary>
    private string? _categoryDictCode;

    public SkillTreeTableController(
        EntityService<CertPlatform.Admin.Entities.Wf.SkillCategoryDict> treeEntityService,
        EntityService<CertPlatform.Admin.Entities.Wf.Skill> tableEntityService,
        EntityService<Sys_Dictionary> dictionaryService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        _dictionaryService = dictionaryService;

        // ──── 左树配置（技能分类，就地维护） ────
        TreeConfig.NameField = "DicName";             // 树显示名 = 字典项 DicName
        TreeConfig.CodeField = "DicValue";            // 树 Code = 业务编码 DicValue（关联 wf_skill.CategoryCode，非字典项 GUID）
        TreeConfig.ParentCodeField = "ParentCode";    // 扁平结构恒 null
        TreeConfig.RelateField = "CategoryCode";      // 右表通过 CategoryCode 关联左树
        TreeConfig.MaxLevel = 2;                      // 两级：大类 → 子类
        // 分类在当前页面就地维护（2026-10-09 用户裁决：随时可能要调整分类）
        TreeConfig.AllowAddChild = true;              // 根级分类允许新增子类
        TreeConfig.AllowEdit = true;                  // 允许编辑分类
        TreeConfig.AllowDelete = true;                // 允许删除分类（含外键校验）
        TreeConfig.AllowRename = false;
        TreeConfig.AllowToggle = false;               // 分类启停由 IsValid 字段隐式控制（禁用后自动从树消失）
        TreeConfig.NoSelectionBehavior = "all";       // 未选中分类（默认"全部"节点）时返回全量
        TreeConfig.EnableField = "IsValid";

        // 树节点表单配置（弹窗新增/编辑分类时的表单字段）
        TreeFormConfigName = "Workflow/SkillCategoryForm";

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
    /// 获取根节点 = skill_category 字典下 ParentCode=null 的有效字典项（一级大类）
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

            // 仅查根级分类（ParentCode = null），子级通过 /tree/children 懒加载
            var result = await TreeEntity.GetListAsync(x =>
                x.DicCode == dictCode && x.ParentCode == null && x.IsValid == 1);
            if (!result.Success)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(result.Error!));

            // DicValue 为空的字典项无法作为关联值（wf_skill.CategoryCode），跳过防止脏节点
            var items = (result.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
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
    /// 懒加载子节点 —— 按 ParentCode 查询指定一级大类下的子类
    /// POST /api/Workflow/SkillTreeTable/tree/children
    /// </summary>
    [HttpPost("tree/children")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto[]>>> GetChildren(
        [FromBody] TreeChildrenRequest request)
    {
        try
        {
            var dictCode = await ResolveCategoryDictCodeAsync();
            if (dictCode == null)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(
                    $"字典「技能分类」(DicNo={CategoryDictNo}) 未配置"));

            // 按 ParentCode 查子级分类（同一 DicCode 下）
            var result = await TreeEntity.GetListAsync(x =>
                x.DicCode == dictCode && x.ParentCode == request.ParentCode && x.IsValid == 1);
            if (!result.Success)
                return Ok(ApiResponse<TreeItemDto[]>.Fail(result.Error!));

            var items = (result.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
                .Where(x => !string.IsNullOrWhiteSpace(x.DicValue))
                .OrderBy(x => x.OrderNo ?? 0)
                .ThenBy(x => x.DicName)
                .ToList();

            var dtos = items.Select(item => MapToTreeItem(item, request.Level + 1)).ToList();
            await FillIsLeafBatch(dtos);
            return Ok(ApiResponse<TreeItemDto[]>.Ok(dtos.ToArray()));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<TreeItemDto[]>.Fail($"加载子节点失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 实体 → TreeItemDto 映射：补 Color + DicValue + OrderNo + ParentCode（PascalCase）
    ///
    /// 前端 openTreeNodeDialog 编辑回填逻辑（TreeTableCore.ts:632-635）：
    ///   从 node.Extra 按 treeFormFields 的 prop 白名单取值（prop = FieldName，PascalCase）。
    ///   · TreeMapper 自动以「小写 orderNo」放入 Extra → 与前端 prop `OrderNo` 不匹配 → 回填空
    ///   · DicValue 完全不在 Extra 中 → 回填空
    ///   故需显式以 PascalCase 注入，确保前端能匹配。
    /// </summary>
    protected override TreeItemDto MapToTreeItem(
        CertPlatform.Admin.Entities.Wf.SkillCategoryDict entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra ??= new Dictionary<string, object>();
        dto.Extra["Color"] = entity.Color ?? "";
        dto.Extra["IsValid"] = entity.IsValid;
        dto.Extra["DicValue"] = entity.DicValue ?? "";
        dto.Extra["OrderNo"] = entity.OrderNo ?? 0;
        dto.Extra["ParentCode"] = entity.ParentCode ?? "";
        return dto;
    }

    // ========================================================
    // 三、树写操作校验（分类在当前页面就地维护）
    // ========================================================

    /// <summary>
    /// 新增分类前校验：DicName/DicValue 必填 + DicValue 唯一 + 自动填充 DicCode
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(
        CertPlatform.Admin.Entities.Wf.SkillCategoryDict entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DicName))
            return (false, "分类名称不能为空");

        if (string.IsNullOrWhiteSpace(entity.DicValue))
            return (false, "分类编码（DicValue）不能为空");

        // 自动填充 DicCode（skill_category 字典 Code）
        var dictCode = await ResolveCategoryDictCodeAsync();
        if (dictCode == null)
            return (false, $"字典「技能分类」(DicNo={CategoryDictNo}) 未配置，请先在「字典管理」中创建");
        entity.DicCode = dictCode;

        // DicValue 唯一性校验（同一字典下不允许重复）
        var exists = await TreeEntity.ExistsAsync(x => x.DicCode == dictCode && x.DicValue == entity.DicValue);
        if (exists.Data)
            return (false, $"分类编码【{entity.DicValue}】已存在");

        // IsValid 默认 1
        entity.IsValid = 1;
        return (true, null);
    }

    /// <summary>
    /// 修改分类前校验：DicName 不可空 + DicValue 唯一（排除自身） + 子级分类不允许改父级（保两层结构）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(
        CertPlatform.Admin.Entities.Wf.SkillCategoryDict entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DicName))
            return (false, "分类名称不能为空");

        if (string.IsNullOrWhiteSpace(entity.DicValue))
            return (false, "分类编码（DicValue）不能为空");

        var dictCode = await ResolveCategoryDictCodeAsync();
        if (dictCode == null)
            return (false, $"字典「技能分类」(DicNo={CategoryDictNo}) 未配置");

        // 子级分类不允许改父级（保两层结构）
        var existingList = await TreeEntity.GetListAsync(x => x.DicCode == dictCode && x.Code == entity.Code);
        var existingItem = (existingList.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>()).FirstOrDefault();
        if (existingItem != null && existingItem.ParentCode != null && existingItem.ParentCode != entity.ParentCode)
            return (false, "子分类不允许修改上级分类");

        // DicValue 唯一性校验（排除自身：按 DicValue 查出的记录不能是别的 GUID）
        var dup = await TreeEntity.GetListAsync(x => x.DicCode == dictCode && x.DicValue == entity.DicValue);
        var conflict = (dup.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
            .FirstOrDefault(x => x.Code != entity.Code);
        if (conflict != null)
            return (false, $"分类编码【{entity.DicValue}】已存在");

        return (true, null);
    }

    /// <summary>
    /// 删除分类前校验：检查是否有技能正在引用该分类
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
    {
        if (codes == null || codes.Length == 0)
            return (false, "未指定要删除的分类");

        // 前端传的是 DicValue（树 Code = DicValue），需查回 GUID 再检查外键
        var dictCode = await ResolveCategoryDictCodeAsync();
        if (dictCode == null)
            return (false, $"字典「技能分类」(DicNo={CategoryDictNo}) 未配置");

        var items = await TreeEntity.GetListAsync(x => x.DicCode == dictCode && codes.Contains(x.DicValue));
        var guids = (items.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
            .Select(x => x.Code)
            .ToList();

        // 检查每个 GUID 下是否有技能引用
        foreach (var guid in guids)
        {
            var categoryItem = (items.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
                .FirstOrDefault(x => x.Code == guid);
            if (categoryItem == null) continue;

            var skillCount = await Entity.CountAsync(s => s.CategoryCode == categoryItem.DicValue);
            if (skillCount.Data > 0)
                return (false, $"分类【{categoryItem.DicName}】下有 {skillCount.Data} 个技能引用，无法删除");
        }

        return (true, null);
    }

    // ========================================================
    // 3.5 GUID/DicValue 映射处理（前端 Code = DicValue，后端 Code = GUID）
    // ========================================================

    /// <summary>
    /// 覆盖 UpdateTreeNode：前端发 {Code: DicValue, DicName, DicValue, ...}
    /// → 先按 DicValue 查回 GUID 实体 → 合并表单字段 → 调基类 Update
    /// </summary>
    [HttpPost("tree/update")]
    public override async Task<ActionResult<ApiResponse<TreeItemDto>>> UpdateTreeNode(
        [FromBody] CertPlatform.Admin.Entities.Wf.SkillCategoryDict entity)
    {
        try
        {
            // 1. 修改前钩子（校验 DicName/DicValue 必填 + 唯一性）
            var (ok, cancelMsg) = await OnBeforeUpdateTree(entity);
            if (!ok) return Ok(ApiResponse.Fail(cancelMsg ?? "操作已取消"));

            // 2. 按 DicValue 查回 GUID 实体
            var dictCode = await ResolveCategoryDictCodeAsync();
            if (dictCode == null)
                return Ok(ApiResponse.Fail($"字典「技能分类」(DicNo={CategoryDictNo}) 未配置"));

            var existing = await TreeEntity.GetListAsync(x =>
                x.DicCode == dictCode && x.DicValue == entity.DicValue);
            var existingItem = (existing.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
                .FirstOrDefault();
            if (existingItem == null)
                return Ok(ApiResponse.Fail($"分类编码【{entity.DicValue}】不存在"));

            // 3. 合并表单字段到 GUID 实体（保留 Code/DicCode/IsValid 等）
            existingItem.DicName = entity.DicName;
            existingItem.DicValue = entity.DicValue;
            existingItem.Color = entity.Color;
            existingItem.OrderNo = entity.OrderNo;
            existingItem.Remark = entity.Remark;

            // 4. 执行修改
            var result = await TreeEntity.Update(existingItem, UserContext.ClientIp);
            if (!result.Success) return Ok(ApiResponse.Fail(result.Error));

            // 5. 修改后钩子
            await OnAfterUpdateTree(result.Data!);
            await OnAfterCommitted();

            // 6. 回填 IsLeaf
            var updateDto = MapToTreeItem(result.Data!, 0);
            await FillIsLeafBatch(new List<TreeItemDto> { updateDto });
            return Ok(ApiResponse<TreeItemDto>.Ok(updateDto, "修改成功"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail($"修改分类失败：{ex.Message}"));
        }
    }

    /// <summary>
    /// 覆盖 DeleteTreeNode：前端发 [DicValue, ...]
    /// → 先批量查回 GUID → 再调基类删除（基类按 GUID 执行）
    /// </summary>
    [HttpPost("tree/delete")]
    public override async Task<ActionResult<ApiResponse<string>>> DeleteTreeNode(
        [FromBody] string[] codes)
    {
        try
        {
            if (codes == null || codes.Length == 0)
                return Ok(ApiResponse.Fail("未指定要删除的分类"));

            // 1. 删除前钩子（外键校验）
            var (ok, cancelMsg) = await OnBeforeDeleteTree(codes);
            if (!ok) return Ok(ApiResponse.Fail(cancelMsg ?? "操作已取消"));

            // 2. 按 DicValue 查回 GUID
            var dictCode = await ResolveCategoryDictCodeAsync();
            if (dictCode == null)
                return Ok(ApiResponse.Fail($"字典「技能分类」(DicNo={CategoryDictNo}) 未配置"));

            var items = await TreeEntity.GetListAsync(x => x.DicCode == dictCode && codes.Contains(x.DicValue));
            var guids = (items.Data ?? new List<CertPlatform.Admin.Entities.Wf.SkillCategoryDict>())
                .Select(x => x.Code)
                .ToArray();

            if (guids.Length == 0)
                return Ok(ApiResponse.Fail("未找到指定的分类"));

            // 3. 调基类删除（按 GUID 执行）
            return await base.DeleteTreeNode(guids);
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse.Fail($"删除分类失败：{ex.Message}"));
        }
    }

    // ========================================================
    // 四、表格（技能）生命周期钩子
    // ========================================================

    /// <summary>
    /// 新增技能前校验：编码必填 + 唯一
    /// （2026-09-26 移除 Guid.NewGuid 兜底 —— 编码必须语义可读，禁止自动生成 GUID）
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(
        CertPlatform.Admin.Entities.Wf.Skill entity)
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
        CertPlatform.Admin.Entities.Wf.Skill entity)
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
        CertPlatform.Admin.Entities.Wf.Skill entity)
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
        CertPlatform.Admin.Entities.Wf.Skill entity)
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
