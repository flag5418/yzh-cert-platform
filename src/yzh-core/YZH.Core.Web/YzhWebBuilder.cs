using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Interfaces;
using YZH.Core.Api.Repositories;
using YZH.Core.Api.Services;
using YZH.Core.DataBase;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.MultiDatabase;
using YZH.Core.Stand.Helpers;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Result;
using YZH.Core.Stand.NoSql;
using YZH.Core.Stand.Options;
using YZH.Core.Web.Extensions;

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

        // 注册 YzhCoreOptions 以供依赖注入使用
        builder.Services.Configure<YzhCoreOptions>(opts =>
        {
            opts.AppName = options.AppName;
            opts.JwtIssuer = options.JwtIssuer;
            opts.JwtAudience = options.JwtAudience;
            opts.JwtSecret = options.JwtSecret;
            opts.EnableSwagger = options.EnableSwagger;
            opts.EnableJwt = options.EnableJwt;
            opts.CoreEntityConfigPath = options.CoreEntityConfigPath;
            opts.BusinessEntityConfigPaths = options.BusinessEntityConfigPaths;
        });

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

        // 注册角色查询服务（封装跨表 SQL，从 Controller 中抽取）
        builder.Services.AddScoped<IRoleService, RoleService>();

        // 注册菜单权限服务（按角色过滤可见菜单）
        builder.Services.AddScoped<MenuPermissionService>();

        // 注册密码工具（使用 PasswordSecret 配置）
        var passwordSecret = builder.Configuration["PasswordSecret"];
        if (string.IsNullOrWhiteSpace(passwordSecret) || passwordSecret.Length < 32)
            throw new InvalidOperationException("PasswordSecret 必须在 appsettings.json 中配置，且长度不得小于 32 字符");
        builder.Services.AddSingleton<YZH.Core.Stand.Helpers.PasswordHelper>(_ => new YZH.Core.Stand.Helpers.PasswordHelper(passwordSecret));

        // 注册 JWT 工具
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var jwtSecret = jwtSettings["SecretKey"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
            throw new InvalidOperationException("JwtSettings:SecretKey 必须在 appsettings.json 中配置，且长度不得小于 32 字符");
        var jwtIssuer = jwtSettings["Issuer"] ?? "YZH.Core";
        var jwtAudience = jwtSettings["Audience"] ?? "YZH.Core.Client";
        var jwtExpiration = int.TryParse(jwtSettings["ExpirationMinutes"], out var exp) ? exp : 43200;
        builder.Services.AddSingleton<YZH.Core.Stand.Helpers.JwtHelper>(_ =>
        {
            var jwtOptions = new YZH.Core.Stand.Helpers.JwtOptions
            {
                Issuer = jwtIssuer,
                Audience = jwtAudience,
                SecretKey = jwtSecret,
                ExpirationMinutes = jwtExpiration
            };
            return new YZH.Core.Stand.Helpers.JwtHelper(jwtOptions);
        });

        // 注册 INoSql 实现（内存缓存版，生产环境可替换为 Redis）
        builder.Services.AddScoped<INoSql, MemoryCacheNoSql>();

        // 注册 IDistributedCache（TokenVersionService 使用，开发环境用内存版）
        builder.Services.AddDistributedMemoryCache();

        // 注册 TokenVersionService（JWT 版本校验/SSO 挤号核心）
        builder.Services.AddScoped<TokenVersionService>();

        // 注册缓存管理器
        builder.Services.AddScoped<ICacheManager, YZH.Core.Api.Services.CacheManager>();

        // 注册字典服务（Scoped：ICacheManager 是 Scoped，不能从 Singleton 消费 Scoped）
        builder.Services.AddScoped<IDictService, DictService>();

        // 注册内存缓存（EntityConfig + 验证码 + 数据缓存用）
        builder.Services.AddMemoryCache();

        // 初始化 EntityConfigHelper 多目录路径（静态类，供 YzhControllerBase 使用）
        var corePath = options.CoreEntityConfigPath
            ?? Path.Combine(builder.Environment.ContentRootPath, "Assets", "EntityConfigs");
        EntityConfigHelper.SetCoreConfigDir(corePath);
        if (options.BusinessEntityConfigPaths != null)
        {
            EntityConfigHelper.SetBusinessConfigDirs(options.BusinessEntityConfigPaths);
        }

        // 注册 EntityConfig 加载器（支持核心模块 + 业务模块多目录）
        builder.Services.AddSingleton<IEntityConfigLoader>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<YzhCoreOptions>>().Value;
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<EntityConfigLoader>>();
            return new EntityConfigLoader(cache, logger, opts);
        });

        // 注册对象存储（IObjectStorage：MinIO / 阿里 OSS，配置驱动）
        builder.Services.AddYzhStorage(builder.Configuration);

        // 注册队列引擎（QueueManager + QueueHostedService）
        builder.Services.AddYzhQueue();

        // 注册验证码服务
        builder.Services.AddScoped<ICaptchaService, CaptchaService>();

        // 注册 YZH 认证过滤器
        builder.Services.AddScoped<YZH.Core.Api.Filters.YzhAuthFilter>();

        // 注册接口权限相关服务
        builder.Services.AddScoped<IApiRepository, ApiRepository>();
        builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();
        builder.Services.AddScoped<IPermissionService, PermissionService>();
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
