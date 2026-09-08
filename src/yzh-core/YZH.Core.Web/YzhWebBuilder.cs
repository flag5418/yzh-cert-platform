using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Services;
using YZH.Core.DataBase;
using YZH.Core.DataBase.NoSql;
using YZH.Core.Stand.Models;
using YZH.Core.Web.Middlewares;

namespace YZH.Core.Web;

/// <summary>
///     YZH Web 应用构建扩展
/// </summary>
public static class YzhWebBuilderExtensions
{
    /// <summary>
    ///     使用 YZH Core 框架配置服务
    /// </summary>
    public static WebApplicationBuilder UseYzhCore(
        this WebApplicationBuilder builder,
        Action<YzhCoreOptions>? configure = null)
    {
        var options = new YzhCoreOptions();
        configure?.Invoke(options);

        // 注册 HttpContextAccessor（UserContext 获取 IP 必须）
        builder.Services.AddHttpContextAccessor();

        // 注册 Dapper 数据库操作（唯一数据库组件）
        builder.Services.AddScoped<IDbOrm, DapperDbOrm>();

        // IRepository<BaseRepository> 已废弃（EF Core 实现），新架构使用 Dapper
        // 如需启用，请先EF Core 注册DbContext

        // 注册用户上下文 (获取 IP、UserId、UserName)
        builder.Services.AddScoped<IUserContext, UserContext>();

        // 注册审计日志服务（新版 IYzhAuditLogger + YzhAuditLogger 后台服务）
        builder.Services.AddHostedService<YzhAuditLogger>();
        builder.Services.AddScoped<IYzhAuditLogger, YzhAuditLogger>();

        // 注册实体操作服务
        builder.Services.AddScoped(typeof(EntityService<>));

        // 注册 EntityConfig 加载器
        builder.Services.AddScoped<IEntityConfigLoader, EntityConfigLoader>();

        // 注册密码工具（使用 PasswordSecret 配置）
        var passwordSecret = builder.Configuration["PasswordSecret"] ?? "C5ABA9E202D94C43A3CA66002BF77FAF";
        builder.Services.AddSingleton<YZH.Core.Stand.Helpers.PasswordHelper>(_ => new YZH.Core.Stand.Helpers.PasswordHelper(passwordSecret));

        // 注册 JWT 工具
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        builder.Services.AddSingleton<YZH.Core.Stand.Helpers.JwtHelper>(_ =>
        {
            var options = new YZH.Core.Stand.Helpers.JwtOptions
            {
                Issuer = jwtSettings["Issuer"] ?? "vol.core.owner",
                Audience = jwtSettings["Audience"] ?? "vol.core",
                SecretKey = jwtSettings["SecretKey"] ?? "AA3627441FFA4B5DB4E64A29B53CE525",
                ExpirationMinutes = 43200 // 30天
            };
            return new YZH.Core.Stand.Helpers.JwtHelper(options);
        });

        // 注册缓存管理器（使用完全限定名避免命名空间冲突）
        builder.Services.AddScoped<ICacheManager, YZH.Core.Api.Services.CacheManager>();

        // 注册字典服务（Scoped：ICacheManager 是 Scoped，不能从 Singleton 消费 Scoped）
        // 注意：缓存实际存储在 INoSql（Redis）中，Scoped 生命周期不影响缓存效果
        builder.Services.AddScoped<IDictService, DictService>();

        // 注册轻量级 SQL 查询服务（Dapper + 多数据库方言）
        builder.Services.AddScoped<ISqlBaseService, SqlBaseService>();

        // 注册 NoSQL 工厂
        var noSqlOptions = new NoSqlOptions();
        builder.Services.AddSingleton(noSqlOptions);
        builder.Services.AddSingleton<INoSqlFactory>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new NoSqlFactory(noSqlOptions, loggerFactory);
        });
        builder.Services.AddScoped<INoSql>(sp =>
        {
            var factory = sp.GetRequiredService<INoSqlFactory>();
            return factory.DefaultProvider;
        });

        // 注册内存缓存（EntityConfig缓存用）
        builder.Services.AddMemoryCache();

        // 注册验证码服务
        builder.Services.AddScoped<ICaptchaService, CaptchaService>();

        // 注册全局过滤器
        builder.Services.AddControllers(opts =>
        {
            opts.Filters.Add<GlobalExceptionFilter>();
            opts.Filters.Add<IdempotentFilter>(); // 全局防重复提交
            opts.Filters.Add<YzhAuditingFilter>(); // 全局审计
        })
        .AddJsonOptions(json =>
        {
            json.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            json.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // CORS
        builder.Services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        // JWT 注意：不在 UseYzhCore 中注册，由 Program.cs 统一配置（避免 Scheme 重复）
        // 如需在 UseYzhCore 中注册，请删除 Program.cs 中的 ConfigureJwtAuthentication 调用

        // Swagger 注意：不在 UseYzhCore 中注册，由 Program.cs 统一配置（避免 SwaggerDoc 重复）

        return builder;
    }

    /// <summary>
    ///     配置请求管道
    /// </summary>
    public static WebApplication UseYzhPipeline(this WebApplication app, bool useCors = true)
    {
        // 全局异常处理
        app.UseMiddleware<GlobalExceptionMiddleware>();

        if (useCors)
        {
            app.UseCors("AllowAll");
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Swagger
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();
        return app;
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
