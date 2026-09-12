using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.System;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Web.Controllers.System;

/// <summary>
///     数据字典控制器（左树右表）—— 基础能力，每个项目都需要
///
///     左树：Sys_Dictionary（字典/分类树，懒加载 + 增删改 + 启用禁用）
///     右表：Sys_DictionaryList（选中字典后展示其字典项，分页 + 过滤 + 增删改 + 启用禁用）
///
///     形态与 OrganizationController（机构-人员）完全同构：
///         左树 Sys_Organization → Sys_Dictionary
///         右表 Sys_User         → Sys_DictionaryList
///         关联 Sys_User.OrgCode → Sys_DictionaryList.DicCode
///
///     ══════════════════════════════════════════════════════════════════
///     【两条架构铁律】
///     ① 关联一律走 Code。定位记录的核心字段是 Code；两表已无 ParentId / Dic_ID /
///        CreateID / ModifyID 等任何 Id 型关联字段。
///     ② Code 与业务编码分离。Code 是随机生成、全局唯一、永不重复的稳定标识，也是唯一关联键；
///        字典编码 DicNo 只是普通属性，可为空、仅用于显示，不参与任何关联。
///        → 因此本控制器【不需要写任何 Code 生成逻辑】：
///          · 树节点：TreeTableControllerBase.AddTreeNode 已自动填 Guid.NewGuid().ToString("N")
///          · 字典项：YzhControllerBase.AddCore 已自动填 Guid.NewGuid().ToString("N")
///          且 SqlSugarDbOrm.UpdateAsync 会 IgnoreColumns("Code")，即 Code 插入后不可修改。
///     ══════════════════════════════════════════════════════════════════
///
///     【删除语义】字典与字典项一律软删除（[YZHDeleteStrategy(Mode = DeleteMode.Soft)]）：
///     字典/分类可能被多个业务引用，删除前做业务判断逻辑极复杂且易漏。
///     因此：
///       · OnBeforeDeleteTree 不做任何校验
///       · AllowDeleteWithChildren = true —— 删除分类时级联软删除其下全部字典，
///         避免"必须逐层清空"这种变相的前置判断。数据可恢复（IsDeleted 置位，行保留）。
///
///     【API 路由】
///     --- 树（字典/分类） ---
///     POST  /api/System/Dictionary/tree/root                根节点
///     POST  /api/System/Dictionary/tree/children            懒加载子节点
///     POST  /api/System/Dictionary/tree/add                 新增字典/分类
///     POST  /api/System/Dictionary/tree/update              修改字典/分类
///     POST  /api/System/Dictionary/tree/delete              删除（软删除，级联）
///     POST  /api/System/Dictionary/tree/toggle-valid        启用/禁用
///     --- 右表（字典项） ---
///     POST  /api/System/Dictionary/filter                   分页（自动注入 DicCode 过滤）
///     POST  /api/System/Dictionary/add|update|delete        字典项增删改
///     POST  /api/System/Dictionary/toggle-valid             启用/禁用字典项
///     --- 配置 ---
///     GET   /api/System/Dictionary/treepconfig              TreeTableConfig（TableConfig + TreeConfig + TreeFormConfig）
///     GET   /api/System/Dictionary/config                   右表 EntityConfig
///     --- 字典消费（供业务页面取下拉数据） ---
///     GET   /api/System/Dictionary/items/{code}             取某字典下的字典项：value=字典项Code，label=显示文本
///     GET   /api/System/Dictionary/category/{code}/dictionaries  取某分类下的字典：value=字典Code，label=字典名称
///     以上两个消费端点均自动过滤已禁用（IsValid=1）与已删除（IsDeleted=0）的记录。
/// </summary>
[ApiController]
[Route("api/System/[controller]")]
[Route("api/[controller]")]
public class DictionaryController : TreeTableControllerBase<Sys_Dictionary, Sys_DictionaryList>
{
    /// <summary>字典项配置文件名（Assets/EntityConfigs/System/Sys_DictionaryList.json）</summary>
    private const string TableConfigName = "System/Sys_DictionaryList";

    public DictionaryController(
        EntityService<Sys_Dictionary> treeEntityService,
        EntityService<Sys_DictionaryList> tableEntityService,
        IUserContext userContext)
        : base(treeEntityService, tableEntityService, userContext)
    {
        // ── TreeConfig（左树配置）──
        TreeConfig.NameField = "DicName";        // 树节点显示名
        TreeConfig.CodeField = "Code";           // 树节点编码
        TreeConfig.ParentCodeField = "ParentCode"; // 树父子关系
        TreeConfig.RelateField = "DicCode";      // 右表过滤字段（FilterCore 注入 {DicCode, eq, 节点Code}）
        TreeConfig.MaxLevel = 3;                 // 分类 → 字典 → （字典项不在树上），3 层足够
        // 删除分类时级联软删除子字典；见类注释「删除语义」
        TreeConfig.AllowDeleteWithChildren = true;

        // ── 树节点编辑弹窗表单配置 ──
        TreeFormConfigName = "System/Sys_DictionaryForm";
    }

