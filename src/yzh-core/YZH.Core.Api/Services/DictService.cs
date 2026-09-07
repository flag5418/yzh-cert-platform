using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YZH.Core.Stand.Models;

namespace YZH.Core.Api.Services;

/// <summary>
///     字典服务实现
///     管理所有 Controller 注册的字典数据源
///     提供统一的字典获取入口（带缓存）
/// </summary>
public class DictService : IDictService
{
    private readonly ICacheManager _cacheManager;
    private readonly ILogger<DictService> _logger;

    // 字典数据源注册表：ControllerName -> (DictCode -> Loader)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DictLoaderEntry>> _registrations = new();

    // 异步加载器注册表
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Func<Task<List<DictItem>>>>> _asyncLoaders = new();

    public DictService(ICacheManager cacheManager, ILogger<DictService> logger)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public void Register(string controllerName, string dictCode, Func<List<DictItem>> loader)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        var dicts = _registrations.GetOrAdd(controllerKey, _ => new ConcurrentDictionary<string, DictLoaderEntry>());

        dicts[dictCode] = new DictLoaderEntry
        {
            Loader = loader,
            DictCode = dictCode
        };

        _logger.LogDebug("字典注册: {Controller}.{DictCode}", controllerKey, dictCode);
    }

    public void Register(string controllerName, string dictCode, Func<object?, List<DictItem>> loaderWithArgs)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        var dicts = _registrations.GetOrAdd(controllerKey, _ => new ConcurrentDictionary<string, DictLoaderEntry>());

        dicts[dictCode] = new DictLoaderEntry
        {
            Loader = () => loaderWithArgs(null),
            LoaderWithArgs = loaderWithArgs,
            DictCode = dictCode
        };
    }

    public void Register(string controllerName, string dictCode, Func<Task<List<DictItem>>> loader)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        var loaders = _asyncLoaders.GetOrAdd(controllerKey, _ => new ConcurrentDictionary<string, Func<Task<List<DictItem>>>>());
        loaders[dictCode] = loader;
    }

    public List<DictItem> GetDict(string controllerName, string dictCode)
    {
        var controllerKey = NormalizeControllerName(controllerName);

        // 尝试同步加载
        if (_registrations.TryGetValue(controllerKey, out var dicts) &&
            dicts.TryGetValue(dictCode, out var entry))
        {
            var cacheKey = $"dict:{controllerKey}:{dictCode}";
            return _cacheManager.GetOrAdd(cacheKey, entry.Loader, TimeSpan.FromMinutes(120)) ?? new List<DictItem>();
        }

        // 尝试异步加载
        if (_asyncLoaders.TryGetValue(controllerKey, out var asyncDicts) &&
            asyncDicts.TryGetValue(dictCode, out var asyncLoader))
        {
            var cacheKey = $"dict:{controllerKey}:{dictCode}";
            // 同步等待异步结果（实际生产环境建议前端用异步接口）
            var task = asyncLoader();
            task.Wait();
            var items = task.Result;
            _cacheManager.Set(cacheKey, items, TimeSpan.FromMinutes(120));
            return items;
        }

        _logger.LogWarning("字典未注册: {Controller}.{DictCode}", controllerKey, dictCode);
        return new List<DictItem>();
    }

    public List<DictItem> GetDict(string controllerName, string dictCode, object? args)
    {
        // 带参数的字典查询，不缓存（因为参数可能每次都不同）
        var controllerKey = NormalizeControllerName(controllerName);

        if (_registrations.TryGetValue(controllerKey, out var dicts) &&
            dicts.TryGetValue(dictCode, out var entry) &&
            entry.LoaderWithArgs != null)
        {
            return entry.LoaderWithArgs(args) ?? new List<DictItem>();
        }

        // 无参数版本兜底
        return GetDict(controllerName, dictCode);
    }

    public void InvalidateDict(string controllerName, string dictCode)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        var cacheKey = $"dict:{controllerKey}:{dictCode}";
        _cacheManager.Remove(cacheKey);
        _logger.LogInformation("字典缓存清除: {Controller}.{DictCode}", controllerKey, dictCode);
    }

    public void InvalidateControllerDicts(string controllerName)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        _cacheManager.RemoveByPrefix($"dict:{controllerKey}:");
        _logger.LogInformation("Controller 字典缓存全部清除: {Controller}", controllerKey);
    }

    public IEnumerable<string> GetRegisteredDictCodes(string controllerName)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        if (_registrations.TryGetValue(controllerKey, out var dicts))
            return dicts.Keys;
        return Enumerable.Empty<string>();
    }

    public bool IsRegistered(string controllerName, string dictCode)
    {
        var controllerKey = NormalizeControllerName(controllerName);
        return _registrations.TryGetValue(controllerKey, out var dicts) && dicts.ContainsKey(dictCode);
    }

    private static string NormalizeControllerName(string controllerName)
    {
        // 移除 "Controller" 后缀
        if (controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            return controllerName[..^"Controller".Length];
        return controllerName;
    }

    /// <summary>
    ///     字典加载器条目
    /// </summary>
    private class DictLoaderEntry
    {
        public Func<List<DictItem>> Loader { get; set; } = null!;
        public Func<object?, List<DictItem>>? LoaderWithArgs { get; set; }
        public string DictCode { get; set; } = string.Empty;
    }
}
