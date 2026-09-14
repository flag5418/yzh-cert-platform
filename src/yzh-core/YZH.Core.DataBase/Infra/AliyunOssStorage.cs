using Microsoft.Extensions.Configuration;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.DataBase.Infra;

/// <summary>
/// 阿里云 OSS 对象存储实现（预留骨架）
/// <para>配置来源：appsettings.json → AliyunOss:Endpoint / AccessKeyId / AccessKeySecret / BucketName</para>
/// <para>按需引入 Aliyun.OSS.SDK.NetCore 包后填充实现</para>
/// </summary>
public class AliyunOssStorage : IObjectStorage
{
    // private readonly OssClient _client;
    // private readonly string _bucketName;

    public AliyunOssStorage(IConfiguration configuration)
    {
        // TODO: 按需引入 Aliyun.OSS.SDK.NetCore 后实现
        // var endpoint = configuration["AliyunOss:Endpoint"];
        // var accessKeyId = configuration["AliyunOss:AccessKeyId"];
        // var accessKeySecret = configuration["AliyunOss:AccessKeySecret"];
        // _bucketName = configuration["AliyunOss:BucketName"];
        // _client = new OssClient(endpoint, accessKeyId, accessKeySecret);
        throw new NotImplementedException("阿里云 OSS 实现暂未启用，请在 appsettings.json 中配置 Storage:Provider 为 minio");
    }

    public Task UploadAsync(string objectName, Stream stream, long size, string contentType = "application/octet-stream", CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<(Stream Stream, string ContentType)> DownloadAsync(string objectName, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(string objectName, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task RenameAsync(string oldObjectName, string newObjectName, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<List<string>> ListObjectsAsync(string prefix, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
        => throw new NotImplementedException();
}
