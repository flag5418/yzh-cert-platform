namespace YZH.Core.Stand.Models.Config;

/// <summary>树懒加载请求</summary>
public class TreeChildrenRequest
{
    public string ParentCode { get; set; } = string.Empty;
    public int Level { get; set; }
}

/// <summary>树节点 DTO（前后端统一标准）</summary>
public class TreeItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public string? NodeType { get; set; }
    public bool IsLeaf { get; set; }
    public int Level { get; set; }
    public int? Sort { get; set; }

    /// <summary>扩展业务字段（enable、remark 等通过此字典传递）</summary>
    public Dictionary<string, object>? Extra { get; set; }

    /// <summary>子节点（全量加载场景）</summary>
    public List<TreeItemDto>? Children { get; set; }
}

/// <summary>树配置</summary>
public class TreeConfig
{
    /// <summary>是否懒加载（true=点击展开时加载子节点）</summary>
    public bool Lazy { get; set; } = true;

    /// <summary>是否允许编辑树节点（true=显示添加/修改/删除按钮）</summary>
    public bool AllowEdit { get; set; } = true;

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

/// <summary>搜索条件配置</summary>
public class SearchFieldConfig
{
    /// <summary>显示名称</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>字段名（对应实体属性）</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>查询类型：like/eq/gt/lt/in</summary>
    public string Operator { get; set; } = "like";

    /// <summary>控件类型：input/select/date/cascader</summary>
    public string ControlType { get; set; } = "input";

    /// <summary>下拉选项（ControlType=select时有效）</summary>
    public List<SelectOption>? Options { get; set; }

    /// <summary>宽度（px）</summary>
    public int Width { get; set; } = 180;
}
