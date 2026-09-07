namespace YZH.Core.Api.Services;

/// <summary>
///     字典服务接口
///     管理业务启动时注册的字典数据源集合
///     
/// 设计：
/// - 每个单表 Controller 启动时注册字典数据源（Func）
/// - 继承类实现 OnRegisterDicts 方法，声明需要的字典及数据加载方式
/// - 前端调用 GET api/{controller}/dicts/{dictCode} 获取字典
/// - 字典数据默认使用 Redis 缓存，减少数据库查询
/// </summary>
public interface IDictService
{
    /// <summary>注册字典数据源</summary>
    void Register(string controllerName, string dictCode, Func<List<DictItem>> loader);

    /// <summary>注册带参数的字典数据源（如级联查询）</summary>
    void Register(string controllerName, string dictCode, Func<object?, List<DictItem>> loaderWithArgs);

    /// <summary>注册字典数据源（异步）</summary>
    void Register(string controllerName, string dictCode, Func<Task<List<DictItem>>> loader);

    /// <summary>获取字典（从缓存或数据源）</summary>
    List<DictItem> GetDict(string controllerName, string dictCode);

    /// <summary>获取字典（支持动态参数，如级联查询）</summary>
    List<DictItem> GetDict(string controllerName, string dictCode, object? args);

    /// <summary>清除字典缓存</summary>
    void InvalidateDict(string controllerName, string dictCode);

    /// <summary>清除指定 Controller 的所有字典缓存</summary>
    void InvalidateControllerDicts(string controllerName);

    /// <summary>获取 Controller 注册的所有字典编码</summary>
    IEnumerable<string> GetRegisteredDictCodes(string controllerName);

    /// <summary>检查字典是否已注册</summary>
    bool IsRegistered(string controllerName, string dictCode);
}
