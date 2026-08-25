namespace YZH.Core.Attributes;

/// <summary>
/// 声明左树右表场景的树数据来源。
/// 标记在右表实体上，告知基类如何自动加载左树数据 + 如何联动过滤。
///
/// 示例：
///   [TreeSource("CertCertificationBody", "CbCode", "Code", "Name")]
///   public class ISOStandard : YZHBaseEntity { ... }
///   // 含义：左树从认证机构表加载，右表按 CbCode 过滤
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class TreeSourceAttribute : Attribute
{
    /// <summary>树数据来源的 Controller 名称（如 "CertCertificationBody"）</summary>
    public string TreeController { get; }

    /// <summary>右表过滤字段名（如 "CbCode"）</summary>
    public string FilterField { get; }

    /// <summary>树节点 Key 字段（默认 "Code"）</summary>
    public string TreeKeyField { get; }

    /// <summary>树节点显示字段（默认 "Name"）</summary>
    public string TreeLabelField { get; }

    public TreeSourceAttribute(
        string treeController,
        string filterField,
        string treeKeyField = "Code",
        string treeLabelField = "Name")
    {
        TreeController = treeController;
        FilterField = filterField;
        TreeKeyField = treeKeyField;
        TreeLabelField = treeLabelField;
    }
}
