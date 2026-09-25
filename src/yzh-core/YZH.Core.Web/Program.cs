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
using CertPlatform.Auditor;

var builder = WebApplication.CreateBuilder(args);

// 使用 YZH Core 框架注册核心服务
builder.UseYzhCore(options =>
{
    options.EnableSwagger = true;
    // 核心模块配置目录（YZH.Core.Web 自有：User、Role、Menu、SysConfig 等）
    options.CoreEntityConfigPath = Path.Combine(builder.Environment.ContentRootPath, "Assets", "EntityConfigs");
    // 业务模块配置目录（各业务模块自有的 EntityConfig）
    // ⚠️⚠️ 新增业务模块时必须在此追加其 Assets/EntityConfigs 目录 —— 漏列不会报任何错，
    //     但该模块的 EntityConfigHelper.GetConfig 会静默落到 NewEmptyConfig：
    //     症状 = Title 等于实体类型名、Columns=[]、页面「有数据行却一列都不显示」。
    //     （2026-09-25 实测：Auditor 的 Enterprise.json 因此未被加载）
    // 搜索顺序 = 本列表顺序，同名文件先命中者生效；目录不存在会被自动跳过。
    var bizRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "certplatform-api"));
    options.BusinessEntityConfigPaths = new List<string>
    {
        Path.Combine(bizRoot, "CertPlatform.Admin", "Assets", "EntityConfigs"),
        Path.Combine(bizRoot, "CertPlatform.Auditor", "Assets", "EntityConfigs"),
    };
});

// 业务服务自注册（启动工程不感知具体业务实现）
builder.Services.AddCertPlatformAdminServices();
// 专家端（专家注册 / 专家登录）业务服务
builder.Services.AddCertPlatformAuditorServices();

// 注册审计日志数据库写入初始化服务（在启动时设置 IYzhAuditLogger.DbWriter）
builder.Services.AddSingleton<IHostedService, AuditLogDbWriterInitializer>();

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
    options.Filters.Add<YZH.Core.Api.Filters.YzhAuditingFilter>();
    // ★ B-R4（信封统一改造 P3）：放行前校验 ApiResponse 三条不变量，违规即抛（防回潮绊线）
    options.Filters.Add<YZH.Core.Api.Filters.ApiResponseContractFilter>();
})
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.Workflow.StandardDirectoryController).Assembly)
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.Workflow.WorkflowTestController).Assembly)
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.Foundation.DirectoryTemplateController).Assembly)
.AddApplicationPart(typeof(CertPlatform.Admin.Controllers.System.QueueMonitorController).Assembly)
// 专家端控制器（api/AuditorAuth/*）
.AddApplicationPart(typeof(CertPlatform.Auditor.Controllers.AuditorAuthController).Assembly)
.AddJsonOptions(json =>
{
    // PascalCase 序列化（与实体属性名一致）
    json.JsonSerializerOptions.PropertyNamingPolicy = null;
    // 枚举序列化为字符串（与前端 ControlType 名称一致：TextBox/ComboBox/Switch...）
    json.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    json.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    json.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    // 敏感字段脱敏：带 [YzhSensitive] 的属性永不写出（输入绑定不受影响）
    // 详见 YZH.Core.Stand/Extensions/JsonSensitiveFieldExtensions.cs
    YZH.Core.Stand.Extensions.JsonSensitiveFieldExtensions
        .ApplySensitiveFieldMasking(json.JsonSerializerOptions);
});

// 22 §三：模型校验失败（[Required] / JSON 绑定失败）= 业务拒绝 → HTTP 200 + success:false + err 非空。
// 不配这里的话，[ApiController] 会自动回 400 + RFC7807 ValidationProblemDetails，
// 既没有 success/err 字段，也与「业务失败一律 HTTP 200」相悖（P1 收口）。
// 保留自动校验过滤器本身（SuppressModelStateInvalidFilter 仍为 false）——只替换响应工厂。
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        static string Friendly(string msg)
        {
            // DataAnnotations 默认模板是英文（"The 所属字典 field is required."）→ 转中文
            if (msg.StartsWith("The ") && msg.EndsWith(" field is required."))
                return msg[4..^19] + " 不能为空";
            return msg;
        }

        var errors = ctx.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .SelectMany(kv => kv.Value!.Errors.Select(e =>
                string.IsNullOrWhiteSpace(e.ErrorMessage) ? kv.Key : e.ErrorMessage))
            .Distinct()
            .ToList();

        var err = errors.Count > 0
            ? string.Join("；", errors.Select(Friendly))
            : "参数校验失败";

        return new Microsoft.AspNetCore.Mvc.OkObjectResult(
            YZH.Core.Stand.Models.Result.ApiResponse.Fail(err));
    };
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

// ── 业务实体命名自检（启动期强校验：违规直接抛异常，阻止服务启动）──
// 规则来源：YZH.Core.Stand/BizConventions/BizNamingRules.cs
// 双重黑名单 = 静态清单 + 框架程序集动态推导（框架新增实体时无需再维护静态清单）
{
    // CertPlatform.Admin / Auditor 已由上方 AddApplicationPart 强制加载；
    // CertPlatform.Shared 作为其依赖一并加载（注意：该程序集挂了 extern alias，不能直接 typeof 引用）
    var bizAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name is "CertPlatform.Shared" or "CertPlatform.Admin"
                                             or "CertPlatform.Auditor" or "CertPlatform.Enterprise")
        .ToArray();

    var frameworkAssemblies = new[]
    {
        typeof(YZH.Core.Api.Controllers.YzhControllerBase<>).Assembly,
        typeof(YZH.Core.Stand.BizConventions.BizNamingRules).Assembly,
    };

    var checkedTypes = YZH.Core.Stand.BizConventions.BizNamingRules
        .ValidateWithFrameworkGuard(bizAssemblies, frameworkAssemblies);

    Console.WriteLine($"[YZH] 业务实体命名自检通过：校验 {checkedTypes} 个类型，"
                    + $"框架黑名单 {frameworkAssemblies.Length} 个程序集。");
}

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
