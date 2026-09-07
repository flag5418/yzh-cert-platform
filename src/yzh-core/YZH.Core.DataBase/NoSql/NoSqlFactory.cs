using Microsoft.Extensions.Logging;

namespace YZH.Core.DataBase.NoSql;

/// <summary>
///     NoSQL 提供程序工厂
///     根据配置创建对应的 NoSQL 实现
/// </summary>
public interface INoSqlFactory
{
    /// <summary>创建 NoSQL 提供程序</summary>
    INoSql CreateProvider(string name = "default");

    /// <summary>获取默认提供程序</summary>
    INoSql DefaultProvider { get; }
}

/// <summary>
///     NoSQL 工厂实现
/// </summary>
public class NoSqlFactory : INoSqlFactory, IDisposable
{
    private readonly NoSqlOptions _options;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly Dictionary<string, INoSql> _providers = new();

    public INoSql DefaultProvider => CreateProvider("default");

    public NoSqlFactory(NoSqlOptions options, ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public INoSql CreateProvider(string name = "default")
    {
        if (_providers.TryGetValue(name, out var existing))
            return existing;

        var provider = CreateProviderInternal(name);
        _providers[name] = provider;
        return provider;
    }

    private INoSql CreateProviderInternal(string name)
    {
        var options = _options with { DefaultPrefix = string.IsNullOrEmpty(name) || name == "default" ? _options.DefaultPrefix : $"{_options.DefaultPrefix}:{name}" };

        return options.ProviderType.ToLower() switch
        {
            "redis" => new RedisNoSqlProvider(options, _loggerFactory?.CreateLogger<RedisNoSqlProvider>()),
            "memorycache" or "memory" => new MemoryCacheProvider(options, _loggerFactory?.CreateLogger<MemoryCacheProvider>()),
            _ => new MemoryCacheProvider(options, _loggerFactory?.CreateLogger<MemoryCacheProvider>())
        };
    }

    public void Dispose()
    {
        foreach (var provider in _providers.Values)
        {
            (provider as IDisposable)?.Dispose();
        }
        _providers.Clear();
    }
}
