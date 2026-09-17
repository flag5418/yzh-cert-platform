namespace YZH.Core.Stand.Interfaces;

/// <summary>
///     有效标志接口
///     实现此接口的实体支持启用/禁用：IsValid=1 有效，IsValid=0 无效
///     查询时默认自动过滤 IsValid=1 的记录
/// </summary>
public interface IIsValid
{
    /// <summary>有效标志（1=有效，0=无效）</summary>
    int IsValid { get; set; }
}
