using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace YZH.Core.DataBase.NoSql;

/// <summary>
///     内存缓存 NoSQL 提供程序（基于 IMemoryCache）
///     用于开发环境或单机部署场景，支持 TTL
/// </summary>
public class MemoryCacheProvider : INoSql, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheProvider>? _logger;
    private readonly NoSqlOptions _options;
    private string _prefix;
    private readonly ConcurrentDictionary<string, DateTime> _expires = new();

    public string ProviderName => "MemoryCache";

    public MemoryCacheProvider(NoSqlOptions options, ILogger<MemoryCacheProvider>? logger = null)
    {
        _options = options;
        _logger = logger;
        _prefix = options.DefaultPrefix;

        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 10240, // 最多 10240 个条目
            CompactionPercentage = 0.25,
            ExpirationScanFrequency = TimeSpan.FromMinutes(5)
        };

        _cache = new MemoryCache(cacheOptions);
    }

    public MemoryCacheProvider(string prefix = "yzh", ILogger<MemoryCacheProvider>? logger = null)
    {
        _logger = logger;
        _prefix = prefix;
        _options = new NoSqlOptions { DefaultPrefix = prefix };

        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    // ==================== 基础 CRUD ====================

    public NoSqlOperationResult Set<T>(string key, T value)
    {
        return SetInternal(key, value, null);
    }

    public NoSqlOperationResult Set<T>(string key, T value, TimeSpan absoluteExpiration)
    {
        return SetInternal(key, value, absoluteExpiration);
    }

    public NoSqlOperationResult Set<T>(string key, T value, TimeSpan absoluteExpiration, TimeSpan slidingExpiration)
    {
        try
        {
            var fullKey = BuildKey(key);
            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration,
                SlidingExpiration = slidingExpiration,
                Size = 1
            };

            var json = JsonSerializer.Serialize(value);
            _cache.Set(fullKey, json, entryOptions);
            _expires[fullKey] = DateTime.UtcNow.Add(absoluteExpiration);

            return NoSqlOperationResult.Ok(1);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MemoryCache Set 失败: {Key}", key);
            return NoSqlOperationResult.Fail($"写入失败: {ex.Message}", ex);
        }
    }

    public NoSqlQueryResult<T> Get<T>(string key)
    {
        try
        {
            var fullKey = BuildKey(key);

            if (!_cache.TryGetValue(fullKey, out string? json) || json == null)
                return NoSqlQueryResult<T>.Fail("Key not found");

            var value = JsonSerializer.Deserialize<T>(json);

            _expires.TryGetValue(fullKey, out var expiresAt);
            var remainingTtl = expiresAt != default ? (long)(expiresAt - DateTime.UtcNow).TotalSeconds : (long?)null;

            return NoSqlQueryResult<T>.Ok(value!, remainingTtl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MemoryCache Get 失败: {Key}", key);
            return NoSqlQueryResult<T>.Fail($"获取失败: {ex.Message}");
        }
    }

    public NoSqlOperationResult Remove(string key)
    {
        try
        {
            _cache.Remove(BuildKey(key));
            _expires.TryRemove(BuildKey(key), out _);
            return NoSqlOperationResult.Ok(1);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MemoryCache Remove 失败: {Key}", key);
            return NoSqlOperationResult.Fail($"删除失败: {ex.Message}", ex);
        }
    }

    public NoSqlOperationResult<bool> Exists(string key)
    {
        try
        {
            var exists = _cache.TryGetValue(BuildKey(key), out _);
            return NoSqlOperationResult<bool>.Ok(exists);
        }
        catch (Exception ex)
        {
            return NoSqlOperationResult<bool>.Fail($"检查失败: {ex.Message}", ex);
        }
    }

    public NoSqlQueryResult<long> GetTtl(string key)
    {
        try
        {
            if (_expires.TryGetValue(BuildKey(key), out var expiresAt))
            {
                var remaining = (long)(expiresAt - DateTime.UtcNow).TotalSeconds;
                return NoSqlQueryResult<long>.Ok(Math.Max(0, remaining));
            }

            return NoSqlQueryResult<long>.Ok(-2); // 不存在或永不过期
        }
        catch (Exception ex)
        {
            return NoSqlQueryResult<long>.Fail($"获取TTL失败: {ex.Message}");
        }
    }

    public NoSqlOperationResult Expire(string key, TimeSpan expiration)
    {
        try
        {
            var fullKey = BuildKey(key);
            _expires[fullKey] = DateTime.UtcNow.Add(expiration);

            if (_cache.TryGetValue(fullKey, out string? value) && value != null)
            {
                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration,
                    Size = 1
                };
                _cache.Set(fullKey, value, entryOptions);
            }

            return NoSqlOperationResult.Ok(1);
        }
        catch (Exception ex)
        {
            return NoSqlOperationResult.Fail($"设置过期失败: {ex.Message}", ex);
        }
    }

    public NoSqlOperationResult Persist(string key)
    {
        try
        {
            _expires.TryRemove(BuildKey(key), out _);
            return NoSqlOperationResult.Ok(1);
        }
        catch (Exception ex)
        {
            return NoSqlOperationResult.Fail($"持久化失败: {ex.Message}", ex);
        }
    }

    // ==================== 批量操作 ====================

    public NoSqlOperationResult SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, TimeSpan? absoluteExpiration = null)
    {
        try
        {
            var count = 0;
            foreach (var (key, value) in items)
            {
                var result = Set(key, value, absoluteExpiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes));
                if (result.Success)
                    count++;
            }

            return NoSqlOperationResult.Ok(count);
        }
        catch (Exception ex)
        {
            return NoSqlOperationResult.Fail($"批量写入失败: {ex.Message}", ex);
        }
    }

    public NoSqlQueryResult<IDictionary<string, T>> GetBatch<T>(IEnumerable<string> keys)
    {
        try
        {
            var result = new Dictionary<string, T>();
            foreach (var key in keys)
            {
                var getResult = Get<T>(key);
                if (getResult.Success && getResult.Data != null)
                    result[key] = getResult.Data;
            }

            return NoSqlQueryResult<IDictionary<string, T>>.Ok(result);
        }
        catch (Exception ex)
        {
            return NoSqlQueryResult<IDictionary<string, T>>.Fail($"批量获取失败: {ex.Message}");
        }
    }

    public NoSqlOperationResult RemoveBatch(IEnumerable<string> keys)
    {
        try
        {
            var count = 0;
            foreach (var key in keys)
            {
                var result = Remove(key);
                if (result.Success)
                    count++;
            }

            return NoSqlOperationResult.Ok(count);
        }
        catch (Exception ex)
        {
            return NoSqlOperationResult.Fail($"批量删除失败: {ex.Message}", ex);
        }
    }

    // ==================== 扫描 ====================

    public NoSqlQueryResult<IEnumerable<string>> ScanByKey(string pattern, int count = 100)
    {
        try
        {
            var prefix = BuildKey(pattern).Replace("*", "").Replace("?", "");
            var matchingKeys = _expires.Keys
                .Where(k => k.StartsWith(prefix))
                .Take(count);

            return NoSqlQueryResult<IEnumerable<string>>.Ok(matchingKeys);
        }
        catch (Exception ex)
        {
            return NoSqlQueryResult<IEnumerable<string>>.Fail($"扫描失败: {ex.Message}");
        }
    }

    // ==================== 命名空间 ====================

    public string BuildKey(string key)
    {
        return string.IsNullOrEmpty(_prefix) ? key : $"{_prefix}:{key}";
    }

    public void SetPrefix(string prefix)
    {
        _prefix = prefix;
    }

    // ==================== 健康检查 ====================

    public NoSqlOperationResult HealthCheck()
    {
        // 内存缓存总是健康的
        return NoSqlOperationResult.Ok();
    }

    // ==================== 自动清除 ====================

    public NoSqlOperationResult Cleanup(string pattern, int batchSize = 100)
    {
        try
        {
            var prefix = BuildKey(pattern).Replace("*", "").Replace("?", "");
            var keysToRemove = _expires.Keys
                .Where(k => k.StartsWith(prefix))
                .Take(batchSize)
                .ToList();

            var count = 0;
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _expires.TryRemove(key, out _);
                count++;
            }

            return NoSqlOperationResult.Ok(count);
        }
        catch (Exception ex)
        {
            return NoSqlOperationResult.Fail($"清理失败: {ex.Message}", ex);
        }
    }

    // ==================== 私有方法 ====================

    private NoSqlOperationResult SetInternal<T>(string key, T value, TimeSpan? expiration)
    {
        try
        {
            var fullKey = BuildKey(key);
            var entryOptions = new MemoryCacheEntryOptions { Size = 1 };

            if (expiration.HasValue)
            {
                entryOptions.AbsoluteExpirationRelativeToNow = expiration.Value;
                _expires[fullKey] = DateTime.UtcNow.Add(expiration.Value);
            }

            var json = JsonSerializer.Serialize(value);
            _cache.Set(fullKey, json, entryOptions);

            return NoSqlOperationResult.Ok(1);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MemoryCache Set 失败: {Key}", key);
            return NoSqlOperationResult.Fail($"写入失败: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
