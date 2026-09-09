using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;
using YZH.Core.Stand.NoSql;

namespace YZH.Core.Api.Services;

/// <summary>
///     缓存管理器接口
///     为实体和字典提供统一的缓存抽象层
///     封装 INoSql（Redis/MemoryCache），提供高级 API
/// </summary>
public interface ICacheManager
{
    // ==================== 基础缓存操作 ====================

    /// <summary>获取缓存，不存在时从数据源加载</summary>
    T? GetOrAdd<T>(string key, Func<T?> factory, TimeSpan? expiration = null) where T : class;

    /// <summary>获取缓存（异步版本）</summary>
    Task<T?> GetOrAddAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null) where T : class;

    /// <summary>写入缓存</summary>
    void Set<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>批量写入缓存</summary>
    void SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, TimeSpan? expiration = null);

    /// <summary>删除指定 key</summary>
    void Remove(string key);

    /// <summary>批量删除</summary>
    void RemoveBatch(IEnumerable<string> keys);

    /// <summary>按前缀批量删除（如 dict:*, entity:ISOStandard:*）</summary>
    void RemoveByPrefix(string prefix);

    /// <summary>检查 key 是否存在</summary>
    bool Exists(string key);

    /// <summary>清空所有缓存</summary>
    void Clear();

    // ==================== 实体缓存便捷方法（从特性获取配置） ====================

    /// <summary>获取实体缓存（自动从 YZHCacheEntity 特性读取配置）</summary>
    T? GetEntity<T>(string id, Func<T?> factory) where T : BaseEntity;

    /// <summary>获取实体列表缓存（自动从特性读取配置）</summary>
    List<T> GetEntityList<T>(string key, Func<List<T>> factory) where T : BaseEntity;

    /// <summary>设置实体缓存（自动从特性读取配置）</summary>
    void SetEntity<T>(string id, T entity) where T : BaseEntity;

    /// <summary>设置实体列表缓存（自动从特性读取配置）</summary>
    void SetEntityList<T>(string key, List<T> entities) where T : BaseEntity;

    /// <summary>清除指定类型实体的所有缓存</summary>
    void InvalidateEntity<T>() where T : BaseEntity;

    /// <summary>清除指定实体的缓存（单条 + 列表）</summary>
    void InvalidateEntity<T>(string id) where T : BaseEntity;

    // ==================== 字典缓存便捷方法 ====================

    /// <summary>获取字典缓存</summary>
    List<DictItem>? GetDict(string dictCode, Func<List<DictItem>> factory);

    /// <summary>设置字典缓存</summary>
    void SetDict(string dictCode, List<DictItem> items, TimeSpan? expiration = null);

    /// <summary>清除字典缓存</summary>
    void InvalidateDict(string dictCode);

    // ==================== Key 生成 ====================

    /// <summary>生成实体缓存 key</summary>
    string MakeEntityKey<T>(string id) where T : BaseEntity;

    /// <summary>生成字典缓存 key</summary>
    string MakeDictKey(string dictCode);
}

/// <summary>
///     字典项模型
/// </summary>
public class DictItem
{
    /// <summary>字典编码</summary>
    public string DictCode { get; set; } = string.Empty;
    /// <summary>字典标签（显示文本）</summary>
    public string DictLabel { get; set; } = string.Empty;
    /// <summary>字典值</summary>
    public string DictValue { get; set; } = string.Empty;
    /// <summary>排序号</summary>
    public int SortNo { get; set; }
}

/// <summary>
///     缓存管理器配置
/// </summary>
public class CacheManagerOptions
{
    /// <summary>默认过期时间（分钟）</summary>
    public int DefaultExpirationMinutes { get; set; } = 60;

    /// <summary>实体缓存默认过期时间（分钟）</summary>
    public int EntityCacheExpirationMinutes { get; set; } = 30;

    /// <summary>字典缓存默认过期时间（分钟）</summary>
    public int DictCacheExpirationMinutes { get; set; } = 120;

    /// <summary>是否启用缓存</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>缓存前缀</summary>
    public string Prefix { get; set; } = "yzh";
}