    // ========================================================
    // 一、配置
    // ========================================================

    /// <summary>严格配置加载：JSON 缺失或字段为空时直接抛错，避免"页面空白且无报错"</summary>
    protected override bool StrictConfigLoad => true;

    /// <summary>
    ///     右表（字典项）EntityConfig
    ///     来源：Assets/EntityConfigs/System/Sys_DictionaryList.json
    /// </summary>
    protected override EntityConfig LoadConfig()
    {
        var config = EntityConfigHelper.GetConfig(TableConfigName);
        // 基类的缺失检测在本方法被覆盖后不会自动触发，这里显式补一次
        if (config.Columns == null || config.Columns.Count == 0)
            OnConfigMissing(nameof(Sys_DictionaryList));
        return config;
    }

    // ========================================================
    // 二、树节点（字典/分类）生命周期钩子
    // ========================================================

    /// <summary>
    ///     新增字典/分类前校验
    ///     注意：Code 由框架自动填充随机唯一值（TreeTableControllerBase.cs:146-147），此处不生成。
    /// </summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAddTree(Sys_Dictionary entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DicName))
            return (false, "字典名称不能为空");

        // 同一父节点下不允许重名（数据卫生；已软删除/已禁用的记录由框架自动排除）
        var dupName = await TreeEntity.ExistsAsync(d =>
            d.ParentCode == entity.ParentCode && d.DicName == entity.DicName);
        if (dupName.Data)
            return (false, $"同级下已存在名为【{entity.DicName}】的字典");

        // 字典编码是可选属性：不填放过，填了必须唯一
        if (!string.IsNullOrWhiteSpace(entity.DicNo))
        {
            var dupNo = await TreeEntity.ExistsAsync(d => d.DicNo == entity.DicNo);
            if (dupNo.Data)
                return (false, $"字典编码【{entity.DicNo}】已存在");
        }

