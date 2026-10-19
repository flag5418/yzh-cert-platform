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

    /// <summary>
    /// 下载文件并返回内存流（兼容旧调用；大文件请改用 <see cref="OpenReadAsync"/>）。
    /// </summary>
    Task<(Stream Stream, string ContentType)> DownloadAsync(string objectName, CancellationToken ct = default);

    /// <summary>
    /// 以流的方式打开对象（**不缓冲整份文件**），供下载/预览/透传使用。
    /// <para>对象不存在时抛 <see cref="ObjectNotFoundException"/>；网络/鉴权失败必须冒泡，⛔ 不得伪装成「不存在」。</para>
    /// <para>默认实现回退到 <see cref="DownloadAsync"/>；支持流式读取的实现应覆写。</para>
    /// </summary>
    async Task<ObjectContent> OpenReadAsync(string objectName, CancellationToken ct = default)
    {
        var (stream, contentType) = await DownloadAsync(objectName, ct);
        var length = stream.CanSeek ? stream.Length : -1;
        return new ObjectContent(stream, length, contentType);
    }

    /// <summary>删除单个对象</summary>
    Task DeleteAsync(string objectName, CancellationToken ct = default);

    /// <summary>重命名（复制新 + 删除旧）</summary>
    Task RenameAsync(string oldObjectName, string newObjectName, CancellationToken ct = default);

    /// <summary>复制对象（保留源对象；06 册 G-3c 恢复归档件回当前路径用）</summary>
    Task CopyAsync(string sourceObjectName, string targetObjectName, CancellationToken ct = default);

    /// <summary>列出前缀下所有对象键</summary>
    Task<List<string>> ListObjectsAsync(string prefix, CancellationToken ct = default);

    /// <summary>递归删除前缀下所有对象</summary>
    Task DeletePrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>检查对象是否存在</summary>
    Task<bool> ExistsAsync(string objectName, CancellationToken ct = default);
}

/// <summary>
/// 流式对象内容。由调用方负责 Dispose（<c>using</c> / <c>await using</c>）。
/// </summary>
public sealed class ObjectContent : IAsyncDisposable, IDisposable
{
    public ObjectContent(Stream stream, long length, string contentType)
    {
        Stream = stream;
        Length = length;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
    }

    /// <summary>对象内容流（可能不可 Seek，⛔ 不要假定支持 Range）</summary>
    public Stream Stream { get; }

    /// <summary>字节长度；未知时为 -1</summary>
    public long Length { get; }

    /// <summary>MIME 类型</summary>
    public string ContentType { get; }

    public void Dispose() => Stream.Dispose();

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}

/// <summary>
/// 对象不存在（明确的 404 语义）。
/// <para>用途：把「文件真的没有」与「网络/鉴权/存储不可用」分开 —— 后者必须冒泡到调用方，
/// 否则会重演「代理故障被报成文件不存在」的误诊（见 2026-10-06 MinIO 事故）。</para>
/// </summary>
public sealed class ObjectNotFoundException : Exception
{
    public ObjectNotFoundException(string objectName, Exception? inner = null)
        : base($"对象不存在：{objectName}", inner) => ObjectName = objectName;

    /// <summary>缺失的对象键</summary>
    public string ObjectName { get; }
}
