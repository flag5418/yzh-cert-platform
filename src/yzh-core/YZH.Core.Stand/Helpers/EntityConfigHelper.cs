using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Config;

namespace YZH.Core.Stand.Helpers;

/// <summary>
///     实体配置加载助手
///     职责：从 JSON 文件加载 EntityConfig
///     缓存：1小时自动过期 + FileWatcher 监听文件变更自动清除
///     
///     多目录支持：
///     - 核心模块配置目录（CoreConfigDir）
///     - 业务模块配置目录列表（BusinessConfigDirs）
///     搜索顺序：核心目录 → 业务目录1 → 业务目录2 → ...
/// </summary>
public static class EntityConfigHelper
{
    private static string _coreConfigDir = System.IO.Path.Combine(
        AppContext.BaseDirectory, "Assets", "EntityConfigs");
    
    private static List<string> _businessConfigDirs = new();

    private static readonly ConcurrentDictionary<string, (EntityConfig config, DateTime expires)> _cache = new();
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);
    private static FileSystemWatcher? _watcher;

    /// <summary>
    ///     设置核心配置目录
    /// </summary>
    public static void SetCoreConfigDir(string path) => _coreConfigDir = path;

    /// <summary>
    ///     添加业务配置目录
    /// </summary>
    public static void AddBusinessConfigDir(string path)
    {
        if (!_businessConfigDirs.Contains(path))
            _businessConfigDirs.Add(path);
    }

    /// <summary>
    ///     设置业务配置目录列表（覆盖）
    /// </summary>
    public static void SetBusinessConfigDirs(List<string> paths) => _businessConfigDirs = paths;

    static EntityConfigHelper()
    {
        InitFileWatcher();
    }

    /// <summary>
    ///     泛型方式获取配置（自动用 typeof(T).Name 匹配文件名）
    /// </summary>
    public static EntityConfig GetEntityConfig<T>()
    {
        var typeName = typeof(T).Name;
        return GetConfig(typeName);
    }

    /// <summary>
    ///     泛型方式获取配置（自动用 typeof(T).Name 匹配文件名）
    /// </summary>
    public static EntityConfig GetConfig<T>()
    {
        var typeName = typeof(T).Name;
        return GetConfig(typeName);
    }

    /// <summary>
    ///     自定义名称获取配置（文件名需与此一致，大小写不敏感）
    /// </summary>
    public static EntityConfig GetConfig(string configName)
    {
        var key = configName.ToLowerInvariant();

        // 1. 查缓存
        if (_cache.TryGetValue(key, out var entry) && entry.expires > DateTime.UtcNow)
            return entry.config;

        // 2. 缓存未命中/过期，加载文件
        var config = LoadFromFile(configName);

        // 3. 写入缓存
        _cache[key] = (config, DateTime.UtcNow.Add(_cacheDuration));

        return config;
    }

    /// <summary>
    ///     清除指定缓存
    /// </summary>
    public static void Invalidate<T>()
    {
        var key = typeof(T).Name.ToLowerInvariant();
        _cache.TryRemove(key, out _);
    }

    /// <summary>
    ///     清除所有缓存
    /// </summary>
    public static void InvalidateAll()
    {
        _cache.Clear();
    }

    /// <summary>
    ///     从 JSON 文件加载配置（支持子目录搜索，大小写不敏感）
    ///     按核心目录 → 业务目录1 → 业务目录2 → ... 顺序搜索
    ///     支持 "System/User" 格式直接定位
    /// </summary>
    private static EntityConfig LoadFromFile(string configName)
    {
        var targetName = configName.ToLowerInvariant();

        // 收集所有需要搜索的目录
        var allDirs = new List<string>();
        if (Directory.Exists(_coreConfigDir)) allDirs.Add(_coreConfigDir);
        allDirs.AddRange(_businessConfigDirs.Where(Directory.Exists));

        if (allDirs.Count == 0)
            return NewEmptyConfig(configName);

        // 1. 如果 configName 包含 '/'，视为 "Domain/Name" 直接拼接路径
        if (configName.Contains('/'))
        {
            foreach (var dir in allDirs)
            {
                var directPath = System.IO.Path.Combine(dir, configName + ".json");
                if (File.Exists(directPath))
                    return LoadAndParse(directPath, configName);
            }
        }

        // 2. 按目录顺序搜索
        foreach (var dir in allDirs)
        {
            // 搜索根目录（扁平兼容）
            var rootFile = Directory.GetFiles(dir, "*.json")
                .FirstOrDefault(f => System.IO.Path.GetFileNameWithoutExtension(f).ToLowerInvariant() == targetName);
            if (rootFile != null)
                return LoadAndParse(rootFile, configName);

            // 遍历子目录递归搜索
            foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (fileName == targetName)
                    return LoadAndParse(file, configName);
            }
        }

        return NewEmptyConfig(configName);
    }

    private static EntityConfig LoadAndParse(string filePath, string configName)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<EntityConfig>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });
            if (config != null) return config;
        }
        catch (Exception ex)
        {
            // ★ 2026-09-30：此前这里是空 catch ⇒ **JSON 里写错一个枚举值就会静默丢掉整份配置**
            //   （症状：Title 等于实体类型名、Columns=[]、页面有数据行却一列都不显示、零报错）。
            //   典型触发：`Type` 写了枚举里不存在的值（如 `CustomSlot` 曾长期缺失）。
            //   这里必须留下痕迹，否则排查成本极高。输出到 stderr → 后端日志文件可见。
            Console.Error.WriteLine(
                $"[EntityConfigHelper] ✗ 配置解析失败，已回落到空配置！file={filePath} " +
                $"（症状：页面有数据行但一列都不显示）。原因：{ex.GetType().Name}: {ex.Message}");
        }
        return NewEmptyConfig(configName);
    }

    private static EntityConfig NewEmptyConfig(string configName) => new()
    {
        Title = configName,
        Columns = new List<DefineColumn>()
    };

    /// <summary>
    ///     监听配置文件目录，文件变更时自动清除对应缓存
    /// </summary>
    private static void InitFileWatcher()
    {
        var allDirs = new List<string>();
        if (Directory.Exists(_coreConfigDir)) allDirs.Add(_coreConfigDir);
        allDirs.AddRange(_businessConfigDirs.Where(Directory.Exists));

        if (allDirs.Count == 0) return;

        try
        {
            _watcher = new FileSystemWatcher(allDirs[0], "*.json")
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.Renamed += OnConfigFileRenamed;
            _watcher.Created += OnConfigFileChanged;
        }
        catch
        {
            // FileSystemWatcher 初始化失败不影响核心功能
        }
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(e.Name);
        var key = fileName.ToLowerInvariant();
        _cache.TryRemove(key, out _);
    }

    private static void OnConfigFileRenamed(object sender, RenamedEventArgs e)
    {
        var oldFileName = System.IO.Path.GetFileNameWithoutExtension(e.OldName);
        _cache.TryRemove(oldFileName.ToLowerInvariant(), out _);
        var newFileName = System.IO.Path.GetFileNameWithoutExtension(e.Name);
        _cache.TryRemove(newFileName.ToLowerInvariant(), out _);
    }
}
