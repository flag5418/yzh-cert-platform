using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// MinIO 操作帮助服务实现
    /// 所有MinIO操作集中在一个类中，便于维护和复用
    /// </summary>
    public class MinIOHelper : IMinIOHelper
    {
        private readonly IMinioClient _client;
        private readonly string _bucketName;

        public string BucketName => _bucketName;

        public MinIOHelper(IConfiguration configuration, IMinioClient minioClient)
        {
            _client = minioClient;
            _bucketName = configuration["MinIO:BucketName"] ?? "cert-platform";
        }

        /// <summary>上传文件到MinIO</summary>
        public async Task UploadAsync(string objectName, Stream stream, long size, string contentType = null)
        {
            var args = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName.TrimStart('/'))
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(contentType ?? "application/octet-stream");
            await _client.PutObjectAsync(args);
        }

        /// <summary>从MinIO下载文件</summary>
        public async Task<(Stream stream, string contentType)> DownloadAsync(string objectName)
        {
            var ms = new MemoryStream();
            var objectNameTrimmed = objectName.TrimStart('/');
            
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectNameTrimmed);
            var stat = await _client.StatObjectAsync(statArgs);

            var getArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectNameTrimmed)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await stream.CopyToAsync(ms, ct);
                    ms.Position = 0;
                });
            await _client.GetObjectAsync(getArgs);
            ms.Position = 0;

            return (ms, stat.ContentType ?? "application/octet-stream");
        }

        /// <summary>删除MinIO对象</summary>
        public async Task DeleteAsync(string objectName)
        {
            try
            {
                var args = new RemoveObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName.TrimStart('/'));
                await _client.RemoveObjectAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MinIOHelper.Delete] 删除失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>重命名/移动MinIO对象（Copy新路径 + Delete旧路径）</summary>
        public async Task RenameAsync(string oldObjectName, string newObjectName)
        {
            var oldPath = oldObjectName.TrimStart('/');
            var newPath = newObjectName.TrimStart('/');

            var ms = new MemoryStream();
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(oldPath)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await stream.CopyToAsync(ms, ct);
                    ms.Position = 0;
                });
            await _client.GetObjectAsync(getArgs);

            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(oldPath);
            var stat = await _client.StatObjectAsync(statArgs);

            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(newPath)
                .WithStreamData(ms)
                .WithObjectSize(ms.Length)
                .WithContentType(stat.ContentType ?? "application/octet-stream");
            await _client.PutObjectAsync(putArgs);
            ms.Close();

            await DeleteAsync(oldPath);
            Console.WriteLine($"[MinIOHelper.Rename] {oldPath} → {newPath}");
        }

        /// <summary>列出指定前缀下的所有对象（递归）</summary>
        public async Task<List<string>> ListObjectsAsync(string prefix)
        {
            var cleanPrefix = (prefix ?? "").TrimStart('/');
            var listArgs = new ListObjectsArgs()
                .WithBucket(_bucketName)
                .WithPrefix(cleanPrefix)
                .WithRecursive(true);

            var keys = new List<string>();
            await foreach (var item in _client.ListObjectsEnumAsync(listArgs))
            {
                if (!string.IsNullOrEmpty(item?.Key))
                    keys.Add(item.Key);
            }
            return keys;
        }

        /// <summary>递归删除指定前缀下的所有对象</summary>
        public async Task DeletePrefixAsync(string prefix)
        {
            var keys = await ListObjectsAsync(prefix);
            if (keys.Count == 0)
                return;

            try
            {
                // 批量删除（一次最多 1000 个，分批处理）
                const int batchSize = 1000;
                for (int i = 0; i < keys.Count; i += batchSize)
                {
                    var batch = keys.Skip(i).Take(batchSize).ToList();
                    var removeArgs = new RemoveObjectsArgs()
                        .WithBucket(_bucketName)
                        .WithObjects(batch);
                    var errors = await _client.RemoveObjectsAsync(removeArgs);
                    foreach (var err in errors)
                    {
                        Console.WriteLine($"[MinIOHelper.DeletePrefix] 删除失败: {err.Key} - {err.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MinIOHelper.DeletePrefix] 批量删除异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>检查对象是否存在</summary>
        public async Task<bool> ExistsAsync(string objectName)
        {
            try
            {
                var args = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName.TrimStart('/'));
                await _client.StatObjectAsync(args);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
