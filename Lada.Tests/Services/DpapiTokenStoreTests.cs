using System;
using System.IO;
using System.Threading.Tasks;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class DpapiTokenStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "LadaDpapiTokenStoreTests_" + Guid.NewGuid());

    [Fact]
    public async Task StoreThenGet_ReturnsOriginalValue()
    {
        var store = new DpapiTokenStore(_tempDir);

        await store.StoreAsync("key1", "hello world");
        var result = await store.GetAsync<string>("key1");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task Get_UnknownKey_ReturnsNull()
    {
        var store = new DpapiTokenStore(_tempDir);

        var result = await store.GetAsync<string>("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesStoredValue()
    {
        var store = new DpapiTokenStore(_tempDir);
        await store.StoreAsync("key1", "hello world");

        await store.DeleteAsync<string>("key1");
        var result = await store.GetAsync<string>("key1");

        Assert.Null(result);
    }

    [Fact]
    public async Task StoredFileOnDisk_IsNotPlaintext()
    {
        var store = new DpapiTokenStore(_tempDir);
        await store.StoreAsync("key1", "a very secret refresh token");

        var files = Directory.GetFiles(_tempDir);
        Assert.Single(files);
        var rawBytes = await File.ReadAllBytesAsync(files[0]);
        var rawText = System.Text.Encoding.UTF8.GetString(rawBytes);

        Assert.DoesNotContain("a very secret refresh token", rawText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
