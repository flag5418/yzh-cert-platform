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
                    var secretKey = config["MinIO:SecretKey"] ?? "Yzh123456.";

                    return new MinioClient()
                        .WithEndpoint(endpoint)
                        .WithCredentials(accessKey, secretKey)
                        .WithSSL(false)
                        .Build();
                });
                services.AddScoped<IObjectStorage, MinioObjectStorage>();
                break;
        }

        return services;
    }
}
