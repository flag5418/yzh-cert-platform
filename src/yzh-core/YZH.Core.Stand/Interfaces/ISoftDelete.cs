namespace YZH.Core.Stand.Interfaces;

/// <summary>
///     软删除标记接口
///     实现此接口的实体在查询时自动过滤 IsDeleted=true 的记录
/// </summary>
public interface ISoftDelete
{
    /// <summary>是否已删除（软删除标志）</summary>
    bool IsDeleted { get; set; }
}

/// <summary>
///     有效标志接口
///     实现此接口的实体在查询时自动过滤 IsValid=0 的记录
/// </summary>
public interface IValidFlag
{
    /// <summary>是否有效（1=有效，0=无效）</summary>
    bool IsValid { get; set; }
}
