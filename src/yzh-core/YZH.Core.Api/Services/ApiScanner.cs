using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Attributes;
using YZH.Core.Api.Models;

namespace YZH.Core.Api.Services;

/// <summary>
/// 接口描述符
/// </summary>
public class ApiDescriptor
{
    public string ApiCode { get; set; } = "";
    public string ControllerName { get; set; } = "";
    public string ActionName { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";

    /// <summary>接口分组：模块/控制器（如 System/Config），权限树按此分层</summary>
    public string GroupPath { get; set; } = "";

    /// <summary>分组层级片段（TreePath[0]/TreePath[1] 即 GroupPath）</summary>
    public string[] TreePath { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ParamDescriptor> Parameters { get; set; } = new();
}

/// <summary>
/// 参数描述符
/// </summary>
public class ParamDescriptor
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Description { get; set; } = "";
    public object? Default { get; set; }
}

/// <summary>
/// 接口扫描器
/// 通过反射自动发现所有 Controller 接口，生成 ApiDescriptor
/// </summary>
public class ApiScanner
{
    private readonly ILogger<ApiScanner> _logger;
    
    public ApiScanner(ILogger<ApiScanner> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 扫描所有程序集中的 Controller 接口
    /// </summary>
    public List<ApiDescriptor> Scan()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var apis = new List<ApiDescriptor>();
        var seen = new HashSet<string>();
        
        // JIT 加载问题：AppDomain.CurrentDomain.GetAssemblies() 只返回已加载的程序集
        // 需要手动加载 bin 目录下所有 YZH* 和 CertPlatform* 的 DLL
        var assemblies = LoadAllAssemblies();
        
        _logger.LogInformation("开始扫描 {Count} 个程序集", assemblies.Count());
        
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 某些类型依赖的程序集缺失，继续处理已成功加载的类型
                types = ex.Types.Where(t => t != null).ToArray();
                _logger.LogWarning("程序集 {Assembly} 部分类型加载失败，跳过 {Skipped} 个类型", 
                    assembly.GetName().Name, ex.Types.Count(t => t == null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描程序集 {Assembly} 失败", assembly.GetName().Name);
                continue;
            }
            
            foreach (var type in types)
            {
                if (type == null) continue;
                try
                {
                    if (!typeof(ControllerBase).IsAssignableFrom(type) 
                        || type.IsAbstract 
                        || type.IsGenericType)
                        continue;
                    
                    // 路由前缀：优先控制器自己声明的 Route（未声明时才回退到基类），
                    // 多路由（api/System/X 与 api/X）时取最具体的一条作为展示路径。
                    // 两条路由的 ApiCode 相同（取路由末段作控制器名），因此不影响已有授权。
                    var controllerName = type.Name.Replace("Controller", "");
                    var routeAttrs = type.GetCustomAttributes(typeof(RouteAttribute), false).OfType<RouteAttribute>().ToList();
                    if (routeAttrs.Count == 0)
                        routeAttrs = type.GetCustomAttributes(typeof(RouteAttribute), true).OfType<RouteAttribute>().ToList();

                    var routePrefix = routeAttrs
                        .Select(a => (a.Template ?? "").Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase))
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .OrderByDescending(t => t.Count(c => c == '/'))
                        .ThenByDescending(t => t.Length)
                        .FirstOrDefault() ?? $"api/{controllerName}";

                    // 分组：命名空间模块 / 控制器名（System/Config、Foundation/ISOClause）
                    var groupPath = BuildGroupPath(type, controllerName);

                    // 关键：必须扫描继承链。config/filter/add/update/delete/export/import/
                    // toggle-valid/action/{methodName} 等端点声明在 YzhControllerBase /
                    // TreeTableControllerBase 上，它们对每个派生控制器都是真实可调用的路由；
                    // 只扫 DeclaringType == type 会漏掉绝大部分接口，导致接口授权无法勾选。
                    var methods = CollectEndpointMethods(type);
                    
                    foreach (var method in methods)
                    {
                        try
                        {
                            var httpMethodAttr = GetHttpMethodAttribute(method);
                            if (httpMethodAttr == null) continue;
                            
                            // HttpMethodAttribute.HttpMethods 是 IEnumerable<string>，取第一个
                            var httpMethod = httpMethodAttr.HttpMethods.FirstOrDefault()?.ToUpper() ?? "GET";

                            // 真实地址：HttpPost("filter") 的路径是 api/xxx/filter，而不是方法名 Filter。
                            // 未显式指定模板时才回退到方法名。
                            var actionPath = string.IsNullOrWhiteSpace(httpMethodAttr.Template)
                                ? FormatPath(routePrefix, method.Name)
                                : FormatRoute(routePrefix, httpMethodAttr.Template);
                            
                            var apiCode = GenerateApiCode(
                                routePrefix,
                                httpMethod,
                                method.Name,
                                method.GetParameters()
                            );
                            
                            if (seen.Contains(apiCode))
                            {
                                _logger.LogWarning("重复接口 ApiCode: {ApiCode}, 跳过", apiCode);
                                continue;
                            }
                            seen.Add(apiCode);
                            
                            var descAttr = method.GetCustomAttribute<ApiDescriptionAttribute>(true);
                            
                            apis.Add(new ApiDescriptor
                            {
                                ApiCode = apiCode,
                                ControllerName = type.Name.Replace("Controller", ""),
                                ActionName = method.Name,
                                Method = httpMethod,
                                Path = actionPath,
                                GroupPath = groupPath,
                                TreePath = ParseTreePath(groupPath),
                                Description = ResolveDescription(method, httpMethodAttr, descAttr),
                                Author = descAttr?.Author ?? "",
                                CreatedAt = descAttr?.CreatedAt ?? DateTime.UtcNow,
                                UpdatedAt = descAttr?.UpdatedAt ?? DateTime.UtcNow,
                                Parameters = method.GetParameters()
                                    .Select(p => new ParamDescriptor
                                    {
                                        Name = p.Name ?? "",
                                        Type = p.ParameterType.Name,
                                        Required = !p.HasDefaultValue,
                                        Description = p.GetCustomAttribute<ParamDescriptionAttribute>()?.Description ?? "",
                                        Default = p.DefaultValue
                                    })
                                    .ToList()
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("扫描方法 {Controller}.{Method} 失败: {Message}", 
                                type.Name, method.Name, ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("扫描控制器 {Controller} 失败: {Message}", 
                        type.Name, ex.Message);
                }
            }
        }
        
        stopwatch.Stop();
        _logger.LogInformation("扫描完成，发现 {Count} 个接口，耗时 {Elapsed}ms", 
            apis.Count, stopwatch.ElapsedMilliseconds);
        
        return apis;
    }
    
    /// <summary>
    /// 收集控制器可调用的端点：自身 + 继承链上的基类（YzhControllerBase / TreeTableControllerBase …）
    /// 同名同参只保留最派生的一份（override / new），基类路由前缀与派生控制器一致
    /// </summary>
    private static List<MethodInfo> CollectEndpointMethods(Type controllerType)
    {
        var result = new List<MethodInfo>();
        var seenSignatures = new HashSet<string>(StringComparer.Ordinal);

        for (var t = controllerType; t != null && t != typeof(object); t = t.BaseType)
        {
            if (!typeof(ControllerBase).IsAssignableFrom(t)) break;

            foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName || method.IsStatic) continue;

                var signature = $"{method.Name}({string.Join(',', method.GetParameters().Select(p => p.ParameterType.Name))})";
                if (!seenSignatures.Add(signature)) continue; // 已被派生类实现，跳过基类声明
                result.Add(method);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取 Http 方法特性：重写方法上若未重复声明，必须回退到基类声明
    /// （GetCustomAttributes(false) 不返回继承来的特性，这也是端点被漏掉的原因之一）
    /// </summary>
    private static HttpMethodAttribute? GetHttpMethodAttribute(MethodInfo method)
    {
        var attr = method.GetCustomAttributes(false).OfType<HttpMethodAttribute>().FirstOrDefault();
        if (attr != null) return attr;

        var baseDefinition = method.GetBaseDefinition();
        return baseDefinition != method
            ? baseDefinition.GetCustomAttributes(false).OfType<HttpMethodAttribute>().FirstOrDefault()
            : null;
    }

    /// <summary>
    /// 接口分组：命名空间模块 / 控制器名（如 System/Config、Foundation/ISOClause）
    /// 与路由前缀（api/System/Config）同源，保证权限树分层稳定
    /// </summary>
    private static string BuildGroupPath(Type controllerType, string controllerName)
    {
        var module = ExtractModule(controllerType.Namespace);
        return string.IsNullOrEmpty(module) ? controllerName : $"{module}/{controllerName}";
    }

    /// <summary>从命名空间取模块段（...Controllers.System → System）</summary>
    private static string ExtractModule(string? ns)
    {
        if (string.IsNullOrEmpty(ns)) return "";

        var parts = ns.Split('.');
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (!parts[i].EndsWith("Controllers", StringComparison.OrdinalIgnoreCase)) continue;
            return i == parts.Length - 1 ? "" : parts[i + 1];
        }

        return "";
    }

    /// <summary>接口名称：[ApiDescription] → 框架端点中文名 → 方法名</summary>
    private static string ResolveDescription(
        MethodInfo method,
        HttpMethodAttribute httpMethodAttr,
        ApiDescriptionAttribute? descAttr)
    {
        if (descAttr != null && !string.IsNullOrWhiteSpace(descAttr.Description))
            return descAttr.Description;

        var template = string.IsNullOrWhiteSpace(httpMethodAttr.Template)
            ? method.Name
            : httpMethodAttr.Template!.Trim();

        return BaseEndpointNames.TryGetValue(template, out var name) ? name : method.Name;
    }

    /// <summary>框架基类端点（无 [ApiDescription]）的中文名，避免权限树里出现空白名称</summary>
    private static readonly Dictionary<string, string> BaseEndpointNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["config"] = "获取接口配置",
        ["filter"] = "分页查询",
        ["add"] = "新增",
        ["update"] = "修改",
        ["delete"] = "批量删除",
        ["export"] = "导出",
        ["import"] = "导入",
        ["import/template"] = "下载导入模板",
        ["action/{methodName}"] = "执行行操作",
        ["toggle-valid"] = "启用/禁用",
        ["tree/root"] = "树根节点",
        ["tree/children"] = "树子节点",
        ["tree/add"] = "新增树节点",
        ["tree/update"] = "修改树节点",
        ["tree/delete"] = "删除树节点",
        ["tree/action/{methodName}"] = "执行树节点操作",
        ["tree/toggle-valid"] = "树节点启用/禁用",
        ["treepconfig"] = "树表配置",
        ["checkTree"] = "关联树数据",
        ["check/add"] = "新增关联",
        ["check/remove"] = "移除关联",
        ["check/all"] = "全部关联",
    };

    /// <summary>
    /// 生成 ApiCode（基于 Method + ControllerName + ActionName）
    /// 只要这三个信息不变，Code 就稳定唯一
    /// </summary>
    private string GenerateApiCode(string route, string httpMethod, string actionName, ParameterInfo[] parameters)
    {
        var controllerName = route.Split('/').LastOrDefault() ?? "";
        var input = $"{httpMethod.ToUpper()}|{controllerName}|{actionName.ToLower()}";
        return Sha256Hash(input);
    }
    
    private string[] ParseTreePath(string path)
    {
        return path.Split('/')
            .Where(p => !string.IsNullOrEmpty(p) && p != "api")
            .ToArray();
    }
    
    private static string Sha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
    
    private static string FormatPath(string route, string actionName)
    {
        var path = $"{route}/{actionName}".Replace("//", "/");
        return path.EndsWith("/") ? path[..^1] : path;
    }

    /// <summary>
    /// 格式化路由模板（HttpPost("toggle-valid") / HttpGet("category/{code}/dictionaries")）
    /// 模板以 / 或 ~/ 开头时为绝对路由，不再拼接控制器前缀
    /// </summary>
    private static string FormatRoute(string routePrefix, string template)
    {
        var t = template.Trim();
        if (t.StartsWith("~/", StringComparison.Ordinal)) t = t[2..];
        if (t.StartsWith('/')) return t.TrimEnd('/');

        var path = $"{routePrefix}/{t}".Replace("//", "/");
        return path.EndsWith("/") ? path[..^1] : path;
    }

    /// <summary>
    /// 手动加载 bin 目录下所有 YZH* 和 CertPlatform* 的程序集
    /// 解决 JIT 延迟加载导致 AppDomain.CurrentDomain.GetAssemblies() 不全的问题
    /// </summary>
    private static List<Assembly> LoadAllAssemblies()
    {
        var assemblies = new List<Assembly>();
        var loadedNames = new HashSet<string>();
        
        // 先加载已经存在于 AppDomain 中的
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.IsDynamic && asm.GetName().Name != null)
            {
                assemblies.Add(asm);
                loadedNames.Add(asm.GetName().Name);
            }
        }
        
        // 手动加载 bin 目录下的所有匹配 DLL
        var binDir = AppContext.BaseDirectory;
        if (Directory.Exists(binDir))
        {
            var dllFiles = Directory.GetFiles(binDir, "*.dll")
                .Where(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    return name.StartsWith("YZH") || 
                           name.StartsWith("CertPlatform") ||
                           name.StartsWith("System.") ||
                           name.StartsWith("Microsoft.");
                });
            
            foreach (var dll in dllFiles)
            {
                try
                {
                    var asmName = Path.GetFileNameWithoutExtension(dll);
                    if (loadedNames.Contains(asmName)) continue;
                    
                    // 只加载目标程序集，跳过 .NET 运行时（已在 AppDomain 中）
                    if (asmName.StartsWith("System.") || asmName.StartsWith("Microsoft."))
                        continue;
                    
                    var assembly = Assembly.LoadFrom(dll);
                    if (assembly != null)
                    {
                        assemblies.Add(assembly);
                        loadedNames.Add(asmName);
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集（如 native DLL）
                }
            }
        }
        
        return assemblies;
    }
}
