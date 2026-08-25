using VOL.Core.Utilities;

namespace YZH.Core;

/// <summary>
/// 关联表专用基类（双泛型）。
/// 不走标准 Add/Update/Del，是勾选即保存的关联操作模式。
///
/// 使用：
///   public class OrgStandardLinkService
///       : YZHLinkTableServiceBase&lt;CertificationBody, YzhOrgStandard&gt;
///       , IOrgStandardLinkService, IDependency
///   {
///       public override List&lt;object&gt; GetLinkedItems(string cbCode) { ... }
///       public override WebResponseContent ToggleLink(string cbCode, string standardCode, bool isLinked) { ... }
///   }
/// </summary>
public abstract class YZHLinkTableServiceBase<TMain, TLink>
    where TMain : class
    where TLink : class
{
    /// <summary>查询已关联的项</summary>
    public abstract List<object> GetLinkedItems(string mainCode);

    /// <summary>查询全部可关联项（标记是否已关联）</summary>
    public abstract List<object> GetLinkableItems(string mainCode);

    /// <summary>切换关联状态（勾选即保存）</summary>
    public abstract WebResponseContent ToggleLink(
        string mainCode, string itemCode, bool isLinked);

    /// <summary>批量设置关联</summary>
    public abstract WebResponseContent BatchSetLinks(
        string mainCode, List<string> itemCodes);

    // 钩子
    protected virtual void OnLinkAdded(string mainCode, string itemCode) { }
    protected virtual void OnLinkRemoved(string mainCode, string itemCode) { }
}
