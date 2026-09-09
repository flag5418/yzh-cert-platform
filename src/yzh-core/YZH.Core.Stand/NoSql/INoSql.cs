namespace YZH.Core.Stand.NoSql;

/// <summary>
///     NoSQL 缓存接口（Redis/MemoryCache 抽象）
/// </summary>
public interface INoSql
{
    /// <summary>获取缓存</summary>
    T? Get<T>(string key);

    /// <summary>设置缓存</summary>
    bool Set<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>批量设置缓存</summary>
    bool SetBatch<T>(Dictionary<string, T> items, TimeSpan? expiration = null);

    /// <summary>删除缓存</summary>
    bool Remove(string key);

    /// <summary>批量删除</summary>
    bool RemoveBatch(IEnumerable<string> keys);

    /// <summary>检查 key 是否存在</summary>
    bool Exists(string key);

    /// <summary>按前缀扫描 key</summary>
    IEnumerable<string> ScanByKey(string pattern);
}
