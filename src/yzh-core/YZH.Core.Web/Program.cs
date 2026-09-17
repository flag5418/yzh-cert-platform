using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text;
using YZH.Core.Api.Services;
using YZH.Core.Web;
using YZH.Core.DataBase.Services;
using CertPlatform.Admin;

var builder = WebApplication.CreateBuilder(args);

// 使用 YZH Core 框架注册核心服务
builder.UseYzhCore(options =>
{
    options.EnableSwagger = true;
    // 核心模块配置目录（YZH.Core.Web 自有：User、Role、Menu、SysConfig 等）
    options.CoreEntityConfigPath = Path.Combine(builder.Environment.ContentRootPath, "Assets", "EntityConfigs");
    // 业务模块配置目录（CertPlatform.Admin 的 EntityConfig）
    var adminAssetsPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "certplatform-api", "CertPlatform.Admin", "Assets", "EntityConfigs"));
    options.BusinessEntityConfigPaths = new List<string> { adminAssetsPath };
});

// 业务服务自注册（启动工程不感知具体业务实现）
builder.Services.AddCertPlatformAdminServices();

// 注册队列相关（QueueManager 是单例，executor/notifier/handler 必须也是单例）
// OfficeConvertTaskExecutor、CertQueueNotifier、UploadQueueCancelHandler 的注册
// 已通过 AddCertPlatformAdminServices() 统一注册

// 读取 JWT 配置
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSettings["SecretKey"] ?? "AA3627441FFA4B5DB4E64A29B53CE525";
var jwtIssuer = jwtSettings["Issuer"] ?? "YZH.Core";
var jwtAudience = jwtSettings["Audience"] ?? "YZH.Core.Client";

// 注册 JWT 认证
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 注册 Authorization 策略
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("YZHAuthorizePolicy", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
});

// 注册 MVC 控制器 + 全局过滤器
builder.Services.AddControllers(options =>
{
    options.Filters.Add<YZH.Core.Api.Filters.GlobalExceptionFilter>();
    options.Filters.Add<YZH.Core.Api.Filters.PermissionFilter>();
})
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.Workflow.StandardDirectoryController).Assembly)
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.Workflow.WorkflowTestController).Assembly)
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.Foundation.DirectoryTemplateController).Assembly)
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.System.QueueMonitorController).Assembly)
.AddJsonOptions(json =>
{
    // PascalCase 序列化（与实体属性名一致）
    json.JsonSerializerOptions.PropertyNamingPolicy = null;
    // 枚举序列化为字符串（与前端 ControlType 名称一致：TextBox/ComboBox/Switch...）
    json.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    json.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    json.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

// 注册 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册 CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:9990", "http://localhost:9991" };
builder.Services.AddCors(cors =>
{
    cors.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// CORS 必须在 Swagger 之前：否则静态文档不返回 CORS 头，
// 前端（9990）无法读取 swagger.json 去构建接口深链。
app.UseCors();

// 开发环境启用 Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // 允许通过 #/分组/操作 定位到具体接口（接口管理页的「测试」按钮依赖此开关）
        options.EnableDeepLinking();
    });
}

// 认证中间件（必须在 UseAuthorization 之前）
app.UseAuthentication();
app.UseAuthorization();

// 权限同步启动时扫描
using (var scope = app.Services.CreateScope())
{
    var apiSync = scope.ServiceProvider.GetRequiredService<ApiSyncService>();
    var scanner = scope.ServiceProvider.GetRequiredService<ApiScanner>();
    var apis = scanner.Scan();
    await apiSync.SyncAsync(apis);
}

app.MapControllers();

// 临时测试端点：验证 routing pipeline 是否工作
app.MapGet("/api/test/ping", () => "pong");

app.Run();
