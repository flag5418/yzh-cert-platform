namespace YZH.Core.Stand.Attributes;

/// <summary>
///     实体缓存特性
///     标记在 BaseEntity 子类上，控制该实体的缓存行为
///     
///     默认不缓存，需显式启用
///     
/// 使用示例：
/// [YZHCacheEntity(Enabled = true, ExpirationMinutes = 60)]
/// public class ISOStandard : BaseEntity { }
/// 
/// 无特性时不缓存（默认）
/// public class AuditLog : BaseEntity { }
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class YZHCacheEntityAttribute : Attribute
{
    /// <summary>是否启用缓存（默认 false，需显式启用）</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>缓存过期时间（分钟），0=使用默认值</summary>
    public int ExpirationMinutes { get; set; } = 0;

    /// <summary>
    ///     缓存级别
    ///     None: 不缓存
    ///     Local: 仅本地内存缓存
    ///     Distributed: 分布式缓存（Redis）
    ///     Both: 双层缓存（本地 + 分布式）
    /// </summary>
    public CacheLevel Level { get; set; } = CacheLevel.Distributed;

    /// <summary>缓存 key 前缀（为空则使用类名）</summary>
    public string? Prefix { get; set; }

    /// <summary>是否在写操作后自动清除缓存（默认 true）</summary>
    public bool AutoInvalidate { get; set; } = true;

    /// <summary>
    ///     缓存模式
    ///     All: 缓存分页列表 + 单条记录
    ///     Single: 仅缓存单条记录（ById, GetByCode）
    ///     ListOnly: 仅缓存列表（全量查询）
    /// </summary>
    public CacheMode Mode { get; set; } = CacheMode.All;
}

/// <summary>
///     缓存级别
/// </summary>
public enum CacheLevel
{
    /// <summary>不缓存</summary>
    None = 0,

    /// <summary>仅本地内存</summary>
    Local = 1,

    /// <summary>分布式（Redis）</summary>
    Distributed = 2,

    /// <summary>双层缓存</summary>
    Both = 3
}

/// <summary>
///     缓存模式
/// </summary>
public enum CacheMode
{
    /// <summary>仅缓存单条</summary>
    Single = 0,

    /// <summary>仅缓存列表</summary>
    ListOnly = 1,

    /// <summary>全部缓存</summary>
    All = 2
}
