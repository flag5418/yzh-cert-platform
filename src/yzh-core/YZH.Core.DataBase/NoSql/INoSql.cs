namespace YZH.Core.DataBase.NoSql;

/// <summary>
///     NoSQL 通用接口（改进版）
///     对标老架构的 INoSql，增加以下能力：
///     1. TTL/过期时间支持（绝对过期 + 滑动过期）
///     2. Key 前缀命名空间隔离
///     3. 健康检查
///     4. 结构化结果返回（替代 out string Err）
///     5. 批量操作优化（Pipeline）
///     6. 自动清除策略
/// </summary>
public interface INoSql
{
    // === 基础 CRUD ===

    /// <summary>写入单个值（永不过期）</summary>
    NoSqlOperationResult Set<T>(string key, T value);

    /// <summary>写入单个值（带过期时间）</summary>
    NoSqlOperationResult Set<T>(string key, T value, TimeSpan absoluteExpiration);

    /// <summary>写入单个值（带滑动过期时间）</summary>
    NoSqlOperationResult Set<T>(string key, T value, TimeSpan absoluteExpiration, TimeSpan slidingExpiration);

    /// <summary>读取单个值</summary>
    NoSqlQueryResult<T> Get<T>(string key);

    /// <summary>删除单个 key</summary>
    NoSqlOperationResult Remove(string key);

    /// <summary>检查 key 是否存在</summary>
    NoSqlOperationResult<bool> Exists(string key);

    /// <summary>获取 key 剩余 TTL（秒）</summary>
    NoSqlQueryResult<long> GetTtl(string key);

    /// <summary>更新 key 的 TTL</summary>
    NoSqlOperationResult Expire(string key, TimeSpan expiration);

    /// <summary>移除 key 的过期时间（持久化）</summary>
    NoSqlOperationResult Persist(string key);

    // === 批量操作 ===

    /// <summary>批量写入</summary>
    NoSqlOperationResult SetBatch<T>(IEnumerable<KeyValuePair<string, T>> items, TimeSpan? absoluteExpiration = null);

    /// <summary>批量读取</summary>
    NoSqlQueryResult<IDictionary<string, T>> GetBatch<T>(IEnumerable<string> keys);

    /// <summary>批量删除</summary>
    NoSqlOperationResult RemoveBatch(IEnumerable<string> keys);

    // === 条件查询（部分 NoSQL 支持） ===

    /// <summary>按前缀扫描 key</summary>
    NoSqlQueryResult<IEnumerable<string>> ScanByKey(string pattern, int count = 100);

    // === 命名空间管理 ===

    /// <summary>构建带前缀的完整 key</summary>
    string BuildKey(string key);

    /// <summary>设置默认命名空间前缀</summary>
    void SetPrefix(string prefix);

    // === 健康检查 ===

    /// <summary>检查 NoSQL 服务是否可用</summary>
    NoSqlOperationResult HealthCheck();

    /// <summary>获取后端类型名称</summary>
    string ProviderName { get; }

    // === 自动清除 ===

    /// <summary>按模式批量删除（用于定时清理）</summary>
    NoSqlOperationResult Cleanup(string pattern, int batchSize = 100);
}

/// <summary>
///     NoSQL 配置选项（record 类型，支持 with 表达式创建变体）
/// </summary>
public record NoSqlOptions
{
    /// <summary>Provider 类型：Redis / MemoryCache / FileSystem</summary>
    public string ProviderType { get; set; } = "MemoryCache";

    /// <summary>连接字符串</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>默认 Key 前缀（项目名称/模块名称）</summary>
    public string DefaultPrefix { get; set; } = "yzh";

    /// <summary>默认过期时间（分钟），0 表示永不过期</summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>序列化方式：JSON / MessagePack</summary>
    public string SerializerType { get; set; } = "JSON";

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>重试间隔（毫秒）</summary>
    public int RetryDelayMs { get; set; } = 200;

    /// <summary>启用健康检查</summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>健康检查间隔（秒）</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>清理策略</summary>
    public CleanupPolicy Cleanup { get; set; } = new();
}

/// <summary>
///     清理策略配置
/// </summary>
public record CleanupPolicy
{
    /// <summary>是否启用自动清理</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>清理 cron 表达式</summary>
    public string CronExpression { get; set; } = "0 3 * * *"; // 每天凌晨 3 点

    /// <summary>要清理的 key 模式（为空则清理所有过期 key）</summary>
    public List<string> Patterns { get; set; } = new();

    /// <summary>每批删除数量</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>单次清理最大数量（防止内存溢出）</summary>
    public int MaxCleanupPerRun { get; set; } = 10000;
}
