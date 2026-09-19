using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：实体页面完整配置
///
///     设计哲学 — "后端驱动，前端自适应"：
///     ┌─────────────────────────────────────────────────────────────────────┐
///     │  后端基类（YzhControllerBase）提供完整默认配置：                       │
///     │    1. Columns      → JSON 文件（静态结构，管理员可编辑）               │
///     │    2. SearchFields → 基类智能推断（选择 XsFlag=true 的字段）           │
///     │    3. RowButtons   → 基类默认（Edit=true, Delete=true）               │
///     │    4. Toolbar      → 基类默认（Add=true, Delete=true）                │
///     │    5. NewEntity    → 后端反射生成（SchemaHelper）                    │
///     │    6. Schema       → 后端反射生成（字段类型描述）                      │
///     │                                                                     │
///     │  继承类只需 override 需要定制的部分：                                  │
///     │    - GetSearchFields() 自定义搜索字段                                  │
///     │    - GetRowButtons()   自定义行按钮                                    │
///     │    - GetToolbar()      自定义工具栏                                    │
///     │                                                                     │
///     │  前端读取此配置 → 自动渲染表格、表单、搜索栏、按钮，零手写 UI              │
///     └─────────────────────────────────────────────────────────────────────┘
///
///     配置优先级（从高到低）：
///     1. 子类 override 虚方法（代码控制）
///     2. 基类默认推断（智能规则）
///     3. JSON 文件静态配置（仅 Columns 必须）
/// </summary>
public class EntityConfigDto
{
    /// <summary>页面标题（显示在页面顶部和表格上方）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>表格列填充模式（AutoFix=等比例填满，PixFix=按像素宽度横向滚动）</summary>
    public string? FillMode { get; set; }

    /// <summary>表单布局列数（1=单列，2=双列，0=自动：BcFlag字段≤10用1列，>10用2列）</summary>
    public int FormCols { get; set; } = 0;

    /// <summary>列/字段定义集合（同时驱动表格和表单，来源：JSON 文件）</summary>
    public List<ColumnConfigDto> Columns { get; set; } = new();

    /// <summary>
    ///     空实体模板（后端反射自动生成，前端直接用于初始化表单）
    ///     来源：EntitySchemaHelper.GetEmptyEntity＜V＞()
    /// </summary>
    public Dictionary<string, object>? NewEntity { get; set; }

    /// <summary>
    ///     字段结构描述（后端反射自动生成，前端用于表单校验和控件推断）
    ///     来源：EntitySchemaHelper.GetSchema＜V＞()
    /// </summary>
    public Dictionary<string, EntityFieldSchema>? Schema { get; set; }

    // ── JSON 文件兼容字段（历史遗留，新代码不应依赖） ──

    /// <summary>配置名称（历史兼容，通常等于 Title）</summary>
    public string? ConfigName { get; set; }

    /// <summary>数据库表名（历史兼容，用于显示）</summary>
    public string? TableName { get; set; }

    // ── 运行时由控制器注入 ──

    /// <summary>
    ///     工具栏按钮配置（页面顶部操作区）
    ///     默认：Add=true, Delete=true, Export=false, Import=false
    ///     子类可 override GetToolbar() 自定义
    /// </summary>
    public ToolbarConfigDto? Toolbar { get; set; }

    /// <summary>
    ///     行按钮配置（每行操作列）
    ///     默认：Edit=true, Delete=true
    ///     子类可 override GetRowButtons() 自定义
    /// </summary>
    public RowButtonConfigDto? RowButtons { get; set; }

    /// <summary>
    ///     搜索字段配置（搜索栏字段列表）
    ///     默认：从 Columns 中选择前 3 个 XsFlag=true 且非 Other 类型的字段
    ///     子类可 override GetSearchFields() 自定义
    /// </summary>
    public List<SearchFieldDto>? SearchFields { get; set; }

    /// <summary>启用/禁用字段名（默认 IsValid，前端根据此字段显示启用/禁用按钮）</summary>
    public string EnableField { get; set; } = "IsValid";
}
