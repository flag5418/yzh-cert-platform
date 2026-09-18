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
