using System;
using System.Collections.Generic;
using System.IO;

namespace Lada.Services;

public enum FileCategory
{
    Folder,
    Document,
    Image,
    Video,
    Audio,
    Executable,
    Other
}

public static class FileTypeCategorizer
{
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".doc", ".docx", ".pdf", ".txt", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".rtf", ".csv" };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".ico" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm" };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".lnk", ".msi", ".bat", ".cmd" };

    public static FileCategory Categorize(string path)
    {
        if (Directory.Exists(path))
            return FileCategory.Folder;

        var extension = Path.GetExtension(path);

        if (DocumentExtensions.Contains(extension)) return FileCategory.Document;
        if (ImageExtensions.Contains(extension)) return FileCategory.Image;
        if (VideoExtensions.Contains(extension)) return FileCategory.Video;
        if (AudioExtensions.Contains(extension)) return FileCategory.Audio;
        if (ExecutableExtensions.Contains(extension)) return FileCategory.Executable;

        return FileCategory.Other;
    }
}
