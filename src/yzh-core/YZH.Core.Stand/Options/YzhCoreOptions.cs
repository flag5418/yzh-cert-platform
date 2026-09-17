namespace YZH.Core.Stand.Options;

/// <summary>
///     YZH Core 核心配置项
/// </summary>
public class YzhCoreOptions
{
    public string? AppName { get; set; }
    public string? JwtIssuer { get; set; }
    public string? JwtAudience { get; set; }
    public string? JwtSecret { get; set; }
    public bool EnableSwagger { get; set; } = true;
    public bool? EnableJwt { get; set; }

    /// <summary>
    ///     核心模块 EntityConfig 目录（默认 YZH.Core.Web/Assets/EntityConfigs）
    /// </summary>
    public string? CoreEntityConfigPath { get; set; }

    /// <summary>
    ///     业务模块 EntityConfig 目录列表（支持 Admin/Auditor/Enterprise 等项目）
    /// </summary>
    public List<string>? BusinessEntityConfigPaths { get; set; }
}
