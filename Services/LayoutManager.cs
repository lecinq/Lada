using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Lada.Models;

namespace Lada.Services;

public sealed class LayoutManager : IDisposable
{
    private readonly string _filePath;
    private readonly TimeSpan _debounceInterval;
    private readonly object _lock = new();
    private Timer? _debounceTimer;
    private LadaLayoutCollection? _pendingLayout;

    public LayoutManager(string filePath, TimeSpan? debounceInterval = null)
    {
        _filePath = filePath;
        _debounceInterval = debounceInterval ?? TimeSpan.FromMilliseconds(500);
    }

    public static string GetDefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lada");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "layout.json");
    }

    public LadaLayoutCollection Load()
    {
        if (!File.Exists(_filePath))
        {
            var empty = new LadaLayoutCollection();
            empty.ApplyMigrations();
            return empty;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var layout = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options)
                         ?? new LadaLayoutCollection();
            layout.ApplyMigrations();
            return layout;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            var empty = new LadaLayoutCollection();
            empty.ApplyMigrations();
            return empty;
        }
    }

    public void SaveImmediate(LadaLayoutCollection layout)
    {
        layout.ApplyMigrations();
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(layout, LadaJson.Options);
        File.WriteAllText(tempPath, json);

        if (File.Exists(_filePath))
            File.Replace(tempPath, _filePath, destinationBackupFileName: null);
        else
            File.Move(tempPath, _filePath);
    }

    public void RequestSave(LadaLayoutCollection layout)
    {
        lock (_lock)
        {
            _pendingLayout = layout;
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceElapsed, null, _debounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        LadaLayoutCollection? layout;
        lock (_lock)
        {
            layout = _pendingLayout;
            _pendingLayout = null;
        }

        if (layout is not null)
            SaveImmediate(layout);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _debounceTimer?.Dispose();
        }
    }
}
