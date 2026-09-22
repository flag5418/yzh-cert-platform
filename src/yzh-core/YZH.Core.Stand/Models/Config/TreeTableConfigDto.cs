namespace YZH.Core.Stand.Models.Config;

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
    /// <summary>是否允许「新增下级」节点动作（扁平无层级的树设为 false）</summary>
    public bool AllowAddChild { get; set; } = true;
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
