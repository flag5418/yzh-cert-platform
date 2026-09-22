namespace YZH.Core.Stand.Models.Config;

/// <summary>树配置</summary>
public class TreeConfig
{
    /// <summary>是否懒加载（true=点击展开时加载子节点）</summary>
    public bool Lazy { get; set; } = true;

    /// <summary>是否允许编辑树节点（true=显示修改按钮）</summary>
    public bool AllowEdit { get; set; } = true;

    /// <summary>是否允许「新增下级」节点动作（扁平无层级的树设为 false，前端按钮由本配置驱动）</summary>
    public bool AllowAddChild { get; set; } = true;

    /// <summary>是否允许删除树节点</summary>
    public bool AllowDelete { get; set; } = true;

    /// <summary>是否允许修改树节点名称</summary>
    public bool AllowRename { get; set; } = true;

    /// <summary>根节点父编码值（如 null, '', 'root'）</summary>
    public string? RootParentCode { get; set; }

    /// <summary>树节点名称字段</summary>
    public string NameField { get; set; } = "Name";

    /// <summary>树节点编码字段</summary>
    public string CodeField { get; set; } = "Code";

    /// <summary>树节点父编码字段</summary>
    public string ParentCodeField { get; set; } = "ParentCode";

    /// <summary>关联字段：表格中用于过滤的字段名</summary>
    public string RelateField { get; set; } = string.Empty;

    /// <summary>
    /// 未选中树节点时表格行为
    /// "empty" = 不显示数据（默认）
    /// "all"   = 显示全部数据
    /// </summary>
    public string NoSelectionBehavior { get; set; } = "empty";

    /// <summary>最大层级深度（0=不限）</summary>
    public int MaxLevel { get; set; } = 0;

    /// <summary>是否允许删除含子节点的父节点</summary>
    public bool AllowDeleteWithChildren { get; set; } = false;

    /// <summary>启用/禁用字段名（默认 IsValid，前端根据此字段显示启用/禁用按钮）</summary>
    public string EnableField { get; set; } = "IsValid";

    /// <summary>
    /// 是否显示启用/禁用按钮（true=显示，false=隐藏；null/未设置=沿用 EnableField 非空判断的旧行为）
    /// 当 Controller 使用自定义 disable/enable 操作时，设为 false 避免与基类 toggle-valid 重复。
    /// </summary>
    public bool? AllowToggle { get; set; }
}

/// <summary>左树右表页面配置</summary>
public class TreeTableConfig
{
    /// <summary>表格配置（复用 EntityConfig）</summary>
    public EntityConfig TableConfig { get; set; } = new();

    /// <summary>树配置</summary>
    public TreeConfig TreeConfig { get; set; } = new();

    /// <summary>树节点表单配置（机构/目录等树节点编辑弹窗的字段定义）</summary>
    public EntityConfig? TreeFormConfig { get; set; }
}
