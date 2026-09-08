namespace YZH.Core.Stand.Models;

/// <summary>
///     树节点接口（后端）
///     
///     用于约束树形结构实体的最小字段集。
///     实现此接口的实体可被 TreeControllerBase 直接使用。
///     
///     注意：
///     - Code 属性已由 BaseEntity 或 TreeNodeViewBase 提供
///     - 实现类只需确保 ParentCode 和 IsLeaf 属性存在
///     
///     实体实现示例：
///     // 方式1：继承 TreeNodeViewBase（推荐，用于视图实体）
///     [ViewName("v_org_std_stage_tree")]
///     public class OrgStdStageTree : TreeNodeViewBase { }
///     
///     // 方式2：继承 BaseEntity（用于物理表实体）
///     public class Sys_Department : BaseEntity, ITreeNode
///     {
///         [Column("parent_code")]
///         public string? ParentCode { get; set; }
///         
///         [NotMapped]
///         public bool? IsLeaf { get; set; }
///     }
/// </summary>
public interface ITreeNode
{
    /// <summary>业务编码（唯一标识，已由基类提供）</summary>
    string Code { get; }

    /// <summary>父节点编码（根节点为空或 null）</summary>
    string? ParentCode { get; set; }

    /// <summary>是否叶子节点（后端批量计算，非持久化字段）</summary>
    bool? IsLeaf { get; set; }
}

/// <summary>
///     树实体接口（TreeTableControllerBase 约束用）
///     
///     与 ITreeNode 语义相同，用于架构约束声明。
///     实现类：继承 BaseEntity 的实体，或继承 TreeNodeViewBase 的视图实体。
/// </summary>
public interface ITreeEntity
{
    /// <summary>业务编码（唯一标识）</summary>
    string Code { get; set; }

    /// <summary>父节点编码（根节点为空或 null）</summary>
    string? ParentCode { get; set; }

    /// <summary>是否叶子节点（后端批量计算）</summary>
    bool? IsLeaf { get; set; }
}
