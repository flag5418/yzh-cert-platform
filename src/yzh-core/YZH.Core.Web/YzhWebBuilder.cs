using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Interfaces;
using YZH.Core.Api.Repositories;
using YZH.Core.Api.Services;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.MultiDatabase;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.NoSql;

namespace YZH.Core.Web;

/// <summary>
///     YZH Web 应用构建扩展
///     负责注册 YZH Core 框架核心服务
/// </summary>
public static class YzhWebBuilderExtensions
{
    /// <summary>
    ///     使用 YZH Core 框架配置核心服务
    ///     注意：JWT、CORS、Swagger 等基础配置在 Program.cs 中完成
    /// </summary>
    public static WebApplicationBuilder UseYzhCore(
        this WebApplicationBuilder builder,
        Action<YzhCoreOptions>? configure = null)
    {
        var options = new YzhCoreOptions();
        configure?.Invoke(options);

        // 注册 HttpContextAccessor（UserContext 获取 IP 必须）
        builder.Services.AddHttpContextAccessor();

        // 注册多数据库配置
        builder.Services.Configure<MultiDatabaseOptions>(
            builder.Configuration.GetSection("DatabaseConfigs"));

        // 注册多数据库工厂（单例：内部缓存 SqlSugarClient 实例）
        builder.Services.AddSingleton<IDbContextFactory, DbContextFactory>();

        // 注册 SqlSugar 数据库操作（通过工厂获取默认数据库）
        builder.Services.AddScoped<IDbOrm>(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory>();
            return factory.GetDefault();
        });

        // 注册用户上下文 (获取 IP、UserId、UserName)
        builder.Services.AddScoped<IUserContext, UserContext>();

        // 注册审计日志服务（IYzhAuditLogger + YzhAuditLogger 后台服务）
        builder.Services.AddHostedService<YzhAuditLogger>();
        builder.Services.AddScoped<IYzhAuditLogger, YzhAuditLogger>();

        // 注册实体操作服务
        builder.Services.AddScoped(typeof(EntityService<>));

        // 注册菜单权限服务（按角色过滤可见菜单）
        builder.Services.AddScoped<MenuPermissionService>();

        // 注册密码工具（使用 PasswordSecret 配置）
        var passwordSecret = builder.Configuration["PasswordSecret"] ?? "C5ABA9E202D94C43A3CA66002BF77FAF";
        builder.Services.AddSingleton<YZH.Core.Stand.Helpers.PasswordHelper>(_ => new YZH.Core.Stand.Helpers.PasswordHelper(passwordSecret));

        // 注册 JWT 工具
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        builder.Services.AddSingleton<YZH.Core.Stand.Helpers.JwtHelper>(_ =>
        {
            var jwtOptions = new YZH.Core.Stand.Helpers.JwtOptions
            {
                Issuer = jwtSettings["Issuer"] ?? "vol.core.owner",
                Audience = jwtSettings["Audience"] ?? "vol.core",
                SecretKey = jwtSettings["SecretKey"] ?? "AA3627441FFA4B5DB4E64A29B53CE525",
                ExpirationMinutes = 43200 // 30天
            };
            return new YZH.Core.Stand.Helpers.JwtHelper(jwtOptions);
        });

        // 注册 INoSql 实现（内存缓存版，生产环境可替换为 Redis）
        builder.Services.AddScoped<INoSql, MemoryCacheNoSql>();

        // 注册 IDistributedCache（TokenVersionService 使用，开发环境用内存版）
        builder.Services.AddDistributedMemoryCache();

        // 注册缓存管理器
        builder.Services.AddScoped<ICacheManager, YZH.Core.Api.Services.CacheManager>();

        // 注册字典服务（Scoped：ICacheManager 是 Scoped，不能从 Singleton 消费 Scoped）
        builder.Services.AddScoped<IDictService, DictService>();

        // 注册内存缓存（EntityConfig + 验证码 + 数据缓存用）
        builder.Services.AddMemoryCache();

        // 注册验证码服务
        builder.Services.AddScoped<ICaptchaService, CaptchaService>();

        // 注册 YZH 认证过滤器
        builder.Services.AddScoped<YZH.Core.Api.Filters.YzhAuthFilter>();

        // 注册接口权限相关服务
        builder.Services.AddScoped<IApiRepository, ApiRepository>();
        builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();
        builder.Services.AddScoped<ApiScanner>();
        builder.Services.AddScoped<ApiSyncService>();

        // 注册 JSON 序列化选项（Configure 与 Program.cs 中已注册的 AddControllers 合并）
        builder.Services.Configure<JsonOptions>(json =>
        {
            // 使用 PascalCase（与数据库列名、实体属性名一致）
            // 这样 JSON 字段名 = 实体属性名 = 数据库列名
            json.JsonSerializerOptions.PropertyNamingPolicy = null;
            json.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            json.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return builder;
    }
}

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
}
