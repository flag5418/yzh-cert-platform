using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz.Impl;
using Quartz;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using YZH.Core.CacheManager;
using YZH.Core.Configuration;
using YZH.Core.Controllers.Basic;
using YZH.Core.Dapper;
using YZH.Core.Extensions;
using YZH.Core.Filters;
using YZH.Core.Middleware;
using YZH.Core.ObjectActionValidator;
using YZH.Core.Quartz;
using YZH.WebApi.Controllers.Hubs;
using YZH.WebApi;
using Minio;
using Microsoft.Extensions.Configuration;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddModule(builder.Configuration);

// ====== YZH 全新系统模块（已禁用）======
// YZH.System 项目引用因命名空间冲突暂时移除，AddYzhSystem 调用已注释
// builder.Services.AddYzhSystem(builder.Configuration);

// ====== YZH Framework 服务注册 ======
// YZH V3.0 配置驱动 UI 服务（业务逻辑在 YZH.Core，Controller 只做 HTTP 适配）
builder.Services.AddScoped<YZH.Core.Services.IYzhPageConfigService, YZH.Core.Services.PageConfigService>();


builder.Services
    .AddControllers()
        .AddNewtonsoftJson(op =>
        {
            op.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
            op.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
            op.SerializerSettings.Converters.Add(new LongCovert());
            //op.SerializerSettings.Converters.Add(new StringCovert());
        });
DapperParseGuidTypeHandler.InitParseGuid();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
          .AddJwtBearer(options =>
          {
              options.TokenValidationParameters = new TokenValidationParameters
              {
                  SaveSigninToken = true,//����token,��̨��֤token�Ƿ���Ч(��Ҫ)
                  ValidateIssuer = true,//�Ƿ���֤Issuer
                  ValidateAudience = true,//�Ƿ���֤Audience
                  ValidateLifetime = true,//�Ƿ���֤ʧЧʱ��
                  ValidateIssuerSigningKey = true,//�Ƿ���֤SecurityKey
                  ValidAudience = AppSetting.Secret.Audience,//Audience
                  ValidIssuer = AppSetting.Secret.Issuer,//Issuer���������ǰ��ǩ��jwt������һ��
                  IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AppSetting.Secret.JWT))
              };
              options.Events = new JwtBearerEvents()
              {
                  OnChallenge = context =>
                  {
                      context.HandleResponse();
                      context.Response.Clear();
                      context.Response.ContentType = "application/json";
                      context.Response.StatusCode = 401;
                      context.Response.WriteAsync(new { message = "��Ȩδͨ��", status = false, code = 401 }.Serialize());
                      return Task.CompletedTask;
                  }
              };
          });
builder.Services.AddCors(options =>
{
    options.AddPolicy("cors", builder =>
    {
        builder.SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "vol.core.api", Version = "v1" });
    var security = new Dictionary<string, IEnumerable<string>> { { AppSetting.Secret.Issuer, new string[] { } } };
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Description = "JWT��Ȩtokenǰ����Ҫ�����ֶ�Bearer��һ���ո�,��Bearer token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    { { new OpenApiSecurityScheme{  Reference = new OpenApiReference {  Type = ReferenceType.SecurityScheme,  Id = "Bearer" }}, new string[] { }  } });
})
 .AddControllers()
