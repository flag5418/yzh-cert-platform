namespace YZH.Core.Stand.Interfaces;

/// <summary>
///     软删除接口（实现此接口的实体支持软删除）
///
///     完整软删除字段（实现类必须全部包含）：
///     - IsDeleted   是否已删除（false=正常，true=已删除）
///     - DeleteBy     删除人 Code（删除时自动填充）
///     - DeleteTime   删除时间（删除时自动填充）
///
///     框架行为：
///     - 查询时自动过滤 IsDeleted=true 的记录
///     - 执行软删除时，自动填充 IsDeleted=true + DeleteBy + DeleteTime
///     - 未实现此接口的实体执行 DeleteCore 时走硬删除
/// </summary>
public interface ISoftDelete
{
    /// <summary>是否已删除（false=正常，true=已删除）</summary>
    bool IsDeleted { get; set; }

    /// <summary>删除人 Code（仅软删除时赋值，存储用户业务编码）</summary>
    string? DeleteBy { get; set; }

    /// <summary>删除时间（仅软删除时赋值）</summary>
    DateTime? DeleteTime { get; set; }
}
