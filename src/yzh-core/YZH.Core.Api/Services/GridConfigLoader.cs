using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     EntityConfig 加载服务
///     负责加载和管理实体页面的 UI 配置（表格+表单+工具栏+搜索）
///     
///     加载优先级：
///     1. MemoryCache（运行时缓存）
///     2. JSON 文件系统（Assets/EntityConfigs/{TypeName}.json）
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
/// </summary>
public class EntityConfigLoader : IEntityConfigLoader
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<EntityConfigLoader> _logger;
    private readonly string _configDirectory;

    public EntityConfigLoader(IMemoryCache cache, ILogger<EntityConfigLoader> logger, IWebHostEnvironment environment)
    {
        _cache = cache;
        _logger = logger;
        _configDirectory = Path.Combine(environment.ContentRootPath, "Assets", "EntityConfigs");
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

        // 2. 从文件加载
        var config = LoadFromFile(typeName);

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
        // MemoryCache 不支持通配删除，实际项目中可用 CompactRC 或标记过期
        _logger.LogWarning("InvalidateAll 暂不支持，请逐个类型清除或使用绝对过期时间");
    }

    /// <summary>
    ///     从 JSON 文件加载配置
    /// </summary>
    private EntityConfig LoadFromFile(string typeName)
    {
        var configPath = Path.Combine(_configDirectory, $"{typeName}.json");

        try
        {
            if (File.Exists(configPath))
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
            else
            {
                _logger.LogWarning("EntityConfig 文件不存在: {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EntityConfig 加载失败: {Path}", configPath);
        }

        // 返回默认空配置
        return new EntityConfig
        {
            Title = typeName,
            Columns = new List<DefineColumn>()
        };
    }
}
