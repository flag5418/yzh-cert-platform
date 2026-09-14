using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.DataBase.Infra;

/// <summary>
/// MinIO 对象存储实现
/// <para>配置来源：appsettings.json → MinIO:Endpoint / AccessKey / SecretKey / BucketName</para>
/// </summary>
public class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;

    public MinioObjectStorage(IMinioClient client, IConfiguration configuration)
    {
        _client = client;
        _bucketName = configuration["MinIO:BucketName"] ?? "cert-platform";
    }

    /// <summary>上传文件到 MinIO</summary>
    public async Task UploadAsync(string objectName, Stream stream, long size, string contentType = "application/octet-stream", CancellationToken ct = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName.TrimStart('/'))
            .WithStreamData(stream)
            .WithObjectSize(size)
            .WithContentType(contentType);
        await _client.PutObjectAsync(args, ct);
    }

    /// <summary>从 MinIO 下载文件</summary>
    public async Task<(Stream Stream, string ContentType)> DownloadAsync(string objectName, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        var objectNameTrimmed = objectName.TrimStart('/');

        var statArgs = new StatObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectNameTrimmed);
        var stat = await _client.StatObjectAsync(statArgs, ct);

        var getArgs = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectNameTrimmed)
            .WithCallbackStream(async (stream, token) =>
            {
                await stream.CopyToAsync(ms, token);
                ms.Position = 0;
            });
        await _client.GetObjectAsync(getArgs, ct);
        ms.Position = 0;

        return (ms, stat.ContentType ?? "application/octet-stream");
    }

    /// <summary>删除 MinIO 对象</summary>
    public async Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName.TrimStart('/'));
        await _client.RemoveObjectAsync(args, ct);
    }

    /// <summary>重命名（Copy 新 + Delete 旧）</summary>
    public async Task RenameAsync(string oldObjectName, string newObjectName, CancellationToken ct = default)
    {
        var oldPath = oldObjectName.TrimStart('/');
        var newPath = newObjectName.TrimStart('/');

        // 下载源文件
        var ms = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(oldPath)
            .WithCallbackStream(async (stream, token) =>
            {
                await stream.CopyToAsync(ms, token);
                ms.Position = 0;
            });
        await _client.GetObjectAsync(getArgs, ct);

        // 获取内容类型
        var statArgs = new StatObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(oldPath);
        var stat = await _client.StatObjectAsync(statArgs, ct);

        // 上传到新路径
        var putArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(newPath)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(stat.ContentType ?? "application/octet-stream");
        await _client.PutObjectAsync(putArgs, ct);
        ms.Close();

        // 删除旧路径
        await DeleteAsync(oldPath, ct);
    }

    /// <summary>列出前缀下所有对象键</summary>
    public async Task<List<string>> ListObjectsAsync(string prefix, CancellationToken ct = default)
    {
        var cleanPrefix = (prefix ?? "").TrimStart('/');
        var listArgs = new ListObjectsArgs()
            .WithBucket(_bucketName)
            .WithPrefix(cleanPrefix)
            .WithRecursive(true);

        var keys = new List<string>();
        await foreach (var item in _client.ListObjectsEnumAsync(listArgs, ct))
        {
            if (!string.IsNullOrEmpty(item?.Key))
                keys.Add(item.Key);
        }
        return keys;
    }

    /// <summary>递归删除前缀下所有对象（批量 1000 分批）</summary>
    public async Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        var keys = await ListObjectsAsync(prefix, ct);
        if (keys.Count == 0) return;

        const int batchSize = 1000;
        for (int i = 0; i < keys.Count; i += batchSize)
        {
            var batch = keys.Skip(i).Take(batchSize).ToList();
            var removeArgs = new RemoveObjectsArgs()
                .WithBucket(_bucketName)
                .WithObjects(batch);
            await _client.RemoveObjectsAsync(removeArgs, ct);
        }
    }

    /// <summary>检查对象是否存在</summary>
    public async Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName.TrimStart('/'));
            await _client.StatObjectAsync(args, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