.ConfigureApiBehaviorOptions(options =>
{
    options.SuppressConsumesConstraintForFormFileParameters = true;
    options.SuppressInferBindingSourcesForParameters = true;
    options.SuppressModelStateInvalidFilter = true;
    options.SuppressMapClientErrors = true;
    options.ClientErrorMapping[404].Link =
        "https://*/404";
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<YZH.Core.SignalR.UploadProgressHub>();
builder.Services.AddHttpClient()
.AddHttpContextAccessor()
.AddMemoryCache()
.AddTransient<HttpResultfulJob>()
.AddSingleton<ISchedulerFactory, StdSchedulerFactory>()
.AddSingleton<Quartz.Spi.IJobFactory, IOCJobFactory>()
.AddSingleton<RedisCacheService>();

builder.Services.AddMvc(options =>
{
    options.Filters.Add(typeof(ApiAuthorizeFilter));
    options.Filters.Add(typeof(ActionExecuteFilter));
    // YZH 全局异常过滤器：Controller 层最终兜底，脱敏后返回友好提示
    options.Filters.Add(typeof(YZH.Core.Exceptions.AppExceptionFilter));
});

// ====== Office 文档转换后台服务 ======
builder.Services.AddScoped<Cert.Platform.Services.Admin.Platform.OfficeConvertService>();

// ====== yzh 队列中心（YZH.Core.Queue 框架核心） ======
builder.Services.Configure<YZH.Core.Queue.QueueOptions>(builder.Configuration.GetSection(YZH.Core.Queue.QueueOptions.SectionName));
builder.Services.AddSingleton<YZH.Core.Queue.QueueManager>();
builder.Services.AddSingleton<YZH.Core.Queue.IYzhTaskExecutor, Cert.Platform.Services.Admin.Platform.OfficeConvertTaskExecutor>();
// 队列取消后的业务清理钩子：上传任务取消时彻底清理数据库记录 + MinIO 对象
builder.Services.AddScoped<YZH.Core.Queue.IYzhQueueCancelHandler, Cert.Platform.Services.Admin.Platform.UploadQueueCancelHandler>();
builder.Services.AddHostedService<YZH.Core.Queue.QueueHostedService>();

// ====== SignalR 转换进度通知（桥接队列引擎与 Hub） ======
builder.Services.AddScoped<Cert.Platform.IServices.Admin.Platform.IConvertNotifier, YZH.WebApi.Hubs.ConvertNotifier>();
builder.Services.AddSingleton<YZH.Core.Queue.IYzhQueueNotifier, Cert.Platform.Services.Admin.Platform.CertQueueNotifier>();

// ====== YZH Framework 核心服务注册（替代 FrameworkModule Autofac 注册）======
// 文件提取服务：仅注册 IFileExtractor。
// 注意：不要在这里注册未键控的 ITextExtractor —— FileExtractorService 同时有无参构造与
// (ITextExtractor×4) 构造，MS DI 会优先选择参数最多的构造，未键控注册会导致四个参数
// 全部解析为最后注册的 PlainTextExtractor，所有文档都被当成纯文本（详见 FileExtractorService）。
// 具体提取器由 FileExtractorService 无参构造内部实例化（Npoi/PlainText 等）。
builder.Services.AddScoped<YZH.Core.Extractor.IFileExtractor, YZH.Core.Extractor.FileExtractorService>();

// LLM 服务
builder.Services.AddScoped<YZH.Core.AI.Clients.ILlmProvider, YZH.Core.AI.Clients.QwenApiProvider>();
builder.Services.AddScoped<YZH.Core.AI.Clients.ILlmProvider, YZH.Core.AI.Clients.OllamaProvider>();
builder.Services.AddScoped<YZH.Core.AI.Clients.ILlmProvider, YZH.Core.AI.Clients.MockProvider>();
builder.Services.AddScoped<YZH.Core.AI.Clients.ILlmClient, YZH.Core.AI.Clients.LlmClient>();
builder.Services.AddScoped<YZH.Core.AI.Prompt.IPromptInterpreter, YZH.Core.AI.Prompt.PromptInterpreter>();

// 工作流服务（V2 静态方法版）
builder.Services.AddScoped<YZH.Core.Workflow.SkillExecutor>();
// CertSkillRegistry 替代 SkillRegistry（项目侧实现，依赖 WfSkillReflection）
builder.Services.AddScoped<YZH.Core.Workflow.ISkillRegistry, Cert.Platform.Services.Admin.Platform.WorkflowEngine.CertSkillRegistry>();
builder.Services.AddScoped<YZH.Core.Workflow.IWorkflowEngine, YZH.Core.Workflow.WorkflowEngine>();

// ISkillNode 实例注册（供 SkillRegistry DI 回退，非工作流静态 Skill）
builder.Services.AddScoped<YZH.Core.Workflow.ISkillNode, YZH.Core.Skills.LlmExtractSkill>();

// 工作流执行引擎（V3 模块注册）
builder.Services.AddScoped<Cert.Platform.Services.Admin.Platform.WorkflowEngine.WorkflowConfigParser>();
builder.Services.AddScoped<Cert.Platform.Services.Admin.Platform.WorkflowEngine.NodeExecutor>();
builder.Services.AddScoped<Cert.Platform.Services.Admin.Platform.WorkflowEngine.WorkflowInterpreter>();
builder.Services.AddScoped<Cert.Platform.Services.Admin.Platform.WorkflowEngine.TaskCacheService>();
builder.Services.AddSingleton<Cert.Platform.Services.Admin.Platform.WorkflowEngine.WorkflowLogger>();
builder.Services.AddScoped<Cert.Platform.Services.Admin.Platform.WorkflowEngine.WfExecutionTaskService>();

// 新增Helper服务
            // 注册MinIO客户端
            builder.Services.AddSingleton<IMinioClient>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return new MinioClient()
                    .WithEndpoint(config["MinIO:Endpoint"] ?? "127.0.0.1:9000")
                    .WithCredentials(
                        config["MinIO:AccessKey"] ?? "admin",
                        config["MinIO:SecretKey"] ?? "Yzh123456.")
                    .WithSSL(false)
                    .Build();
            });
            builder.Services.AddScoped<Cert.Platform.IServices.Admin.Platform.IMinIOHelper, Cert.Platform.Services.Admin.Platform.MinIOHelper>();
