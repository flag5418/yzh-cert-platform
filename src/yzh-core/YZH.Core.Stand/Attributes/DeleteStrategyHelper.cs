using System.Reflection;

namespace YZH.Core.Stand.Attributes;

/// <summary>
///     删除策略工具
///     根据实体类型获取删除策略配置
/// </summary>
public static class DeleteStrategyHelper
{
    /// <summary>获取实体类型的删除策略</summary>
    public static YZHDeleteStrategyAttribute GetDeleteStrategy<T>() where T : class
    {
        return GetDeleteStrategy(typeof(T));
    }

    /// <summary>获取实体类型的删除策略</summary>
    public static YZHDeleteStrategyAttribute GetDeleteStrategy(Type entityType)
    {
        var attr = entityType.GetCustomAttribute<YZHDeleteStrategyAttribute>();
        return attr ?? new YZHDeleteStrategyAttribute(); // 默认软删除
    }

    /// <summary>判断是否为软删除</summary>
    public static bool IsSoftDelete<T>() where T : class
    {
        return GetDeleteStrategy<T>().Mode == DeleteMode.Soft;
    }

    /// <summary>判断是否为硬删除</summary>
    public static bool IsHardDelete<T>() where T : class
    {
        return GetDeleteStrategy<T>().Mode == DeleteMode.Hard;
    }

    /// <summary>判断是否为软删除</summary>
    public static bool IsSoftDelete(Type entityType)
    {
        return GetDeleteStrategy(entityType).Mode == DeleteMode.Soft;
    }

    /// <summary>判断是否为硬删除</summary>
    public static bool IsHardDelete(Type entityType)
    {
        return GetDeleteStrategy(entityType).Mode == DeleteMode.Hard;
    }
}
