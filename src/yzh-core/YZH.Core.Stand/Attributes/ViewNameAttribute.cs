namespace YZH.Core.Stand.Annotations;

/// <summary>
///     视图名称特性（用于 Entity → 视图 路由）
///     
///     用于标记实体类对应的 MySQL 视图名称。
///     实体只能查询（视图不可更新），增删改走物理表。
///     
///     使用方式：
///     [ViewName("v_org_std_stage_tree")]
///     public class OrgStdStageTree : TreeNodeViewBase { }
///     
///     与 TableNameAttribute 的区别：
///     - TableName: 物理表，支持增删改查
///     - ViewName: 只读视图，仅用于查询
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ViewNameAttribute : Attribute
{
    /// <summary>视图名称</summary>
    public string ViewName { get; }

    public ViewNameAttribute(string viewName)
    {
        ViewName = viewName ?? throw new ArgumentNullException(nameof(viewName));
    }
}