        entity.Enable = 1;
        return (true, null);
    }

    /// <summary>修改字典/分类前校验（DicNo 可自由修改，Code 不受影响）</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdateTree(Sys_Dictionary entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DicName))
            return (false, "字典名称不能为空");

        var dupName = await TreeEntity.ExistsAsync(d =>
            d.Code != entity.Code &&
            d.ParentCode == entity.ParentCode &&
            d.DicName == entity.DicName);
        if (dupName.Data)
            return (false, $"同级下已存在名为【{entity.DicName}】的字典");

        if (!string.IsNullOrWhiteSpace(entity.DicNo))
        {
            var dupNo = await TreeEntity.ExistsAsync(d =>
                d.Code != entity.Code && d.DicNo == entity.DicNo);
            if (dupNo.Data)
                return (false, $"字典编码【{entity.DicNo}】已存在");
        }

        return (true, null);
    }

    /// <summary>
    ///     删除字典/分类前 —— 不做任何业务校验。
    ///     字典与分类可能被多个业务引用，删除前逐项判断引用关系逻辑极复杂且必然遗漏；
    ///     故一律软删除（行保留、IsDeleted 置位、可恢复），把"是否有引用"的判断交给业务侧按需处理。
    /// </summary>
    protected override Task<(bool ok, string? msg)> OnBeforeDeleteTree(string[] codes)
        => Task.FromResult<(bool, string?)>((true, null));

    // ========================================================
    // 三、字典项（右表）生命周期钩子
    // ========================================================

    /// <summary>新增字典项前校验。Code 由框架自动填充随机唯一值（YzhControllerBase.AddCore:218-223）。</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeAdd(Sys_DictionaryList entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DicCode))
            return (false, "缺少所属字典");

        // 父字典必须存在且未删除
        var parent = await TreeEntity.GetByCode(entity.DicCode);
        if (!parent.Success || parent.Data == null)
            return (false, "所属字典不存在或已被删除");

        if (string.IsNullOrWhiteSpace(entity.DicName))
            return (false, "显示文本不能为空");

        // 同一字典内显示文本不允许重复
        var dup = await Entity.ExistsAsync(x =>
            x.DicCode == entity.DicCode && x.DicName == entity.DicName);
        if (dup.Data)
            return (false, $"该字典下已存在显示文本【{entity.DicName}】");

        return (true, null);
    }

    /// <summary>修改字典项前校验</summary>
    protected override async Task<(bool ok, string? msg)> OnBeforeUpdate(Sys_DictionaryList entity)
    {
        if (string.IsNullOrWhiteSpace(entity.DicName))
            return (false, "显示文本不能为空");

        var dup = await Entity.ExistsAsync(x =>
            x.Code != entity.Code &&
            x.DicCode == entity.DicCode &&
            x.DicName == entity.DicName);
        if (dup.Data)
            return (false, $"该字典下已存在显示文本【{entity.DicName}】");

        return (true, null);
    }

    // ========================================================
    // 四、字典消费端点（供业务页面取下拉数据）
    // ========================================================

    /// <summary>
    ///     取某个字典下的全部字典项（下拉框数据源）
    ///
    ///     GET /api/System/Dictionary/items/{code}
    ///     入参 code = Sys_Dictionary.Code（即"分类/字典"的 Code，不是 DicNo）
    ///     返回 [ { Value: 字典项Code, Label: 显示文本, Color } ]
    ///
    ///     · value 取 Code（随机唯一、稳定，业务侧按 Code 关联）
    ///     · label 取 DicName（显示文本）
    ///     · 自动过滤已禁用（IsValid=0）与已软删除（IsDeleted=1）的记录
    ///       —— EntityService.GetListAsync 内置 IsValid=1 AND IsDeleted=0 过滤
    ///     · 按 OrderNo 升序、其次 DicName 升序
    /// </summary>
    [HttpGet("items/{code}")]
    public virtual async Task<ActionResult<ApiResponse<List<DictOptionDto>>>> GetItems(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(ApiResponse<List<DictOptionDto>>.Fail("字典 Code 不能为空"));

        // 显式带上 IsValid 条件：与框架内置过滤形成双保险，同时让生成 SQL 中可见该过滤
        var result = await Entity.GetListAsync(x => x.DicCode == code && x.IsValid == 1);
        if (!result.Success)
            return BadRequest(ApiResponse<List<DictOptionDto>>.Fail(result.Error!));

        var options = (result.Data ?? new List<Sys_DictionaryList>())
            .OrderBy(x => x.OrderNo ?? 0)
            .ThenBy(x => x.DicName)
            .Select(ToOption)
            .ToList();

        return Ok(ApiResponse<List<DictOptionDto>>.Ok(options));
    }

    /// <summary>
    ///     取某个分类下的全部字典（下拉框数据源）
    ///
    ///     GET /api/System/Dictionary/category/{code}/dictionaries
    ///     入参 code = 分类节点（Sys_Dictionary）的 Code；传空串表示取根级分类
    ///     返回 [ { Value: 字典Code, Label: 字典名称 } ]
    ///
    ///     · value 取字典的 Code，label 取字典的 DicName
    ///     · 同样自动过滤已禁用（IsValid=0）与已软删除（IsDeleted=1）的记录
    /// </summary>
    [HttpGet("category/{code}/dictionaries")]
    public virtual async Task<ActionResult<ApiResponse<List<DictOptionDto>>>> GetDictionariesByCategory(string code)
    {
        var result = await TreeEntity.GetListAsync(d =>
            d.ParentCode == code && d.IsValid == 1);
        if (!result.Success)
            return BadRequest(ApiResponse<List<DictOptionDto>>.Fail(result.Error!));

        var options = (result.Data ?? new List<Sys_Dictionary>())
            .OrderBy(x => x.OrderNo ?? 0)
            .ThenBy(x => x.DicName)
            .Select(d => new DictOptionDto { Value = d.Code, Label = d.DicName })
            .ToList();

        return Ok(ApiResponse<List<DictOptionDto>>.Ok(options));
    }

    // ========================================================
    // 五、树节点 DTO 扩展
    // ========================================================

    /// <summary>
    ///     扩展树节点 Extra 载荷
    ///
    ///     框架 TreeMapper 只自动提取 Enable/Remark/Status/OrderNo/Sort/Level 等，
    ///     且【键名为 camelCase、不含 IsValid】。而前端：
    ///     · 「禁用/启用」按钮文案依赖 extra[EnableField]（= "IsValid"）
    ///     · 编辑弹窗回填依赖各表单字段
    ///     故此处按【PascalCase】补齐，与 EntityConfig 的 FieldName / YzhForm 的 prop 保持同一套命名。
    /// </summary>
    protected override TreeItemDto MapToTreeItem(Sys_Dictionary entity, int level)
    {
        var dto = base.MapToTreeItem(entity, level);
        dto.Extra ??= new Dictionary<string, object>();
        dto.Extra["IsValid"] = entity.IsValid;
        dto.Extra["DicNo"] = entity.DicNo ?? string.Empty;
        dto.Extra["OrderNo"] = entity.OrderNo ?? 0;
        dto.Extra["Remark"] = entity.Remark ?? string.Empty;
        return dto;
    }

    // ========================================================
    // 六、私有辅助
    // ========================================================

    private static DictOptionDto ToOption(Sys_DictionaryList item) => new()
    {
        Value = item.Code,
        Label = item.DicName,
        Color = item.Color
    };
}

/// <summary>
///     字典下拉选项 DTO
///     value = 记录 Code（随机唯一、稳定，业务侧按 Code 关联）
///     label = 显示文本
/// </summary>
public class DictOptionDto
{
    /// <summary>选项值（记录 Code）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>显示文本</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>标签颜色（可选，来自 Sys_DictionaryList.Color）</summary>
    public string? Color { get; set; }
}
