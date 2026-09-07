using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YZH.Core.DataBase.NoSql;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     缓存管理器实现
///     封装 INoSql（Redis/MemoryCache），提供高级缓存 API
///     自动读取 YZHCacheEntity 特性配置决定缓存策略
/// </summary>
public class CacheManager : ICacheManager
{
    private readonly INoSql _noSql;
    private readonly ILogger<CacheManager> _logger;
    private readonly CacheManagerOptions _options;

    public CacheManager(INoSql noSql, ILogger<CacheManager> logger, IOptions<CacheManagerOptions>? options = null)
    {
        _noSql = noSql;
        _logger = logger;
        _options = options?.Value ?? new CacheManagerOptions();
    }

    // ==================== 基础缓存操作 ====================

    public T? GetOrAdd<T>(string key, Func<T?> factory, TimeSpan? expiration = null) where T : class
    {
        if (!_options.Enabled)
            return factory();

        var fullKey = BuildFullKey(key);

        // 1. 尝试从缓存获取
        var cacheResult = _noSql.Get<T>(fullKey);
        if (cacheResult.Success && cacheResult.Data != null)
        {
            return cacheResult.Data;
        }

        // 2. 从数据源加载
        var value = factory();
        if (value == null)
            return null;

        // 3. 写入缓存
        var ttl = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);
        if (ttl.TotalMinutes > 0)
            _noSql.Set(fullKey, value, ttl);

