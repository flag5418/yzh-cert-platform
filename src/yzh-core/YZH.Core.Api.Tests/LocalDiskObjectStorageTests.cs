using System.Text;
using Microsoft.Extensions.Configuration;
using Xunit;
using YZH.Core.DataBase.Infra;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Api.Tests;

/// <summary>
/// 本地磁盘对象存储契约测试。
/// <para>目的：在没有 MinIO / OSS 凭证的机器与 CI 上，也能验证 <see cref="IObjectStorage"/> 的核心语义，
/// 避免「换 Provider 才发现契约不一致」。</para>
/// </summary>
public sealed class LocalDiskObjectStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalDiskObjectStorage _storage;

    public LocalDiskObjectStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "yzh-storage-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "local",
                ["Storage:Local:RootPath"] = _root
            })
            .Build();

        _storage = new LocalDiskObjectStorage(configuration);
    }

    [Fact]
    public async Task Upload_Then_OpenRead_ReturnsSameBytesAndLength()
    {
        var payload = Encoding.UTF8.GetBytes("制度文件-三阶");
        await _storage.UploadAsync("/standard-directory/org/std/受控文件清单.xls", new MemoryStream(payload), payload.Length);

        var content = await _storage.OpenReadAsync("/standard-directory/org/std/受控文件清单.xls");

        Assert.Equal(payload.Length, content.Length);
        using var buffer = new MemoryStream();
        await content.Stream.CopyToAsync(buffer);
        Assert.Equal(payload, buffer.ToArray());
    }

    [Fact]
    public async Task Exists_IsFalseForMissing_AndTrueAfterUpload()
    {
        Assert.False(await _storage.ExistsAsync("/enterprise-original-source/missing.pdf"));

        var payload = Encoding.UTF8.GetBytes("x");
        await _storage.UploadAsync("/enterprise-original-source/a.pdf", new MemoryStream(payload), payload.Length);

        Assert.True(await _storage.ExistsAsync("/enterprise-original-source/a.pdf"));
    }

    [Fact]
    public async Task OpenRead_MissingObject_ThrowsObjectNotFound()
    {
        await Assert.ThrowsAsync<ObjectNotFoundException>(() =>
            _storage.OpenReadAsync("/not-there/really.pdf"));
    }

    [Fact]
    public async Task Delete_RemovesObject_And_IsIdempotent()
    {
        var payload = Encoding.UTF8.GetBytes("x");
        await _storage.UploadAsync("/tmp/delete-me.txt", new MemoryStream(payload), payload.Length);

        await _storage.DeleteAsync("/tmp/delete-me.txt");
        await _storage.DeleteAsync("/tmp/delete-me.txt"); // 二次删除不抛

        Assert.False(await _storage.ExistsAsync("/tmp/delete-me.txt"));
    }

    [Fact]
    public async Task Copy_KeepsSource_AndRename_RemovesSource()
    {
        var payload = Encoding.UTF8.GetBytes("copy");
        await _storage.UploadAsync("/tmp/source.txt", new MemoryStream(payload), payload.Length);

        await _storage.CopyAsync("/tmp/source.txt", "/tmp/copied.txt");
        Assert.True(await _storage.ExistsAsync("/tmp/source.txt"));
        Assert.True(await _storage.ExistsAsync("/tmp/copied.txt"));

        await _storage.RenameAsync("/tmp/copied.txt", "/tmp/renamed.txt");
        Assert.False(await _storage.ExistsAsync("/tmp/copied.txt"));
        Assert.True(await _storage.ExistsAsync("/tmp/renamed.txt"));
    }

    [Fact]
    public async Task TraversalKey_IsRejected()
    {
        // 对象键来自请求参数 ⇒ 必须阻断越权读写
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.ExistsAsync("../../etc/passwd"));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.OpenReadAsync("..\\..\\windows\\win.ini"));
    }

    [Fact]
    public async Task ListObjects_ReturnsKeysWithLeadingSlash()
    {
        var payload = Encoding.UTF8.GetBytes("x");
        await _storage.UploadAsync("/list/a.txt", new MemoryStream(payload), payload.Length);
        await _storage.UploadAsync("/list/sub/b.txt", new MemoryStream(payload), payload.Length);

        var keys = await _storage.ListObjectsAsync("/list");

        Assert.Contains("/list/a.txt", keys);
        Assert.Contains("/list/sub/b.txt", keys);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // 测试清理失败不影响结论
        }
    }
}
