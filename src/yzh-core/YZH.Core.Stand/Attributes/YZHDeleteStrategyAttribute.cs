namespace YZH.Core.Stand.Attributes;

/// <summary>
///     删除策略特性
///     标记在 BaseEntity 子类上，控制该实体的删除行为
///     
/// 使用示例：
/// [YZHDeleteStrategy(DeleteMode.Soft)]  // 软删除（默认，设置 IsDeleted=1）
/// public class ISOStandard : BaseEntity { }
/// 
/// [YZHDeleteStrategy(DeleteMode.Hard)]  // 硬删除（直接从数据库删除）
/// public class TempData : BaseEntity { }
/// 
/// 无特性时默认软删除
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class YZHDeleteStrategyAttribute : Attribute
{
    /// <summary>删除模式（默认 Soft）</summary>
    public DeleteMode Mode { get; set; } = DeleteMode.Soft;

    /// <summary>软删除时使用的字段名（默认 IsDeleted）</summary>
    public string DeleteFlagField { get; set; } = "IsDeleted";

    /// <summary>软删除时间字段名（默认 DeleteTime）</summary>
    public string DeleteTimeField { get; set; } = "DeleteTime";

    /// <summary>软删除人字段名（默认 DeleteBy）</summary>
    public string DeleteByField { get; set; } = "DeleteBy";
}

/// <summary>
///     删除模式
/// </summary>
public enum DeleteMode
{
    /// <summary>软删除：设置删除标记，不移除记录</summary>
    Soft = 0,

    /// <summary>硬删除：直接从数据库移除记录</summary>
    Hard = 1
}
