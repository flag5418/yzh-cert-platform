using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;
using YZH.Core.Stand.Options;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Api.Services;

/// <summary>
///     EntityConfig 加载服务
///     负责加载和管理实体页面的 UI 配置（表格+表单+工具栏+搜索）
///     
///     加载优先级：
///     1. MemoryCache（运行时缓存）
///     2. JSON 文件系统（按配置目录顺序搜索）
///     3. 数据库（未来扩展）
///     4. 默认空配置
/// </summary>
public interface IEntityConfigLoader
{
    /// <summary>根据实体类型获取 EntityConfig</summary>
    EntityConfig GetConfig<T>() where T : BaseEntity;

    /// <summary>根据类型名称获取 EntityConfig</summary>
    EntityConfig GetConfig(string typeName);

    /// <summary>清除指定类型的缓存</summary>
    void Invalidate<T>() where T : BaseEntity;

    /// <summary>清除所有缓存</summary>
    void InvalidateAll();
}

/// <summary>
///     EntityConfig 加载器实现
///     支持从多个目录搜索配置（核心模块 + 业务模块）
/// </summary>
public class EntityConfigLoader : IEntityConfigLoader
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<EntityConfigLoader> _logger;
    private readonly List<string> _configDirectories;

    public EntityConfigLoader(IMemoryCache cache, ILogger<EntityConfigLoader> logger, YzhCoreOptions options)
    {
        _cache = cache;
        _logger = logger;
        _configDirectories = new List<string>();

        // 核心模块配置目录
        var corePath = options.CoreEntityConfigPath;
        if (!string.IsNullOrEmpty(corePath))
        {
            _configDirectories.Add(corePath);
        }

        // 业务模块配置目录
        if (options.BusinessEntityConfigPaths != null)
        {
            _configDirectories.AddRange(options.BusinessEntityConfigPaths);
        }
    }

    public EntityConfig GetConfig<T>() where T : BaseEntity
    {
        var typeName = typeof(T).Name;
        return GetConfig(typeName);
    }

    public EntityConfig GetConfig(string typeName)
    {
        // 1. 尝试从缓存获取
        var cacheKey = $"entityconfig:{typeName}";
        if (_cache.TryGetValue(cacheKey, out EntityConfig? cached) && cached != null)
        {
            return cached;
        }

        // 2. 从所有配置目录中搜索
        var config = LoadFromDirectories(typeName);

        // 3. 存入缓存（滑动过期 30 分钟）
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(30))
            .SetAbsoluteExpiration(TimeSpan.FromHours(2));
        _cache.Set(cacheKey, config, cacheOptions);

        return config;
    }

    public void Invalidate<T>() where T : BaseEntity
    {
        var typeName = typeof(T).Name;
        _cache.Remove($"entityconfig:{typeName}");
    }

    public void InvalidateAll()
    {
        _logger.LogWarning("InvalidateAll 暂不支持，请逐个类型清除或使用绝对过期时间");
    }

    /// <summary>
    ///     从所有配置目录中搜索指定类型的配置文件
    /// </summary>
    private EntityConfig LoadFromDirectories(string typeName)
    {
        if (_configDirectories.Count == 0)
        {
            return new EntityConfig { Title = typeName, Columns = new List<DefineColumn>() };
        }

        var targetName = typeName.ToLowerInvariant();

        foreach (var dir in _configDirectories)
        {
            if (!Directory.Exists(dir)) continue;

            // 直接查找 typeName.json
            var directPath = Path.Combine(dir, $"{typeName}.json");
            if (File.Exists(directPath))
            {
                return LoadFromFile(directPath, typeName);
            }

            // 遍历子目录递归搜索（支持 Domain/TypeName 格式）
            foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (fileName == targetName)
                {
                    return LoadFromFile(file, typeName);
                }
            }
        }

        return new EntityConfig { Title = typeName, Columns = new List<DefineColumn>() };
    }

    /// <summary>
    ///     从文件加载配置
    /// </summary>
    private EntityConfig LoadFromFile(string configPath, string typeName)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<EntityConfig>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config != null)
            {
                _logger.LogDebug("EntityConfig 加载成功: {Path}", configPath);
                return config;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EntityConfig 加载失败: {Path}", configPath);
        }

        return new EntityConfig { Title = typeName, Columns = new List<DefineColumn>() };
    }
}