        return value;
    }

    public async Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class
    {
        if (!_options.Enabled)
            return await factory();

        var fullKey = BuildFullKey(key);

        var cacheResult = _noSql.Get<T>(fullKey);
        if (cacheResult.Success && cacheResult.Data != null)
            return cacheResult.Data;

        var value = await factory();
        if (value == null)
            return null;

        var ttl = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);
        if (ttl.TotalMinutes > 0)
            _noSql.Set(fullKey, value, ttl);

        return value;
    }

    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        if (!_options.Enabled) return;

        var fullKey = BuildFullKey(key);
        var ttl = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);
        if (ttl.TotalMinutes > 0)
            _noSql.Set(fullKey, value, ttl);
    }

    public void SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, TimeSpan? expiration = null)
    {
        if (!_options.Enabled) return;

        var ttl = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);
        var batch = items.ToDictionary(
            item => BuildFullKey(item.Key),
            item => item.Value);
        _noSql.SetBatch(batch, ttl);
    }

    public void Remove(string key)
    {
        var fullKey = BuildFullKey(key);
        _noSql.Remove(fullKey);
    }

    public void RemoveBatch(IEnumerable<string> keys)
    {
        var fullKeys = keys.Select(BuildFullKey);
        _noSql.RemoveBatch(fullKeys);
    }

    public void RemoveByPrefix(string prefix)
    {
        var fullPrefix = $"{BuildFullKey(prefix)}*";
        var scanResult = _noSql.ScanByKey(fullPrefix);
        if (scanResult.Success && scanResult.Data != null)
        {
            var keys = scanResult.Data.ToList();
            if (keys.Count > 0)
            {
                const int batchSize = 100;
                for (int i = 0; i < keys.Count; i += batchSize)
                {
                    var batch = keys.Skip(i).Take(batchSize);
                    _noSql.RemoveBatch(batch);
                }
                _logger.LogInformation("按前缀删除缓存: {Prefix}, 数量: {Count}", fullPrefix, keys.Count);
            }
        }
    }

    public bool Exists(string key)
    {
        var fullKey = BuildFullKey(key);
        var result = _noSql.Exists(fullKey);
        return result.Success && result.Data;
    }

    public void Clear()
    {
        RemoveByPrefix(string.Empty); // 删除所有 yzh 前缀的 key
    }

    // ==================== 实体缓存便捷方法 ====================

    public T? GetEntity<T>(string id, Func<T?> factory) where T : BaseEntity
    {
        var attr = CacheEntityHelper.GetCacheAttribute<T>();
        if (!attr.Enabled || !ShouldCacheSingle(attr.Mode))
            return factory();

        var key = MakeEntityKey<T>(id);
        var ttl = GetExpiration(attr);
        return GetOrAdd(key, factory, ttl);
    }

    public List<T> GetEntityList<T>(string key, Func<List<T>> factory) where T : BaseEntity
    {
        var attr = CacheEntityHelper.GetCacheAttribute<T>();
        if (!attr.Enabled || !ShouldCacheList(attr.Mode))
            return factory();

        var fullKey = $"{BuildFullKey(CacheEntityHelper.GetCachePrefix<T>())}:list:{key}";
        var ttl = GetExpiration(attr);
        return GetOrAdd(fullKey, factory, ttl) ?? new List<T>();
    }

    public void SetEntity<T>(string id, T entity) where T : BaseEntity
    {
        var attr = CacheEntityHelper.GetCacheAttribute<T>();
        if (!attr.Enabled || !ShouldCacheSingle(attr.Mode))
            return;

        var key = MakeEntityKey<T>(id);
        var ttl = GetExpiration(attr);
        Set(key, entity, ttl);
    }

    public void SetEntityList<T>(string key, List<T> entities) where T : BaseEntity
    {
        var attr = CacheEntityHelper.GetCacheAttribute<T>();
        if (!attr.Enabled || !ShouldCacheList(attr.Mode))
            return;

        var fullKey = $"{BuildFullKey(CacheEntityHelper.GetCachePrefix<T>())}:list:{key}";
        var ttl = GetExpiration(attr);
        Set(fullKey, entities, ttl);
    }

    public void InvalidateEntity<T>() where T : BaseEntity
    {
        var prefix = CacheEntityHelper.GetCachePrefix<T>();
        RemoveByPrefix($"{prefix}:");
    }

    public void InvalidateEntity<T>(string id) where T : BaseEntity
    {
        // 删除单条缓存
        var key = MakeEntityKey<T>(id);
        Remove(key);
        // 同时清除列表缓存
        var prefix = CacheEntityHelper.GetCachePrefix<T>();
        RemoveByPrefix($"{prefix}:list:");
    }

    // ==================== 字典缓存便捷方法 ====================

    public List<DictItem>? GetDict(string dictCode, Func<List<DictItem>> factory)
    {
        return GetOrAdd(MakeDictKey(dictCode), factory, TimeSpan.FromMinutes(_options.DictCacheExpirationMinutes));
    }

    public void SetDict(string dictCode, List<DictItem> items, TimeSpan? expiration = null)
    {
        Set(MakeDictKey(dictCode), items, expiration);
    }

    public void InvalidateDict(string dictCode)
    {
        Remove(MakeDictKey(dictCode));
    }

    // ==================== Key 生成 ====================

    public string MakeEntityKey<T>(string id) where T : BaseEntity
    {
        var prefix = CacheEntityHelper.GetCachePrefix<T>();
        return $"{BuildFullKey(prefix)}:entity:{id}";
    }

    public string MakeDictKey(string dictCode)
    {
        return $"{BuildFullKey(_options.Prefix)}:dict:{dictCode}";
    }

    // ==================== 私有方法 ====================

    private string BuildFullKey(string key)
    {
        return $"{_options.Prefix}:{key}";
    }

    private TimeSpan? GetExpiration(YZHCacheEntityAttribute attr)
    {
        var minutes = attr.ExpirationMinutes > 0
            ? attr.ExpirationMinutes
            : _options.EntityCacheExpirationMinutes;
        return minutes > 0 ? TimeSpan.FromMinutes(minutes) : null;
    }

    private static bool ShouldCacheSingle(CacheMode mode) => mode is CacheMode.Single or CacheMode.All;
    private static bool ShouldCacheList(CacheMode mode) => mode is CacheMode.ListOnly or CacheMode.All;
}
