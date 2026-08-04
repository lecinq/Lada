using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Lada.Native;

namespace Lada.Services;

public static class ShellIconService
{
    public static BitmapSource? GetIcon(string path)
    {
        try
        {
            // Shell32 only draws the shortcut-arrow overlay when the path it's
            // asked about is itself recognized as a .lnk — looking up the
            // resolved target's icon instead gives a clean icon with no arrow.
            var iconLookupPath = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                ? ShortcutResolver.ResolveTarget(path) ?? path
                : path;

            var shinfo = new SHFILEINFO();
            var result = NativeMethods.SHGetFileInfo(
                iconLookupPath, 0, ref shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(),
                NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                return Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                NativeMethods.DestroyIcon(shinfo.hIcon);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(ShellIconService), ex);
            return null;
        }
    }

    public static string? GetTypeName(string path)
    {
        try
        {
            var shinfo = new SHFILEINFO();
            var result = NativeMethods.SHGetFileInfo(
                path, 0, ref shinfo, (uint)Marshal.SizeOf<SHFILEINFO>(),
                NativeMethods.SHGFI_TYPENAME);

            return result == IntPtr.Zero ? null : shinfo.szTypeName;
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(ShellIconService), ex);
            return null;
        }
    }
}
