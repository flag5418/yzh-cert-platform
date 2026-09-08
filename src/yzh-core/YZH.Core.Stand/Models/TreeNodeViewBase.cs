using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace YZH.Core.Stand.Models;

/// <summary>
///     视图实体基类（只用于树查询）
///     
///     特点：
///     1. 只读（视图通常不可更新）
///     2. 包含树结构标准字段（Code/Name/ParentCode/NodeType/IsLeaf/Sort）
///     3. [NotMapped] 标记 isLeaf（运行时计算，不持久化）
///     
///     使用方式：
///     [ViewName("v_org_std_stage_tree")]
///     public class OrgStdStageTree : TreeNodeViewBase
///     {
///         public string? ExtraOrgType { get; set; }
///     }
///     
///     视图 SQL 约定字段（必须包含）：
///     - code: 业务编码
///     - name: 显示名称
///     - parent_code: 父节点编码（snake_case）
///     - node_type: 节点类型（可选）
///     - sort: 排序号（可选）
/// </summary>
public abstract class TreeNodeViewBase : ITreeEntity
{
    /// <summary>业务编码（唯一标识）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>父节点编码（根节点为 null）</summary>
    public string? ParentCode { get; set; }

    /// <summary>节点类型（区分来源表，如 org/standard/stage/department）</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>是否叶子节点（运行时批量计算，不持久化）</summary>
    [NotMapped]
    [JsonIgnore]
    public bool? IsLeaf { get; set; }

    /// <summary>排序号（可选）</summary>
    public int? Sort { get; set; }

    /// <summary>层级深度（可选，视图计算列或运行时计算）</summary>
    [NotMapped]
    [JsonIgnore]
    public int Level { get; set; }
}
