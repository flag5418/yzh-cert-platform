using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Stand.Models.Config;

/// <summary>
///     前端 DTO：列配置（PascalCase 命名，与数据库列名、实体属性名一致）
///     规则：实体属性是什么，JSON 就是什么，数据库列就是什么
/// </summary>
public class ColumnConfigDto
{
    public string FieldName { get; set; } = string.Empty;
    public string DesName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool XsFlag { get; set; }
    public bool BcFlag { get; set; }
    public bool Yxk { get; set; }
    public bool Enable { get; set; } = true;
    public bool Sortable { get; set; }
    public int? Width { get; set; }
    public string? Fixed { get; set; }
    public string? Align { get; set; }
    public string? DictCode { get; set; }
    public string? Format { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public object? Mrz { get; set; }
    /// <summary>分组索引（编辑模式控制）："0"=默认可编辑，"1"+=特定模式只读，"99"=详情全部只读</summary>
    public string? GroupIndex { get; set; }
}

/// <summary>
///     前端 DTO：搜索字段配置
/// </summary>
public class SearchFieldDto
{
    public string Label { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "like";
    public string ControlType { get; set; } = "input";
    public List<SelectOptionDto>? Options { get; set; }
    public int Width { get; set; } = 180;
}

public class SelectOptionDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
///     前端 DTO：工具栏配置
/// </summary>
public class ToolbarConfigDto
{
    public bool? Add { get; set; }
    public bool? Delete { get; set; }
    public bool? Export { get; set; }
    public bool? Import { get; set; }
    public Dictionary<string, string>? CustomButtons { get; set; }
}

/// <summary>
///     前端 DTO：行按钮配置
/// </summary>
public class RowButtonConfigDto
{
    public bool? Edit { get; set; }
    public bool? Delete { get; set; }
    public Dictionary<string, string>? CustomButtons { get; set; }
}

/// <summary>
///     前端 DTO：实体配置（PascalCase 命名，与数据库列名、实体属性名一致）
/// </summary>
public class EntityConfigDto
{
    public string Title { get; set; } = string.Empty;
    public string? FillMode { get; set; }
    /// <summary>表单布局列数（1=单列，2=双列，0=自动：BcFlag字段≤10用1列，>10用2列）</summary>
    public int FormCols { get; set; } = 0;
    public List<ColumnConfigDto> Columns { get; set; } = new();
    public Dictionary<string, object>? NewEntity { get; set; }
    public Dictionary<string, EntityFieldSchema>? Schema { get; set; }

    // 兼容旧字段（来自 JSON 文件）
    public string? ConfigName { get; set; }
    public string? TableName { get; set; }

    // 运行时可注入
    public ToolbarConfigDto? Toolbar { get; set; }
    public RowButtonConfigDto? RowButtons { get; set; }
    public List<SearchFieldDto>? SearchFields { get; set; }

    /// <summary>启用/禁用字段名（默认 IsValid，前端根据此字段显示启用/禁用按钮）</summary>
    public string EnableField { get; set; } = "IsValid";
}

/// <summary>
///     前端 DTO：左树右表完整配置
/// </summary>
public class TreeTableConfigDto
{
    public EntityConfigDto TableConfig { get; set; } = new();
    public TreeBehaviorConfigDto TreeConfig { get; set; } = new();
    public EntityConfigDto? TreeFormConfig { get; set; }
}

/// <summary>
///     前端 DTO：树行为配置（PascalCase 命名）
/// </summary>
public class TreeBehaviorConfigDto
{
    public bool Lazy { get; set; } = true;
    public bool AllowEdit { get; set; } = true;
    public bool AllowDelete { get; set; } = true;
    public bool AllowRename { get; set; } = true;
    public object? RootParentCode { get; set; }
    public string NameField { get; set; } = "Name";
    public string CodeField { get; set; } = "Code";
    public string ParentCodeField { get; set; } = "ParentCode";
    public string RelateField { get; set; } = string.Empty;
    public string NoSelectionBehavior { get; set; } = "empty";
    public int MaxLevel { get; set; } = 0;
    public bool AllowDeleteWithChildren { get; set; } = false;
    /// <summary>自定义节点操作按钮：{ 方法名: 显示文字 }</summary>
    public Dictionary<string, string>? CustomActions { get; set; }
    /// <summary>启用/禁用字段名（默认 IsValid）</summary>
    public string EnableField { get; set; } = "IsValid";
}
