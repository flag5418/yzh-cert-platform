using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Middleware;
using YZH.Core.Stand.Helpers;

namespace YZH.Core.Web;

/// <summary>
///     YZH Core Web 应用入口
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 配置服务
        ConfigureServices(builder);

        var app = builder.Build();

        // 配置 HTTP 请求管道
        ConfigurePipeline(app, builder.Environment);

        app.Run();
    }

    /// <summary>
    ///     配置服务注册
    /// </summary>
    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // 注册控制器 + 全局过滤器
        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<GlobalExceptionFilter>();       // 全局异常过滤器
            options.Filters.Add<ValidationFilter>();            // 输入验证过滤器
            options.Filters.Add<PermissionFilter>();            // 权限校验过滤器
            options.Filters.Add<YzhAuditingFilter>();           // 审计过滤器
            options.Filters.AddService<YzhAuthFilter>();        // YZH 认证过滤器（SSO + Token 续租）
        });

        // 配置 JWT 认证
        ConfigureJwtAuthentication(builder);

        // 配置 CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // 配置 Swagger
        ConfigureSwagger(builder);

        // 注册 YZH Core 框架服务（核心服务注册）
        builder.UseYzhCore();

        // 注册 Token 版本服务（SSO 挤号）
        builder.Services.AddScoped<TokenVersionService>();

        // 配置授权策略
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("YZHAuthorizePolicy", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
        });
    }

    /// <summary>
    ///     配置 JWT 认证
    /// </summary>
    private static void ConfigureJwtAuthentication(WebApplicationBuilder builder)
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]
            ?? throw new InvalidOperationException("未配置 JwtSettings:SecretKey");
        var issuer = jwtSettings["Issuer"] ?? "YZH.Core";
        var audience = jwtSettings["Audience"] ?? "YZH.Core.Client";

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        });

        builder.Services.AddAuthorization();
    }

    /// <summary>
    ///     配置 Swagger
    /// </summary>
    private static void ConfigureSwagger(WebApplicationBuilder builder)
    {
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "YZH Core API",
                Version = "v1",
                Description = "YZH Core 框架 API 文档"
            });

            // 添加 JWT 认证
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT 授权头，格式: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }

    /// <summary>
    ///     配置 HTTP 请求管道
    /// </summary>
    private static void ConfigurePipeline(WebApplication app, IWebHostEnvironment env)
    {
        // 全局异常处理中间件（统一使用 Api 层版本）
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // 启用 CORS
        app.UseCors("AllowAll");

        // 启用 HTTPS 重定向
        app.UseHttpsRedirection();

        // 启用静态文件
        app.UseStaticFiles();

        // 启用路由
        app.UseRouting();

        // 启用认证
        app.UseAuthentication();

        // 启用授权
        app.UseAuthorization();

        // 启用 Swagger（仅开发环境）
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "YZH Core API v1");
                c.RoutePrefix = "swagger";
            });
        }

        // 启用控制器路由
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