builder.Services.AddScoped<Cert.Platform.IServices.Admin.Platform.IFolderFileManager, Cert.Platform.Services.Admin.Platform.FolderFileManager>();
builder.Services.AddScoped<Cert.Platform.IServices.Admin.Platform.IFileStorageService, Cert.Platform.Services.Admin.Platform.FileStorageService>();
            // 注册文件提取器（YZH.Core），供文档提取规则 analyze/content 链路使用
            builder.Services.AddScoped<YZH.Core.Extractor.IFileExtractor, YZH.Core.Extractor.FileExtractorService>();

var startup = new Startup(builder.Configuration);


builder.Services.UseMethodsModelParameters().UseMethodsGeneralParameters();
builder.Services.AddSingleton<IObjectModelValidator>(new NullObjectModelValidator());
//Swagger
builder.Services.AddEndpointsApiExplorer();
//��̨Ĭ�������˿�
builder.WebHost.UseUrls("http://*:9992");
builder.Services.Configure<FormOptions>(x =>
{
    x.MultipartBodyLengthLimit = 1024 * 1024 * 100;
}).Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 100;
}).Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 1024 * 1024 * 100;
});

var app = builder.Build();

//��ʽ�������Ҫ�ر�swgger,��ע���������д���
//app.UseDeveloperExceptionPage();
//app.UseSwagger();
//app.UseSwaggerUI();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    //��ʱ���������Ҫ����ִ�ж�ʱ�����뽫�˴������else����
    app.UseQuartz(app.Environment);
}
app.UseMiddleware<ExceptionHandlerMiddleWare>();
app.UseDefaultFiles();
app.UseStaticFiles().UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true
});
app.Use(HttpRequestMiddleware.Context);

string _uploadPath = (app.Environment.ContentRootPath + "/Upload").ReplacePath();

if (!Directory.Exists(_uploadPath))
{
    Directory.CreateDirectory(_uploadPath);
}

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(
    Path.Combine(Directory.GetCurrentDirectory(), @"Upload")),
    RequestPath = "/Upload",
    OnPrepareResponse = (Microsoft.AspNetCore.StaticFiles.StaticFileResponseContext staticFile) => { }
});
//����HttpContext
app.UseStaticHttpContext();
// Configure the HTTP request pipeline.

//��ʽ��������swagger��ȡ������ifע��
//if (app.Environment.IsDevelopment())
//{
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});
//}

app.UseCors("cors");
app.UseCors();
// ʹ��·��
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<HomePageMessageHub>("/message");
app.MapHub<YZH.Core.SignalR.UploadProgressHub>("/uploadHub");
app.Run();