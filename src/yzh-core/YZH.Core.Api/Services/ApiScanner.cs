using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
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
        
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic 
                     && (a.GetName().Name?.StartsWith("YZH") == true 
                         || a.GetName().Name?.StartsWith("CertPlatform") == true));
        
        _logger.LogInformation("开始扫描 {Count} 个程序集", assemblies.Count());
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (!typeof(ControllerBase).IsAssignableFrom(type) 
                        || type.IsAbstract 
                        || type.IsGenericType)
                        continue;
                    
                    var routeAttr = type.GetCustomAttribute<RouteAttribute>();
                    var routePrefix = routeAttr?.Template ?? "";
                    
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.DeclaringType == type);
                    
                    foreach (var method in methods)
                    {
                        var httpMethodAttr = method.GetCustomAttributes(false)
                            .OfType<HttpMethodAttribute>()
                            .FirstOrDefault();
                        
                        if (httpMethodAttr == null) continue;
                        
                        var apiCode = GenerateApiCode(
                            routePrefix,
                            httpMethodAttr.HttpMethod.ToUpper(),
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
                            Method = httpMethodAttr.HttpMethod.ToUpper(),
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描程序集 {Assembly} 失败", assembly.GetName().Name);
            }
        }
        
        stopwatch.Stop();
        _logger.LogInformation("扫描完成，发现 {Count} 个接口，耗时 {Elapsed}ms", 
            apis.Count, stopwatch.ElapsedMilliseconds);
        
        return apis;
    }
    
    private string GenerateApiCode(string route, string httpMethod, string actionName, ParameterInfo[] parameters)
    {
        var sortedParams = parameters
            .OrderBy(p => p.Name ?? "")
            .Select(p => $"{p.Name}:{p.ParameterType.Name}")
            .Join("|");
        
        var input = $"{route.ToLower()}|{httpMethod}|{actionName.ToLower()}|{sortedParams}";
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
}
