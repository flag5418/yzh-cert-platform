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
using Microsoft.OpenApi.Models;
using YZH.Core.Api.Exceptions;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Middleware;
using YZH.Core.Api.Services;
using YZH.Core.DataBase;
using YZH.Core.DataBase.NoSql;
using YZH.Core.Api.Attributes;
using YZH.Core.Stand.Helpers;
using YZH.Core.Web.Middlewares;

namespace YZH.Core.Web;

/// <summary>
/// YZH Core Web 应用构建器
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // 配置服务
        builder.Services.AddControllers(options =>
        {
            // 注册全局异常过滤器
            options.Filters.Add<GlobalExceptionFilter>();
            
            // 注册输入验证过滤器
            options.Filters.Add<ValidationFilter>();
            
            // 注册权限校验过滤器
            options.Filters.Add<PermissionFilter>();
            
            // 注册审计过滤器
            options.Filters.Add<YzhAuditingFilter>();
            
            // 注册 YZH 认证过滤器（SSO 校验 + Token 续租）
            options.Filters.AddService<YzhAuthFilter>();
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
        
        // 注册 YZH Core 服务
        builder.UseYzhCore();

        // 注册 Token 版本服务（SSO 挤号）
        builder.Services.AddScoped<TokenVersionService>();

        // 配置授权策略（认证特性使用）
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("YZHAuthorizePolicy", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });
        });
        
        var app = builder.Build();
        
        // 配置 HTTP 请求管道
        ConfigurePipeline(app, builder.Environment);
        
        app.Run();
    }
    
    /// <summary>
    /// 配置 JWT 认证
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
                ValidateLifetime = true, // Token 用于生产环境启用过期校验
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
    /// 配置 HTTP 请求管道
    /// </summary>
    private static void ConfigurePipeline(WebApplication app, IWebHostEnvironment env)
    {
        // 使用全局异常处理
        app.UseMiddleware<YZH.Core.Web.Middlewares.GlobalExceptionMiddleware>();
        
        // 使用 YZH Core 管道
        app.UseYzhPipeline();
        
        // 启用 HTTPS 重定向
        app.UseHttpsRedirection();
        
        // 启用 CORS
        app.UseCors("AllowAll");
        
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
