namespace YZH.Core.Stand.Interfaces;

/// <summary>
/// 通用对象存储抽象（YZH 核心能力）
/// <para>实现可插拔：MinIO / 阿里 OSS / 本地文件系统</para>
/// <para>配置驱动：appsettings.json → Storage:Provider</para>
/// </summary>
public interface IObjectStorage
{
    /// <summary>上传文件</summary>
    Task UploadAsync(string objectName, Stream stream, long size, string contentType = "application/octet-stream", CancellationToken ct = default);

    /// <summary>下载文件，返回流和内容类型</summary>
    Task<(Stream Stream, string ContentType)> DownloadAsync(string objectName, CancellationToken ct = default);

    /// <summary>删除单个对象</summary>
    Task DeleteAsync(string objectName, CancellationToken ct = default);

    /// <summary>重命名（复制新 + 删除旧）</summary>
    Task RenameAsync(string oldObjectName, string newObjectName, CancellationToken ct = default);

    /// <summary>列出前缀下所有对象键</summary>
    Task<List<string>> ListObjectsAsync(string prefix, CancellationToken ct = default);

    /// <summary>递归删除前缀下所有对象</summary>
    Task DeletePrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>检查对象是否存在</summary>
    Task<bool> ExistsAsync(string objectName, CancellationToken ct = default);
}
