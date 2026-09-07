using System.Reflection;

namespace YZH.Core.Stand.Attributes;

/// <summary>
///     缓存特性读取工具
/// </summary>
public static class CacheEntityHelper
{
    /// <summary>获取实体类型的缓存特性配置</summary>
    public static YZHCacheEntityAttribute GetCacheAttribute<T>() where T : class
    {
        return GetCacheAttribute(typeof(T));
    }

    /// <summary>获取实体类型的缓存特性配置</summary>
    public static YZHCacheEntityAttribute GetCacheAttribute(Type entityType)
    {
        var attr = entityType.GetCustomAttribute<YZHCacheEntityAttribute>();
        return attr ?? new YZHCacheEntityAttribute(); // 默认启用缓存
    }

    /// <summary>判断实体是否启用缓存</summary>
    public static bool IsCacheEnabled<T>() where T : class
    {
        return GetCacheAttribute<T>().Enabled;
    }

    /// <summary>判断实体是否启用缓存</summary>
    public static bool IsCacheEnabled(Type entityType)
    {
        return GetCacheAttribute(entityType).Enabled;
    }

    /// <summary>获取缓存 key 前缀</summary>
    public static string GetCachePrefix<T>() where T : class
    {
        var attr = GetCacheAttribute<T>();
        return attr.Prefix ?? typeof(T).Name;
    }

    /// <summary>获取缓存 key 前缀</summary>
    public static string GetCachePrefix(Type entityType)
    {
        var attr = GetCacheAttribute(entityType);
        return attr.Prefix ?? entityType.Name;
    }
}
