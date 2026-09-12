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
                    
                    // 处理重复 RouteAttribute（如基类+子类都有），取最后一个（子类的）
                    var routeAttrs = type.GetCustomAttributes(typeof(RouteAttribute), false).OfType<RouteAttribute>().ToList();
                    var controllerName = type.Name.Replace("Controller", "");
                    var routePrefix = (routeAttrs.LastOrDefault()?.Template ?? "")
                        .Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);
                    
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.DeclaringType == type);
                    
                    foreach (var method in methods)
                    {
                        try
                        {
                            var httpMethodAttr = method.GetCustomAttributes(false)
                                .OfType<HttpMethodAttribute>()
                                .FirstOrDefault();
                            
                            if (httpMethodAttr == null) continue;
                            
                            // HttpMethodAttribute.HttpMethods 是 IEnumerable<string>，取第一个
                            var httpMethod = httpMethodAttr.HttpMethods.FirstOrDefault()?.ToUpper() ?? "GET";
                            
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
                            
                            var descAttr = method.GetCustomAttribute<ApiDescriptionAttribute>();
                            
                            apis.Add(new ApiDescriptor
                            {
                                ApiCode = apiCode,
                                ControllerName = type.Name.Replace("Controller", ""),
                                ActionName = method.Name,
                                Method = httpMethod,
                                Path = FormatPath(routePrefix, method.Name),
                                TreePath = ParseTreePath(FormatPath(routePrefix, method.Name)),
                                Description = descAttr?.Description ?? "",
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
