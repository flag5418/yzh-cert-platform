using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using YZH.Core.DataBase.Infra;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Extensions;

/// <summary>
/// 对象存储 DI 注册扩展
/// <para>配置驱动：appsettings.json → Storage:Provider（"minio" / "local" / "aliyun-oss"）</para>
/// <para>★ 配置校验在**注册期**完成（= 启动期）：缺密钥/未知 provider 直接抛异常，
/// 不再等到第一个请求才 500（旧实现里 MinIO 密钥缺失是首次解析时才抛）。</para>
/// </summary>
public static class StorageServiceExtensions
{
    public static IServiceCollection AddYzhStorage(this IServiceCollection services, IConfiguration config)
    {
        var provider = (config["Storage:Provider"] ?? "minio").Trim().ToLowerInvariant();
        Console.WriteLine($"[YZH] 对象存储 Provider = {provider}");

        switch (provider)
        {
            case "local":
            {
                var root = config["Storage:Local:RootPath"];
                if (string.IsNullOrWhiteSpace(root))
                    root = Path.Combine("Storage");
                Console.WriteLine($"[YZH] 本地对象存储根目录 = {Path.GetFullPath(root)}");
                services.AddSingleton<IObjectStorage, LocalDiskObjectStorage>();
                break;
            }

            case "aliyun-oss":
            {
                RequireConfig(config, "AliyunOss:Endpoint");
                RequireConfig(config, "AliyunOss:AccessKeyId");
                RequireConfig(config, "AliyunOss:AccessKeySecret");
                RequireConfig(config, "AliyunOss:BucketName");

                // ★ 骨架实现仍在（AliyunOssStorage 全方法 NotImplemented）。
                //   在此**启动期**明确失败，避免「启动正常、第一次上传 500」这种最难查的形态。
                //   接入真实 OSS 时：实现 AliyunOssStorage 后删除本 throw 即可。
                throw new NotSupportedException(
                    "Storage:Provider=aliyun-oss 尚未实现（AliyunOssStorage 为空壳）。" +
                    "请暂用 Storage:Provider=minio（开发）或 local（离线/测试）。" +
                    "实现步骤见 docs/40-实施/开发计划/日志与对象存储治理计划-V1.md 阶段 B。");
            }

            default: // "minio"
            {
                var endpoint = config["MinIO:Endpoint"];
                var accessKey = config["MinIO:AccessKey"];
                // ⛔ 密钥只允许来自 appsettings.json（已 gitignore）；缺失即启动失败
                var secretKey = config["MinIO:SecretKey"];

                RequireConfig(config, "MinIO:Endpoint");
                RequireConfig(config, "MinIO:AccessKey");
                if (string.IsNullOrWhiteSpace(secretKey))
                    throw new InvalidOperationException(
                        "MinIO:SecretKey 未配置。请在 appsettings.json 或环境变量 MinIO__SecretKey 中提供" +
                        "（⛔ 不要把密钥写进代码）。");

                services.AddSingleton<IMinioClient>(sp =>
                {
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
        }

        return services;
    }

    /// <summary>
    /// 必需配置项校验（缺失即启动失败，报错含完整配置键名）。
    /// <para>⚠️ 只校验**存在性**，不校验正确性 —— 连通性由调用方在首次使用时暴露。</para>
    /// </summary>
    private static void RequireConfig(IConfiguration config, string key)
    {
        if (string.IsNullOrWhiteSpace(config[key]))
            throw new InvalidOperationException(
                $"对象存储配置缺少 {key}。请在 appsettings.json 或环境变量（把 ':' 换成 '__'）中提供。");
    }
}
