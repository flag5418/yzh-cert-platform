using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
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

        // 注册通用仓储
        builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

        // 注册用户上下文 (获取 IP、UserId、UserName)
        builder.Services.AddScoped<IUserContext, UserContext>();

        // 注册审计日志服务
        builder.Services.AddScoped<IAuditLogger, AuditLogger>();

        // 注册实体操作服务
        builder.Services.AddScoped(typeof(EntityService<>));

        // 注册事务工作单元
        builder.Services.AddScoped<WorkUnit>();

        // 注册 GridConfig 加载器
        builder.Services.AddScoped<IGridConfigLoader, GridConfigLoader>();

        // 注册缓存管理器（使用完全限定名避免命名空间冲突）
        builder.Services.AddScoped<ICacheManager, YZH.Core.Api.Services.CacheManager>();

        // 注册字典服务（单例，跨 Controller 共享字典注册表）
        builder.Services.AddSingleton<IDictService, DictService>();

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

        // 注册内存缓存（GridConfig缓存用）
        builder.Services.AddMemoryCache();

        // 注册全局异常过滤器 + 防重复提交过滤器
        builder.Services.AddControllers(opts =>
        {
            opts.Filters.Add<GlobalExceptionFilter>();
            opts.Filters.Add<IdempotentFilter>(); // 全局防重复提交
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

        // JWT
        var enableJwt = options.EnableJwt ?? builder.Environment.IsProduction() == false;
        if (enableJwt)
        {
            ConfigureJwt(builder, options);
        }

        // Swagger
        if (options.EnableSwagger)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = options.AppName ?? "YZH Core API",
                    Version = "v1"
                });
            });
        }

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

    /// <summary>
    ///     注册 DbContext（扩展方法）
    /// </summary>
    public static IServiceCollection UseYzhDatabase<TDbContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? dbConfigure = null)
        where TDbContext : DbContext
    {
        services.AddDbContext<TDbContext>(options => dbConfigure?.Invoke(options));
        return services;
    }

    private static void ConfigureJwt(WebApplicationBuilder builder, YzhCoreOptions options)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = options.JwtIssuer ?? "yzh-core",
                    ValidAudience = options.JwtAudience ?? "yzh-app",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.JwtSecret ?? "yzh_default_secret_key"))
                };
            });
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
