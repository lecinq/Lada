using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Lada.Services;

public static class ImageThumbnailService
{
    private const int DecodePixelWidth = 32;

    private static readonly Dictionary<string, BitmapSource> Cache = new();

    public static BitmapSource? GetThumbnail(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.DecodePixelWidth = DecodePixelWidth;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            Cache[path] = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(ImageThumbnailService), ex);
            return null;
        }
    }
}
