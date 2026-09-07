using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace YZH.Core.DataBase.NoSql;

/// <summary>
///     Redis NoSQL 提供程序（基于 StackExchange.Redis）
///     完整实现 INoSql 接口，支持 TTL、命名空间、健康检查
/// </summary>
public class RedisNoSqlProvider : INoSql, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<RedisNoSqlProvider>? _logger;
    private readonly NoSqlOptions _options;
    private string _prefix;

    public string ProviderName => "Redis";

    public RedisNoSqlProvider(NoSqlOptions options, ILogger<RedisNoSqlProvider>? logger = null)
    {
        _options = options;
        _logger = logger;
        _prefix = options.DefaultPrefix;

        var configOptions = new ConfigurationOptions
        {
            EndPoints = { options.ConnectionString },
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AbortOnConnectFail = false,
            ReconnectRetryPolicy = new ExponentialRetry(5000),
            DefaultDatabase = 0
        };

        _redis = ConnectionMultiplexer.Connect(configOptions);
        _db = _redis.GetDatabase();
    }

    public RedisNoSqlProvider(string connectionString, string prefix = "yzh", ILogger<RedisNoSqlProvider>? logger = null)
    {
        _logger = logger;
        _prefix = prefix;
        _options = new NoSqlOptions
        {
            ConnectionString = connectionString,
            DefaultPrefix = prefix
        };

        var configOptions = new ConfigurationOptions
        {
            EndPoints = { connectionString },
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            AbortOnConnectFail = false
        };

        _redis = ConnectionMultiplexer.Connect(configOptions);
        _db = _redis.GetDatabase();
    }

    // ==================== 基础 CRUD ====================

    public NoSqlOperationResult Set<T>(string key, T value)
    {
        return SetInternal(BuildKey(key), value, null, null);
    }

    public NoSqlOperationResult Set<T>(string key, T value, TimeSpan absoluteExpiration)
    {
        return SetInternal(BuildKey(key), value, absoluteExpiration, null);
    }

    public NoSqlOperationResult Set<T>(string key, T value, TimeSpan absoluteExpiration, TimeSpan slidingExpiration)
    {
        return SetInternal(BuildKey(key), value, absoluteExpiration, slidingExpiration);
    }

    public NoSqlQueryResult<T> Get<T>(string key)
    {
        try
        {
            var fullKey = BuildKey(key);
            var redisValue = _db.StringGet(fullKey);

            if (redisValue.IsNullOrEmpty)
                return NoSqlQueryResult<T>.Fail("Key not found");

            var json = redisValue.ToString();
            var value = JsonSerializer.Deserialize<T>(json);

            // 获取剩余 TTL
            var ttl = _db.KeyTimeToLive(fullKey);
            var remainingTtl = ttl.HasValue ? (long)ttl.Value.TotalSeconds : (long?)null;

            return NoSqlQueryResult<T>.Ok(value!, remainingTtl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Get 失败: {Key}", key);
            return NoSqlQueryResult<T>.Fail($"获取失败: {ex.Message}");
        }
    }

    public NoSqlOperationResult Remove(string key)
    {
        try
        {
            var result = _db.KeyDelete(BuildKey(key));
            return NoSqlOperationResult.Ok(result ? 1 : 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Remove 失败: {Key}", key);
            return NoSqlOperationResult.Fail($"删除失败: {ex.Message}", ex);
        }
    }

    public NoSqlOperationResult<bool> Exists(string key)
    {
        try
        {
            var result = _db.KeyExists(BuildKey(key));
            return NoSqlOperationResult<bool>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Exists 失败: {Key}", key);
            return NoSqlOperationResult<bool>.Fail($"检查失败: {ex.Message}", ex);
        }
    }

    public NoSqlQueryResult<long> GetTtl(string key)
    {
        try
        {
            var ttl = _db.KeyTimeToLive(BuildKey(key));
            if (!ttl.HasValue)
                return NoSqlQueryResult<long>.Ok(-2); // key 不存在或永不过期

            return NoSqlQueryResult<long>.Ok((long)ttl.Value.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis GetTtl 失败: {Key}", key);
            return NoSqlQueryResult<long>.Fail($"获取TTL失败: {ex.Message}");
        }
    }

    public NoSqlOperationResult Expire(string key, TimeSpan expiration)
    {
        try
        {
            var result = _db.KeyExpire(BuildKey(key), expiration);
            return NoSqlOperationResult.Ok(result ? 1 : 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Expire 失败: {Key}", key);
            return NoSqlOperationResult.Fail($"设置过期失败: {ex.Message}", ex);
        }
    }

    public NoSqlOperationResult Persist(string key)
    {
        try
        {
            var result = _db.KeyPersist(BuildKey(key));
            return NoSqlOperationResult.Ok(result ? 1 : 0);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Persist 失败: {Key}", key);
            return NoSqlOperationResult.Fail($"持久化失败: {ex.Message}", ex);
        }
    }

    // ==================== 批量操作 ====================

    public NoSqlOperationResult SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, TimeSpan? absoluteExpiration = null)
    {
        try
        {
            var batch = _db.CreateBatch();
            var tasks = new List<Task>();

            foreach (var (key, value) in items)
            {
                var json = JsonSerializer.Serialize(value);
                var fullKey = BuildKey(key);
                if (absoluteExpiration.HasValue)
                    tasks.Add(batch.StringSetAsync(fullKey, json, absoluteExpiration.Value));
                else
                    tasks.Add(batch.StringSetAsync(fullKey, json));
            }

            batch.Execute();
            Task.WaitAll(tasks.ToArray());

            return NoSqlOperationResult.Ok(tasks.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis SetBatch 失败: {Message}", ex.Message);
            return NoSqlOperationResult.Fail($"批量写入失败: {ex.Message}", ex);
        }
    }

    public NoSqlQueryResult<IDictionary<string, T>> GetBatch<T>(IEnumerable<string> keys)
    {
        try
        {
            var keyArray = keys.Select(BuildKey).ToArray();
            var redisKeys = keyArray.Select(k => (RedisKey)k).ToArray();
            var values = _db.StringGet(redisKeys);

            var result = new Dictionary<string, T>();
            for (var i = 0; i < keyArray.Length; i++)
            {
                if (!values[i].IsNull)
                {
                    var value = JsonSerializer.Deserialize<T>((string)values[i]!);
                    if (value != null)
                        result[keyArray[i]] = value;
                }
            }

            return NoSqlQueryResult<IDictionary<string, T>>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis GetBatch 失败: {Message}", ex.Message);
            return NoSqlQueryResult<IDictionary<string, T>>.Fail($"批量获取失败: {ex.Message}");
        }
    }

    public NoSqlOperationResult RemoveBatch(IEnumerable<string> keys)
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)BuildKey(k)).ToArray();
            var count = _db.KeyDelete(redisKeys);
            return NoSqlOperationResult.Ok((int)count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis RemoveBatch 失败: {Message}", ex.Message);
            return NoSqlOperationResult.Fail($"批量删除失败: {ex.Message}", ex);
        }
    }

    // ==================== 扫描 ====================

    public NoSqlQueryResult<IEnumerable<string>> ScanByKey(string pattern, int count = 100)
    {
        try
        {
            var fullPattern = BuildKey(pattern);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: fullPattern, pageSize: count).Take(count).Select(k => k.ToString());

            return NoSqlQueryResult<IEnumerable<string>>.Ok(keys);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis ScanByKey 失败: {Message}", ex.Message);
            return NoSqlQueryResult<IEnumerable<string>>.Fail($"扫描失败: {ex.Message}");
        }
    }

    // ==================== 命名空间管理 ====================

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
        try
        {
            var ping = _db.Ping();
            return NoSqlOperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis 健康检查失败");
            return NoSqlOperationResult.Fail($"Redis 不可用: {ex.Message}", ex);
        }
    }

    // ==================== 自动清除 ====================

    public NoSqlOperationResult Cleanup(string pattern, int batchSize = 100)
    {
        try
        {
            var fullPattern = BuildKey(pattern);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var deletedCount = 0;

            foreach (var key in server.Keys(pattern: fullPattern, pageSize: batchSize))
            {
                _db.KeyDelete(key);
                deletedCount++;

                if (deletedCount >= _options.Cleanup.MaxCleanupPerRun)
                    break;
            }

            return NoSqlOperationResult.Ok(deletedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Cleanup 失败: {Pattern}", pattern);
            return NoSqlOperationResult.Fail($"清理失败: {ex.Message}", ex);
        }
    }

    // ==================== 私有方法 ====================

    private NoSqlOperationResult SetInternal<T>(string fullKey, T value, TimeSpan? absoluteExpiration, TimeSpan? slidingExpiration)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);

            if (absoluteExpiration.HasValue && slidingExpiration.HasValue)
            {
                // StackExchange.Redis 不直接支持滑动过期，需要使用组合方案
                // 写入时同时设置绝对过期时间（取绝对过期和滑动过期的最大值）
                var maxExpiration = TimeSpan.FromTicks(Math.Max(absoluteExpiration.Value.Ticks, slidingExpiration.Value.Ticks));
                _db.StringSet(fullKey, json, maxExpiration);
            }
            else if (absoluteExpiration.HasValue)
            {
                _db.StringSet(fullKey, json, absoluteExpiration.Value);
            }
            else
            {
                _db.StringSet(fullKey, json);
            }

            return NoSqlOperationResult.Ok(1);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Redis Set 失败: {Key}", fullKey);
            return NoSqlOperationResult.Fail($"写入失败: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
