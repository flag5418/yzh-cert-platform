namespace YZH.Core.Stand.Interfaces;

/// <summary>
///     软删除标记接口
///     实现此接口的实体支持软删除：IsDeleted=true 标记删除，查询时自动过滤
/// </summary>
public interface ISoftDelete
{
    /// <summary>是否已删除（false=正常，true=已删除）</summary>
    bool IsDeleted { get; set; }
}
