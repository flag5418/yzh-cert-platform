namespace YZH.Core.Stand.Models.Tree;

/// <summary>
///     树形节点视图基类
///     用于前端展示树形结构（组织架构、菜单树等）
/// </summary>
public class TreeNodeViewBase
{
    /// <summary>节点 ID</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>节点 Code（业务编码）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>节点名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>父节点 ID</summary>
    public string ParentId { get; set; } = string.Empty;

    /// <summary>父节点 Code</summary>
    public string ParentCode { get; set; } = string.Empty;

    /// <summary>节点层级（1=根）</summary>
    public int Level { get; set; } = 1;

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>是否展开</summary>
    public bool Expanded { get; set; }

    /// <summary>是否有子节点</summary>
    public bool HasChildren { get; set; }

    /// <summary>是否叶子节点（无子节点）</summary>
    public bool IsLeaf { get; set; }

    /// <summary>子节点列表</summary>
    public List<TreeNodeViewBase>? Children { get; set; }

    /// <summary>节点图标</summary>
    public string? Icon { get; set; }

    /// <summary>节点禁用状态</summary>
    public bool Disabled { get; set; }
}
