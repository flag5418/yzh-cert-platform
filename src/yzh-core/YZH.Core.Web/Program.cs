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
using CertPlatform.Shared;

// ─────────────────────────────────────────────────────────────────────────────
// ★★★ 必须最先执行：把「本机地址」排除出代理（2026-10-06 实测根治）
//
// 机理：.NET 在 Unix 上 `HttpClient.DefaultProxy` 会读 `HTTP_PROXY`/`HTTPS_PROXY` 环境变量。
//       MinIO 的 SDK 走 HttpClient ⇒ **本机 9000 的请求会被送去代理**。
// 后果：开发机常年开代理，且**代理端口每次会话都变**；后端一旦继承了**旧会话**的代理端口，
//       那个端口早已不存在 ⇒ **所有 MinIO 读写失败**（预览 / 下载 / 转换产物 / Markdown 提取 /
//       语义分析全线），而对外报的却是「文件不存在」「源文件读取失败（对象不存在或存储不可用）」
//       —— **症状与病因完全错位**，看起来像数据缺失，实际是网络层，极难定位。
//
// ⚠️ 只**追加**，⛔ 绝不覆盖或清空 —— 本机 LLM 调用（通义千问等）**需要代理出网**，
//    清掉会让语义分析直接不可用。
// ⚠️ 必须放在**任何 HttpClient 被创建之前**（`HttpClient.DefaultProxy` 是惰性初始化，
//    首次访问即定稿）⇒ 故置于文件最前。
// ⚠️ 这里是「按进程生效」；`scripts/backend/run-backend.sh` 里也有一份，属双保险。
// ─────────────────────────────────────────────────────────────────────────────
{
    var existing = Environment.GetEnvironmentVariable("NO_PROXY")
                   ?? Environment.GetEnvironmentVariable("no_proxy") ?? "";
    var parts = existing
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();
    foreach (var host in new[] { "127.0.0.1", "localhost", "::1", "0.0.0.0" })
        if (!parts.Contains(host, StringComparer.OrdinalIgnoreCase))
            parts.Add(host);
    var merged = string.Join(",", parts);
    Environment.SetEnvironmentVariable("NO_PROXY", merged);
    Environment.SetEnvironmentVariable("no_proxy", merged);
}

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
        // ★ 2026-10-06 新增，**必须放首位**：共用实体的 EntityConfig 以 Shared 为唯一权威。
        //   放首位后，若 Admin/Auditor 里还留着同名副本，会**先命中 Shared 的那份** ⇒ 副本失效（但不报错）。
        //   ⇒ 下沉实体时必须**同步删掉端内的同名 JSON**，否则两份 = 静默分叉。
        Path.Combine(bizRoot, "CertPlatform.Shared", "Assets", "EntityConfigs"),
        Path.Combine(bizRoot, "CertPlatform.Admin", "Assets", "EntityConfigs"),
        Path.Combine(bizRoot, "CertPlatform.Auditor", "Assets", "EntityConfigs"),
        // ⛔ 缺 CertPlatform.Enterprise —— 该端未开工（见下方 AddCertPlatformEnterpriseServices 处的说明）。
        //    企业端开工时必须在此加一行，否则该端 EntityConfig 静默空配置。
    };
});

// 业务服务自注册（启动工程不感知具体业务实现）
// ⚠️ 顺序敏感：Shared **必须最前**。AddScoped 是「后注册覆盖先注册」的语义 ——
//    若 Admin 也注册了同名服务，**Admin 的会赢**，Shared 那份就等于没注册（静默分叉）。
//    ⇒ 下沉服务后必须**同步删掉 Admin 侧的重复注册**。
builder.Services.AddCertPlatformSharedServices();
builder.Services.AddCertPlatformAdminServices();
// 专家端（专家注册 / 专家登录）业务服务
builder.Services.AddCertPlatformAuditorServices();

// ⛔⛔ 企业端 **未开工**，此处刻意不调 AddCertPlatformEnterpriseServices() ⛔⛔
//
// 背景（2026-10-03 架构盘点发现）：`CertPlatform.Enterprise` 是一个 **100% 空壳项目** ——
//   Controllers/ Services/ Entities/ 三个目录全空，全项目只有 .csproj + .DS_Store。
//   但 `YZH.Core.Web.csproj` 引用了它，且 `Program.cs` 里既无 `AddApplicationPart`、
//   也无 `BusinessEntityConfigPaths` 条目 ⇒ **一旦有人往里加 Controller，运行时静默不加载**
//   （页面点开白屏，既无编译错误也无启动错误）。
//
// ⚠️ 这正是「后台/专家/企业三端已分层」的假象来源。开工前必须三件事一起做：
//   ① 本处加 AddCertPlatformEnterpriseServices();
//   ② 下方 AddApplicationPart 加 typeof(CertPlatform.Enterprise.Controllers.XxxController).Assembly;
//   ③ 上方 BusinessEntityConfigPaths 加 Enterprise/Assets/EntityConfigs;
//   缺任何一条 = 该端接口 404 / EntityConfig 静默空（标题=类型名、列全不显示）。
//
// 判定「未开工」的口径：企业端目录下出现第一个 Controller 文件时，本注释必须同步删除。

// 注册审计日志数据库写入初始化服务（在启动时设置 IYzhAuditLogger.DbWriter）
builder.Services.AddSingleton<IHostedService, AuditLogDbWriterInitializer>();

// 注册队列相关（QueueManager 是单例，executor/notifier/handler 必须也是单例）
// OfficeConvertTaskExecutor、CertQueueNotifier、UploadQueueCancelHandler 的注册
// 已通过 AddCertPlatformAdminServices() 统一注册

// 读取 JWT 配置
// ⛔ 密钥只允许来自 appsettings.json（已被 .gitignore 忽略）或环境变量；
//    代码内禁止任何密钥字面量，配置缺失时直接启动失败（缺失即失败 > 静默用弱默认值）。
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException(
        "JwtSettings:SecretKey 未配置。请在 src/yzh-core/YZH.Core.Web/appsettings.json " +
        "或环境变量 JwtSettings__SecretKey 中提供（⛔ 不要把密钥写进代码）。");
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
// ⛔ 缺 CertPlatform.Enterprise —— 该端未开工。开工时必须加 .AddApplicationPart(...)，
//    否则该端 Controller 运行时不被发现 ⇒ 路由 404 且零启动错误。
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
builder.Services.AddSwaggerGen(options =>
{
    // 使用完整类型路径作为 schema ID，避免同名内部类冲突
    options.CustomSchemaIds(type => type.FullName!);
});

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

// yzh 实时推送 Hub（框架核心能力：队列终态等服务端事件 → 浏览器；身份 = JWT access_token，
// 见 YzhMessageHub —— 挂在 /api 前缀下以复用既有 dev/prod 的 /api 代理转发）
app.MapHub<YZH.Core.Web.Hubs.YzhMessageHub>("/api/yzh-msg");

// yzh 上传进度 Hub（框架核心能力：文件上传实时进度推送；前端 subscribe/unsubscribe 生命周期管理）
app.MapHub<YZH.Core.Web.Hubs.UploadProgressHub>("/api/yzh-upload");

// 临时测试端点：验证 routing pipeline 是否工作
app.MapGet("/api/test/ping", () => "pong");

app.Run();
