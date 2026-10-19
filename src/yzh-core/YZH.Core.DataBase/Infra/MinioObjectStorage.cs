using System.IO.Pipelines;
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

    /// <summary>
    /// 以流的方式打开对象（真流式，不把整份文件读进内存）。
    /// <para>实现方式：Stat 拿长度/类型 → GetObject 回调写入 <see cref="Pipe"/> → 调用方从管道读取。</para>
    /// <para>⚠️ 返回的流不可 Seek（无法处理 Range），由调用方决定是否降级为整文件返回。</para>
    /// </summary>
    public async Task<ObjectContent> OpenReadAsync(string objectName, CancellationToken ct = default)
    {
        var key = objectName.TrimStart('/');
        var statArgs = new StatObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key);

        long size;
        string contentType;
        try
        {
            var stat = await _client.StatObjectAsync(statArgs, ct);
            size = stat.Size;
            contentType = stat.ContentType ?? "application/octet-stream";
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            throw new ObjectNotFoundException(objectName, ex);
        }

        var pipe = new Pipe();
        var getArgs = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithCallbackStream(async (stream, token) =>
            {
                try
                {
                    await stream.CopyToAsync(pipe.Writer.AsStream(), token);
                }
                finally
                {
                    await pipe.Writer.CompleteAsync();
                }
            });

        // 后台拉取；异常通过管道回传给读端（CompleteAsync(exception)），不会静默丢失
        _ = Task.Run(async () =>
        {
            try
            {
                await _client.GetObjectAsync(getArgs, ct);
            }
            catch (Exception ex)
            {
                await pipe.Writer.CompleteAsync(ex);
            }
        }, CancellationToken.None);

        return new ObjectContent(pipe.Reader.AsStream(), size, contentType);
    }

    /// <summary>删除 MinIO 对象</summary>
    public async Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName.TrimStart('/'));
        await _client.RemoveObjectAsync(args, ct);
    }

    /// <summary>复制对象（源对象保留）。MinIO SDK 6.0.4 的服务端 CopyObject 源对象参数类型不稳定，
    /// 沿用与 <see cref="RenameAsync"/> 相同的下载-上传通道，行为等价。</summary>
    public async Task CopyAsync(string sourceObjectName, string targetObjectName, CancellationToken ct = default)
    {
        var sourcePath = sourceObjectName.TrimStart('/');

        var ms = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(sourcePath)
            .WithCallbackStream(async (stream, token) =>
            {
                await stream.CopyToAsync(ms, token);
                ms.Position = 0;
            });
        await _client.GetObjectAsync(getArgs, ct);

        var statArgs = new StatObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(sourcePath);
        var stat = await _client.StatObjectAsync(statArgs, ct);

        ms.Position = 0;
        var putArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(targetObjectName.TrimStart('/'))
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(stat.ContentType ?? "application/octet-stream");
        await _client.PutObjectAsync(putArgs, ct);
        ms.Close();
    }

    /// <summary>重命名（Copy 新 + Delete 旧）</summary>
    public async Task RenameAsync(string oldObjectName, string newObjectName, CancellationToken ct = default)
    {
        await CopyAsync(oldObjectName, newObjectName, ct);
        await DeleteAsync(oldObjectName, ct);
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

    /// <summary>
    /// 检查对象是否存在。
    /// <para>★ 只把「确实不存在」判为 false；网络不通、鉴权失败、桶不存在一律抛出。</para>
    /// <para>旧实现 <c>catch { return false; }</c> 会把 <c>Connection refused</c> 报成「文件不存在」，
    /// 与 2026-10-06 代理事故同型（症状与病因错位），已纠正。</para>
    /// </summary>
    public async Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
    {
        var args = new StatObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName.TrimStart('/'));

        try
        {
            await _client.StatObjectAsync(args, ct);
            return true;
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            return false;
        }
    }

    /// <summary>判定「对象不存在」的真实形态：显式 404、SDK 异常、消息文案。</summary>
    private static bool IsNotFound(Exception ex)
    {
        if (ex is ObjectNotFoundException) return false; // 本类已升级的语义，不再二次包装

        // Minio SDK 7.x 的专用异常（最可靠）；其余靠错误码文案兜底
        if (ex is Minio.Exceptions.ObjectNotFoundException) return true;

        var message = ex.Message ?? string.Empty;
        return message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase)
            || message.Contains("specified key does not exist", StringComparison.OrdinalIgnoreCase);
    }
}
