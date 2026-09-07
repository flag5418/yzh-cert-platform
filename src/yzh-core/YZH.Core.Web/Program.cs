using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using YZH.Core.EFDbContext;
using YZH.Core.Stand.Helpers;
using YZH.Core.Web.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// ===== 数据库连接（Vol VOLContext） =====
var connectionString = builder.Configuration.GetConnectionString("CertPlatform")
    ?? "Data Source=127.0.0.1;Database=yzh_cert_platform;User ID=root;Password=Yzh123456.;pooling=true;CharSet=utf8;port=3307;";
builder.Services.AddDbContext<VOLContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// ===== YZH Stand Helper =====
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = new JwtOptions
{
    Issuer = jwtSection["Issuer"] ?? "cert-platform",
    Audience = jwtSection["Audience"] ?? "cert-app",
    SecretKey = jwtSection["SecretKey"] ?? "AA3627441FFA4B5DB4E64A29B53CE525",
    ExpirationMinutes = 43200
};
builder.Services.AddScoped(_ => new JwtHelper(jwtOptions));
builder.Services.AddScoped(_ => new PasswordHelper(
    builder.Configuration["PasswordSecret"] ?? "C5ABA9E202D94C43A3CA66002BF77FAF"));

// ===== JWT 认证 =====
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// ===== 控制器 + CORS =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "认证管理平台 API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { {
        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
        Array.Empty<string>()
    }});
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

var app = builder.Build();

// ===== 中间件管道 =====
app.UseMiddleware<GlobalExceptionMiddleware>();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => new { status = "OK", time = DateTime.Now });

app.Logger.LogInformation("YZH Core Web 启动 - 端口 9992");
app.Run();
