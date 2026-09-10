using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using YZH.Core.Stand.Helpers;

namespace YZH.Core.Stand.NoSql;

/// <summary>
///     基于 IMemoryCache 的 INoSql 实现（开发环境 / 单机部署）
///     生产环境可替换为 Redis 实现
/// </summary>
public class MemoryCacheNoSql : INoSql
{
    private readonly IMemoryCache _cache;
    private static readonly MemoryCacheEntryOptions DefaultOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };

    // 用于扫描 key：维护一个 key 注册表（ScanByKey 用）
    private static readonly HashSet<string> StringKeyRegistry = new();
    private static readonly object RegistryLock = new();

    public MemoryCacheNoSql(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public T? Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var value) && value is T typed)
            return typed;

        return default;
    }

    /// <inheritdoc />
    public bool Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var options = expiration.HasValue
            ? new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiration.Value)
            : DefaultOptions;

        // 注册 key（用于 ScanByKey）
        lock (RegistryLock)
        {
            StringKeyRegistry.Add(key);
        }

        _cache.Set(key, value, options);
        return true;
    }

    /// <inheritdoc />
    public bool SetBatch<T>(Dictionary<string, T> items, TimeSpan? expiration = null)
    {
        foreach (var (key, value) in items)
        {
            Set(key, value, expiration);
        }
        return true;
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        _cache.Remove(key);
        lock (RegistryLock)
        {
            StringKeyRegistry.Remove(key);
        }
        return true;
    }

    /// <inheritdoc />
    public bool RemoveBatch(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            Remove(key);
        }
        return true;
    }

    /// <inheritdoc />
    public bool Exists(string key)
    {
        return _cache.TryGetValue(key, out _);
    }

    /// <inheritdoc />
    public IEnumerable<string> ScanByKey(string pattern)
    {
        // 简单的前缀匹配（将 * 前缀转换为 StartsWith）
        var prefix = pattern.TrimEnd('*');

        lock (RegistryLock)
        {
            if (string.IsNullOrEmpty(prefix))
                return StringKeyRegistry.ToList();

            return StringKeyRegistry
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
