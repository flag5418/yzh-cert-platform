namespace YZH.Core.Stand.Models.Config;

/// <summary>
/// 树形表格选择器 - 统一请求/响应模型
/// 适用场景：左侧选树节点，右侧勾选关联实体（角色-用户、角色-菜单等）
/// </summary>

/// <summary>混合树节点 DTO（前端 el-table tree 模式使用）</summary>
public class CheckTreeNodeDto
{
    /// <summary>唯一编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>父编码（用于前端构建嵌套树）</summary>
    public string? ParentCode { get; set; }

    /// <summary>节点类型（如 "org" / "user" / "menu"，由业务定义）</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>是否已勾选（当前左侧节点的关联状态）</summary>
    public bool CheckFlag { get; set; }

    /// <summary>扩展业务字段</summary>
    public Dictionary<string, object>? Extra { get; set; }
}

/// <summary>树查询请求（左侧切换节点时调用）</summary>
public class CheckTreeRequest
{
    /// <summary>左侧选中节点编码（如角色编码）</summary>
    public string ContextCode { get; set; } = string.Empty;
}

/// <summary>勾选操作请求（右侧勾选/取消时调用）</summary>
public class CheckActionRequest
{
    /// <summary>左侧选中节点编码</summary>
    public string ContextCode { get; set; } = string.Empty;

    /// <summary>右侧勾选/取消的节点集合</summary>
    public List<TreeNodeSelection> Selections { get; set; } = new();
}

/// <summary>节点选择项</summary>
public class TreeNodeSelection
{
    /// <summary>节点编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>节点类型（区分机构/用户/菜单等）</summary>
    public string NodeType { get; set; } = string.Empty;
}

/// <summary>关联关系 DTO（用于批量获取所有关联状态，前端本地缓存）</summary>
public class AssociationDto
{
    /// <summary>左侧上下文编码（如角色编码）</summary>
    public string ContextCode { get; set; } = string.Empty;

    /// <summary>右侧关联节点编码（如用户编码）</summary>
    public string TargetCode { get; set; } = string.Empty;

    /// <summary>右侧节点类型</summary>
    public string NodeType { get; set; } = string.Empty;
}
