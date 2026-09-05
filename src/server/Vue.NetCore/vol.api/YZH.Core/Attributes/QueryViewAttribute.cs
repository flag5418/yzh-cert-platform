namespace YZH.Core.Attributes;

/// <summary>
/// 声明实体对应的查询视图类型。
/// 标记在实体上，TableServiceBase.GetPageData 会自动查询视图类型。
///
/// 示例：
///   [Entity(TableName = "cert_iso_standard")]
///   [QueryView(typeof(ISOStandardView))]
///   public class ISOStandard : EntityBase { ... }
///
///   [NotMapped]
///   public class ISOStandardView : ISOStandard
///   {
///       public string CategoryName { get; set; }  // 字典翻译
///   }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class QueryViewAttribute : Attribute
{
    public Type ViewType { get; }
    public QueryViewAttribute(Type viewType) => ViewType = viewType;
}
