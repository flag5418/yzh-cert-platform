using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using YZH.Core.DataBase.Infra;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Extensions;

/// <summary>
/// 对象存储 DI 注册扩展
/// <para>配置驱动：appsettings.json → Storage:Provider（"minio" / "aliyun-oss"）</para>
/// </summary>
public static class StorageServiceExtensions
{
    public static IServiceCollection AddYzhStorage(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Storage:Provider"] ?? "minio";

        switch (provider.ToLowerInvariant())
        {
            case "aliyun-oss":
                services.AddScoped<IObjectStorage, AliyunOssStorage>();
                break;

            default: // "minio"
                services.AddSingleton<IMinioClient>(sp =>
                {
                    var endpoint = config["MinIO:Endpoint"] ?? "127.0.0.1:9000";
                    var accessKey = config["MinIO:AccessKey"] ?? "admin";
                    // ⛔ 密钥只允许来自 appsettings.json（已 gitignore）；代码内禁止密钥字面量，缺失即启动失败
                    var secretKey = config["MinIO:SecretKey"];
                    if (string.IsNullOrWhiteSpace(secretKey))
                        throw new InvalidOperationException(
                            "MinIO:SecretKey 未配置。请在 appsettings.json 或环境变量 " +
                            "MinIO__SecretKey 中提供（⛔ 不要把密钥写进代码）。");

                    // ─────────────────────────────────────────────────────────────────
                    // ★★★ 显式禁用代理（2026-10-06 实测根治，⛔ 不要删）
                    //
                    // .NET 在 Unix 上 `HttpClient.DefaultProxy` 会读 `HTTP_PROXY`/`HTTPS_PROXY`，
                    // 于是 **本机 9000 的请求被送去代理**；开发机代理端口每会话都变，
                    // 后端继承到旧端口 ⇒ 连接被拒 ⇒ **所有 MinIO 读写失败**，
                    // 而对外报「文件不存在 / 对象不存在或存储不可用」= **看着像数据缺失，实为网络层**。
                    //
                    // MinIO 是**内网对象存储**，走代理没有任何意义 ⇒ 这里直接 UseProxy = false，
                    // 使该修复**不依赖任何环境变量、任何启动路径**（IDE / dotnet run / 服务 都一样）。
                    // （`Program.cs` 顶部另有一份 NO_PROXY 兜底，属双保险。）
                    // ─────────────────────────────────────────────────────────────────
                    var httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false })
                    {
                        // SDK 内部用 CancellationToken 做单请求超时；这里给一个宽松但**有界**的兜底。
                        // ⛔ 不用 InfiniteTimeSpan —— 网络异常时会永久挂住请求。
                        Timeout = TimeSpan.FromMinutes(10),
                    };

                    return new MinioClient()
                        .WithEndpoint(endpoint)
                        .WithCredentials(accessKey, secretKey)
                        .WithSSL(false)
                        .WithHttpClient(httpClient)
                        .Build();
                });
                services.AddScoped<IObjectStorage, MinioObjectStorage>();
                break;
        }

        return services;
    }
}
