using System;
using System.IO;

namespace Lada.Services;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lada", "logs", "lada.log");

    public static void LogError(string context, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:O} [{context}] {ex}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never throw or crash the app.
        }
    }
}
