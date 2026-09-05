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
using YZH.Core.DataBase;
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

        // 注册通用仓储
        builder.Services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

        // 注册内存缓存（GridConfig缓存用）
        builder.Services.AddMemoryCache();

        // 注册全局异常过滤器
        builder.Services.AddControllers(opts =>
        {
            opts.Filters.Add<GlobalExceptionFilter>();
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
