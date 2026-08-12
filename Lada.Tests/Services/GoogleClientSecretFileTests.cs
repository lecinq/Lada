using System;
using System.IO;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class GoogleClientSecretFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "LadaGoogleClientSecretFileTests_" + Guid.NewGuid() + ".json");

    [Fact]
    public void TryLoad_SnakeCaseJson_PopulatesClientIdAndSecret()
    {
        File.WriteAllText(_path, """
            {
              "client_id": "abc123.apps.googleusercontent.com",
              "client_secret": "GOCSPX-xyz"
            }
            """);

        var loaded = GoogleClientSecretFile.TryLoad(_path, out var secrets);

        Assert.True(loaded);
        Assert.Equal("abc123.apps.googleusercontent.com", secrets!.ClientId);
        Assert.Equal("GOCSPX-xyz", secrets.ClientSecret);
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        var loaded = GoogleClientSecretFile.TryLoad(Path.Combine(Path.GetTempPath(), "does-not-exist.json"), out var secrets);

        Assert.False(loaded);
        Assert.Null(secrets);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
