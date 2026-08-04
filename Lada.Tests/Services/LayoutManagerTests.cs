using System;
using System.IO;
using System.Threading.Tasks;
using Lada.Models;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class LayoutManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public LayoutManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LadaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "layout.json");
    }

    [Fact]
    public void Load_ReturnsEmptyCollection_WhenFileMissing()
    {
        var manager = new LayoutManager(_filePath);

        var result = manager.Load();

        Assert.Empty(result.Ladas);
    }

    [Fact]
    public void Load_ReturnsEmptyCollection_WhenFileIsCorrupt()
    {
        File.WriteAllText(_filePath, "{ not valid json");
        var manager = new LayoutManager(_filePath);

        var result = manager.Load();

        Assert.Empty(result.Ladas);
    }

    [Fact]
    public void SaveImmediate_ThenLoad_RoundTrips()
    {
        var manager = new LayoutManager(_filePath);
        var layout = new LadaLayoutCollection
        {
            Ladas = { new LadaLayout { Title = "Test Lada", X = 5, Y = 6 } }
        };

        manager.SaveImmediate(layout);
        var loaded = manager.Load();

        var lada = Assert.Single(loaded.Ladas);
        Assert.Equal("Test Lada", lada.Title);
        Assert.Equal(5, lada.X);
    }

    [Fact]
    public void SaveImmediate_OverwritesExistingFile_Atomically()
    {
        var manager = new LayoutManager(_filePath);
        manager.SaveImmediate(new LadaLayoutCollection { Ladas = { new LadaLayout { Title = "First" } } });
        manager.SaveImmediate(new LadaLayoutCollection { Ladas = { new LadaLayout { Title = "Second" } } });

        var loaded = manager.Load();

        Assert.Equal("Second", Assert.Single(loaded.Ladas).Title);
        Assert.False(File.Exists(_filePath + ".tmp"));
    }

    [Fact]
    public async Task RequestSave_WritesFile_AfterDebounceInterval()
    {
        var manager = new LayoutManager(_filePath, TimeSpan.FromMilliseconds(50));

        manager.RequestSave(new LadaLayoutCollection { Ladas = { new LadaLayout { Title = "First" } } });
        manager.RequestSave(new LadaLayoutCollection { Ladas = { new LadaLayout { Title = "Final" } } });

        await Task.Delay(300);

        var loaded = manager.Load();
        Assert.Equal("Final", Assert.Single(loaded.Ladas).Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
