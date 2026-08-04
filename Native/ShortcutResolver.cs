using System;
using System.Runtime.InteropServices;
using System.Text;
using Lada.Services;

namespace Lada.Native;

// Resolving a .lnk to its real target lets icon lookup use the target's own
// icon instead of the shortcut's — Explorer always draws a .lnk's icon with
// the little arrow overlay, but a plain .exe path has no reason to get one.
// The desktop reference itself (LadaItem.Path) still points at the .lnk;
// only icon lookup uses the resolved path.
public static class ShortcutResolver
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
    }

    // Only Load is ever called, but GetClassID/IsDirty must still be declared
    // in their real vtable order — COM interop resolves methods by
    // declaration position, not by name.
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    }

    public static string? ResolveTarget(string shortcutPath)
    {
        try
        {
            var link = (IShellLinkW)new ShellLink();
            ((IPersistFile)link).Load(shortcutPath, 0);

            var buffer = new StringBuilder(260);
            link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);

            var target = buffer.ToString();
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(ShortcutResolver), ex);
            return null;
        }
    }
}
