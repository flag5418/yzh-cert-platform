using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     GridConfig 加载服务
///     负责加载和管理表格/表单的 UI 配置
///     
///     加载优先级：
///     1. MemoryCache（运行时缓存）
///     2. JSON 文件系统（Assets/GridConfigs/{TypeName}.json）
///     3. 数据库（未来扩展）
///     4. 默认空配置
/// </summary>
public interface IGridConfigLoader
{
    /// <summary>根据实体类型获取 GridConfig</summary>
    GridConfig GetConfig<T>() where T : BaseEntity;

    /// <summary>根据类型名称获取 GridConfig</summary>
    GridConfig GetConfig(string typeName);

    /// <summary>清除指定类型的缓存</summary>
    void Invalidate<T>() where T : BaseEntity;

    /// <summary>清除所有缓存</summary>
    void InvalidateAll();
}

/// <summary>
///     GridConfig 加载器实现
/// </summary>
public class GridConfigLoader : IGridConfigLoader
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<GridConfigLoader> _logger;
    private readonly string _configDirectory;

    public GridConfigLoader(IMemoryCache cache, ILogger<GridConfigLoader> logger, IWebHostEnvironment environment)
    {
        _cache = cache;
        _logger = logger;
        _configDirectory = Path.Combine(environment.ContentRootPath, "Assets", "GridConfigs");
    }

    public GridConfig GetConfig<T>() where T : BaseEntity
    {
        var typeName = typeof(T).Name;
        return GetConfig(typeName);
    }

    public GridConfig GetConfig(string typeName)
    {
        // 1. 尝试从缓存获取
        var cacheKey = $"gridconfig:{typeName}";
        if (_cache.TryGetValue(cacheKey, out GridConfig? cached) && cached != null)
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
        _cache.Remove($"gridconfig:{typeName}");
    }

    public void InvalidateAll()
    {
        // MemoryCache 不支持通配删除，实际项目中可用 CompactRC 或标记过期
        _logger.LogWarning("InvalidateAll 暂不支持，请逐个类型清除或使用绝对过期时间");
    }

    /// <summary>
    ///     从 JSON 文件加载配置
    /// </summary>
    private GridConfig LoadFromFile(string typeName)
    {
        var configPath = Path.Combine(_configDirectory, $"{typeName}.json");

        try
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<GridConfig>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    _logger.LogDebug("GridConfig 加载成功: {Path}", configPath);
                    return config;
                }
            }
            else
            {
                _logger.LogWarning("GridConfig 文件不存在: {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GridConfig 加载失败: {Path}", configPath);
        }

        // 返回默认空配置
        return new GridConfig
        {
            ConfigName = typeName,
            TableName = typeName,
            Title = typeName,
            Columns = new List<DefineColumn>()
        };
    }
}
