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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz.Impl;
using Quartz;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using VOL.Core.CacheManager;
using VOL.Core.Configuration;
using VOL.Core.Controllers.Basic;
using VOL.Core.Dapper;
using VOL.Core.Extensions;
using VOL.Core.Filters;
using VOL.Core.Middleware;
using VOL.Core.ObjectActionValidator;
using VOL.Core.Quartz;
using VOL.WebApi.Controllers.Hubs;
using VOL.WebApi;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddModule(builder.Configuration);

// ====== YZH Framework 服务注册 ======
// YZH V3.0 配置驱动 UI 服务（业务逻辑在 YZH-Framework，Controller 只做 HTTP 适配）
builder.Services.AddScoped<YZH.CertPlatform.Services.IYzhPageConfigService, YZH.CertPlatform.Services.YzhPageConfigService>();


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
builder.Services.AddSingleton<VOL.Core.SignalR.UploadProgressHub>();
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
});

// ====== Office 文档转换后台服务 ======
builder.Services.AddScoped<VOL.Builder.Services.CertPlatform.OfficeConvertService>();
            
            // 新增Helper服务
            builder.Services.AddScoped<VOL.Builder.IServices.CertPlatform.IMinIOHelper, VOL.Builder.Services.CertPlatform.MinIOHelper>();
            builder.Services.AddScoped<VOL.Builder.IServices.CertPlatform.IFolderFileManager, VOL.Builder.Services.CertPlatform.FolderFileManager>();
            builder.Services.AddScoped<VOL.Builder.IServices.CertPlatform.IFileStorageService, VOL.Builder.Services.CertPlatform.FileStorageService>();
builder.Services.AddSingleton<VOL.Builder.Services.CertPlatform.ConvertQueueManager>();
builder.Services.AddHostedService<VOL.Builder.Services.CertPlatform.ConvertHostedService>();

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
app.MapHub<VOL.Core.SignalR.UploadProgressHub>("/uploadHub");
app.Run();