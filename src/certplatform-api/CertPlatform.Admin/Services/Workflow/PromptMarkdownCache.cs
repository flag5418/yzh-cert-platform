using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CertPlatform.Admin.Services.Workflow;

/// <summary>
/// 【提示词工作台】企业文档 Markdown 复用缓存（Redis）。
///
/// <para><b>为什么存在</b>（33 号 §八 勘误，2026-10-02 用户裁决）：
/// 原设计「文件转完 Markdown 即弃」导致**改一次提示词就要重新上传 + 重新转换一次**，
/// 而真实调试是「上传一次企业文件 → 反复调提示词 → 反复分析」。
/// 现在转换结果落 Redis：<b>没换文件就复用同一份 Markdown</b>，换了文件才重新转换覆盖。</para>
///
/// <para><b>范围</b>（Q1 裁决 = 方案 A）：**只有本工作台的 Markdown 走 Redis**；
/// 框架层 <c>INoSql</c> / 认证 / 字典 / 实体缓存仍是内存（<c>MemoryCacheNoSql</c>），一行未改。</para>
///
/// <para><b>降级</b>：Redis 连不上时**不抛异常**——<c>Get</c> 返回 null（表现为「必须重新上传文件」）、
/// <c>Set</c> 静默丢弃。工作台功能完整，只是少了缓存便利。</para>
///
/// <para><b>配置</b>：<c>appsettings.json → Redis:ConnectionString</c>（默认 <c>127.0.0.1:6380</c>，无密码）。</para>
/// </summary>
public sealed class PromptMarkdownCache : IDisposable
{
    /// <summary>key 前缀（便于 <c>SCAN</c> 排查 / 单独清理）</summary>
    public const string KeyPrefix = "prompt_md:";

    /// <summary>默认 TTL = 8 小时（覆盖一个完整工作日的「改提示词 → 重跑」循环）</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(8);

    /// <summary>连接重试最小间隔（避免每次请求都去握手）</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);

    private readonly ILogger<PromptMarkdownCache> _logger;
    private readonly string _connectionString;
    private readonly object _gate = new();

    private volatile ConnectionMultiplexer? _connection;
    private DateTime _nextAttemptUtc = DateTime.MinValue;

    public PromptMarkdownCache(IConfiguration configuration, ILogger<PromptMarkdownCache> logger)
    {
        _logger = logger;
        _connectionString = configuration["Redis:ConnectionString"] ?? "127.0.0.1:6380";
    }

    /// <summary>当前是否已连上（仅用于界面提示，不参与业务判断）</summary>
    public bool IsAvailable => _connection?.IsConnected == true;

    /// <summary>缓存 key（对外暴露，便于前端拿到后在「重新分析」时回传）</summary>
    public static string MakeKey(string cacheKey) => KeyPrefix + cacheKey;

    /// <summary>
    /// 写入 Markdown。失败 / 未连接 / Redis 关闭一律**只记日志**，不抛。
    /// </summary>
    public async Task SetAsync(string cacheKey, string markdown, TimeSpan? ttl = null)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrEmpty(markdown)) return;

        var db = GetDatabase();
        if (db == null) return;

        try
        {
            await db.StringSetAsync(MakeKey(cacheKey), markdown, ttl ?? DefaultTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PromptMdCache] 写入失败 key={Key}", MakeKey(cacheKey));
        }
    }

    /// <summary>读取 Markdown；不存在 / 未连接 / 读失败均返回 null（由调用方决定提示语）。</summary>
    public async Task<string?> GetAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return null;

        var db = GetDatabase();
        if (db == null) return null;

        try
        {
            var val = await db.StringGetAsync(MakeKey(cacheKey));
            return val.IsNullOrEmpty ? null : val.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PromptMdCache] 读取失败 key={Key}", MakeKey(cacheKey));
            return null;
        }
    }

    /// <summary>删除指定缓存（换文件时调用，随后由 Set 覆写）</summary>
    public async Task RemoveAsync(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return;

        var db = GetDatabase();
        if (db == null) return;

        try
        {
            await db.KeyDeleteAsync(MakeKey(cacheKey));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PromptMdCache] 删除失败 key={Key}", MakeKey(cacheKey));
        }
    }

    /// <summary>
    /// 惰性建立连接。**失败不抛**，并在 <see cref="RetryInterval"/> 内不再重试，
    /// 避免 Redis 挂掉时每个请求都卡在握手超时上。
    /// </summary>
    private IDatabase? GetDatabase()
    {
        var conn = _connection;
        if (conn != null && conn.IsConnected) return conn.GetDatabase();

        lock (_gate)
        {
            if (_connection != null && _connection.IsConnected) return _connection.GetDatabase();

            var now = DateTime.UtcNow;
            if (now < _nextAttemptUtc) return null;

            try
            {
                // abortOnConnectFail = false ⇒ 即使当前连不上也能拿到句柄，后台自动重连
                var options = ConfigurationOptions.Parse(_connectionString);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 3000;
                options.SyncTimeout = 3000;
                options.AsyncTimeout = 10000;

                _connection = ConnectionMultiplexer.Connect(options);
                _connection.ConnectionFailed += (_, e) =>
                    _logger.LogWarning("[PromptMdCache] 连接断开：{Type} {End}", e.FailureType, e.EndPoint);
                _nextAttemptUtc = DateTime.MinValue;
                _logger.LogInformation("[PromptMdCache] 已连接 {Conn}（TTL {Ttl}）", _connectionString, DefaultTtl);
            }
            catch (Exception ex)
            {
                _connection = null;
                _nextAttemptUtc = now + RetryInterval;
                _logger.LogWarning(ex, "[PromptMdCache] 连接失败，{Sec}s 后重试；工作台降级为「每次重新转换」",
                    RetryInterval.TotalSeconds);
                return null;
            }

            return _connection?.GetDatabase();
        }
    }

    public void Dispose()
    {
        try { _connection?.Dispose(); } catch { /* 忽略释放期异常 */ }
        _connection = null;
    }
}
