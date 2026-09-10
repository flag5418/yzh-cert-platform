namespace YZH.Core.Stand.Models;

/// <summary>
///     有效标志接口（后端）
///
///     用于约束实体的 IsValid 字段。
///     实现此接口的实体在查询时自动过滤 IsValid == 1。
///     toggle-valid 操作切换 IsValid 值（0 ↔ 1）。
///
///     约定：
///     - 1 = 有效（默认）
///     - 0 = 无效
/// </summary>
public interface IIsValid
{
    /// <summary>有效标志（1=有效，0=无效）</summary>
    int IsValid { get; set; }
}
