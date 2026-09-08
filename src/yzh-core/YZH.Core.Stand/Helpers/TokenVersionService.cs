using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace YZH.Core.Stand.Helpers;

/// <summary>
///     Token 版本服务（SSO 挤号核心）
///     
///     原理：
///     每个用户在 Redis 维护一个 version 值（Guid.ToString()）。
///     登录时生成新 version 写入 Redis，同时写入 JWT 的 "ver" claim。
///     校验 Token 时从 Redis 取出当前 version，与 JWT 中的 version 比对。
///     新登录生成新 version 后，所有旧 Token 校验均失效 → 实现挤号。
///     
///     缓存设计：
///     - Key: token_ver:{userCode}
///     - Value: version (string)
///     - TTL: 与 Token 过期时间一致
/// </summary>
public class TokenVersionService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(30);

    public TokenVersionService(IDistributedCache cache)
    {
        _cache = cache;
    }

    /// <summary>生成新的 Token 版本号（登录时调用）</summary>
    public async Task<string> GenerateVersionAsync(string userCode, TimeSpan? ttl = null)
    {
        var version = Guid.NewGuid().ToString("N");
        var key = GetKey(userCode);
        await _cache.SetStringAsync(key, version, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
        });
        return version;
    }

    /// <summary>校验 Token 版本是否有效</summary>
    public async Task<bool> ValidateVersionAsync(string userCode, string tokenVersion)
    {
        var key = GetKey(userCode);
        var currentVersion = await _cache.GetStringAsync(key);
        
        // Redis 中没有版本记录，说明 Token 已过期/被挤号
        if (string.IsNullOrEmpty(currentVersion))
            return false;

        return string.Equals(currentVersion, tokenVersion, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>使版本失效（登出时调用）</summary>
    public async Task InvalidateVersionAsync(string userCode)
    {
        await _cache.RemoveAsync(GetKey(userCode));
    }

    /// <summary>挤掉用户所有在线 Token（通过生成新版本使所有旧版本失效）</summary>
    public async Task<string> BumpVersionAsync(string userCode, TimeSpan? ttl = null)
    {
        return await GenerateVersionAsync(userCode, ttl);
    }

    private static string GetKey(string userCode) => $"token_ver:{userCode}";
}
