using Microsoft.Extensions.Configuration;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.DataBase.Infra;

/// <summary>
/// 本地磁盘对象存储实现（开发/单机/离线环境用）
///
/// <para>用途：</para>
/// <para>① 开发机无需启动 MinIO / Docker 即可跑通全部文件链路（上传/下载/预览/版本）；</para>
/// <para>② 与 MinIO/OSS 共用同一套 <see cref="IObjectStorage"/> 契约，切换只改 <c>Storage:Provider</c>；</para>
/// <para>③ 作为对象存储的契约测试基座（CI 不依赖任何外部服务）。</para>
///
/// <para>配置：<c>Storage:Provider=local</c> + <c>Storage:Local:RootPath</c>（相对路径按进程当前目录解析）。</para>
/// <para>★ 安全：所有对象键都经过 <see cref="ResolvePath"/> 归一化并校验落在根目录内，
/// 阻断 <c>../</c> 越权读写（对象键来自请求参数，属于不可信输入）。</para>
/// </summary>
public class LocalDiskObjectStorage : IObjectStorage
{
    private readonly string _root;

    public LocalDiskObjectStorage(IConfiguration configuration)
    {
        var configured = configuration["Storage:Local:RootPath"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = "Storage";

        _root = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(Directory.GetCurrentDirectory(), configured));

        Directory.CreateDirectory(_root);
    }

    /// <summary>存储根目录（绝对路径），供启动日志/排障展示</summary>
    public string RootPath => _root;

    public async Task UploadAsync(string objectName, Stream stream, long size, string contentType = "application/octet-stream", CancellationToken ct = default)
    {
        var path = ResolvePath(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // FileMode.Create 覆盖同名；异步写入避免大文件阻塞请求线程
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await stream.CopyToAsync(target, ct);
    }

    public Task<(Stream Stream, string ContentType)> DownloadAsync(string objectName, CancellationToken ct = default)
    {
        var path = ResolvePath(objectName);
        if (!File.Exists(path))
            throw new ObjectNotFoundException(objectName);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult((stream, GetContentType(path)));
    }

    public Task<ObjectContent> OpenReadAsync(string objectName, CancellationToken ct = default)
    {
        var path = ResolvePath(objectName);
        if (!File.Exists(path))
            throw new ObjectNotFoundException(objectName);

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(new ObjectContent(stream, stream.Length, GetContentType(path)));
    }

    public Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        var path = ResolvePath(objectName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string oldObjectName, string newObjectName, CancellationToken ct = default)
    {
        var source = ResolvePath(oldObjectName);
        var target = ResolvePath(newObjectName);
        if (!File.Exists(source))
            throw new ObjectNotFoundException(oldObjectName);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(source, target, overwrite: true);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string sourceObjectName, string targetObjectName, CancellationToken ct = default)
    {
        var source = ResolvePath(sourceObjectName);
        var target = ResolvePath(targetObjectName);
        if (!File.Exists(source))
            throw new ObjectNotFoundException(sourceObjectName);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListObjectsAsync(string prefix, CancellationToken ct = default)
    {
        var normalizedPrefix = NormalizeKey(prefix ?? string.Empty);
        var keys = new List<string>();

        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (normalizedPrefix.Length == 0 || relative.StartsWith(normalizedPrefix, StringComparison.Ordinal))
                keys.Add("/" + relative);
        }

        return Task.FromResult(keys);
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in await ListObjectsAsync(prefix, ct))
        {
            var path = ResolvePath(key);
            if (File.Exists(path)) File.Delete(path);
        }

        // 顺带清理空目录（对象存储没有目录概念，本地实现会留下空壳）
        var normalizedPrefix = NormalizeKey(prefix ?? string.Empty);
        if (normalizedPrefix.Length > 0)
        {
            var dir = ResolvePath(normalizedPrefix);
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
    }

    public Task<bool> ExistsAsync(string objectName, CancellationToken ct = default)
    {
        var path = ResolvePath(objectName);
        return Task.FromResult(File.Exists(path));
    }

    // ==================== 私有方法 ====================

    /// <summary>对象键归一化：去掉前导 '/'、统一分隔符</summary>
    private static string NormalizeKey(string objectName) =>
        (objectName ?? string.Empty).TrimStart('/').Replace('\\', '/');

    /// <summary>
    /// 把对象键解析为根目录内的绝对路径。
    /// <para>⛔ 必须做越界校验：对象键来自请求（用户可传 <c>../../etc/passwd</c>）。</para>
    /// </summary>
    private string ResolvePath(string objectName)
    {
        var key = NormalizeKey(objectName);
        if (key.Length == 0)
            throw new ArgumentException("对象键不能为空", nameof(objectName));

        var full = Path.GetFullPath(Path.Combine(_root, key));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new ArgumentException($"对象键越界：{objectName}", nameof(objectName));

        return full;
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".txt" => "text/plain; charset=utf-8",
        ".md" => "text/markdown; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".csv" => "text/csv; charset=utf-8",
        ".zip" => "application/zip",
        ".rar" => "application/vnd.rar",
        ".7z" => "application/x-7z-compressed",
        _ => "application/octet-stream"
    };
}
