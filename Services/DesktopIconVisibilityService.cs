using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Lada.Native;

namespace Lada.Services;

// Masque/restaure l'icône réelle d'un fichier du bureau Windows en la
// repositionnant hors de la grille visible, jamais en déplaçant ou
// renommant le fichier lui-même. Technique validée par un test jetable
// réel pendant le brainstorming (fonctionne avec et sans "Réorganisation
// automatique des icônes" activée). Même famille de manipulation
// cross-process qu'utilise déjà Native/MouseHook.cs pour le double-clic
// bureau (SysListView32 d'Explorer, un autre processus).
public static class DesktopIconVisibilityService
{
    private const int OffscreenX = -5000;
    private const int OffscreenY = -5000;

    public static bool TryHide(string fullPath, out int originalX, out int originalY)
    {
        var capturedX = 0;
        var capturedY = 0;

        try
        {
            var result = WithDesktopListView((hProcess, listView) =>
            {
                var index = FindItemIndex(hProcess, listView, fullPath);
                if (index < 0)
                    return false;

                if (!TryGetItemPosition(hProcess, listView, index, out capturedX, out capturedY))
                    return false;

                SetItemPosition(listView, index, OffscreenX, OffscreenY);
                return true;
            });

            originalX = capturedX;
            originalY = capturedY;
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(DesktopIconVisibilityService), ex);
            originalX = 0;
            originalY = 0;
            return false;
        }
    }

    public static void Restore(string fullPath, int x, int y)
    {
        try
        {
            WithDesktopListView((hProcess, listView) =>
            {
                var index = FindItemIndex(hProcess, listView, fullPath);
                if (index < 0)
                    return false;

                SetItemPosition(listView, index, x, y);
                return true;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(DesktopIconVisibilityService), ex);
        }
    }

    private static bool WithDesktopListView(Func<IntPtr, IntPtr, bool> action)
    {
        var listView = FindDesktopListView();
        if (listView == IntPtr.Zero)
            return false;

        NativeMethods.GetWindowThreadProcessId(listView, out var pid);
        var hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_QUERY_INFORMATION,
            false, pid);
        if (hProcess == IntPtr.Zero)
            return false;

        try
        {
            return action(hProcess, listView);
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }

    // Progman -> SHELLDLL_DefView -> SysListView32 directement sur certaines
    // configs ; sur d'autres (observé sur Windows 10/11) SHELLDLL_DefView
    // vit plutôt sous un WorkerW frère, d'où le repli par énumération.
    private static IntPtr FindDesktopListView()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        var defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
        {
            var found = IntPtr.Zero;
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                var candidate = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (candidate != IntPtr.Zero)
                {
                    found = candidate;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            defView = found;
        }

        return defView == IntPtr.Zero
            ? IntPtr.Zero
            : NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
    }

    // L'étiquette affichée par Explorer pour une icône dépend du réglage
    // utilisateur "Masquer les extensions des fichiers dont le type est
    // connu" — le nom réel du fichier ne correspond donc pas toujours à ce
    // que LVM_FINDITEMW voit. On essaie le nom complet d'abord, puis sans
    // extension. Cas limite accepté : deux fichiers de même nom de base
    // avec des extensions différentes (ex. "photo.txt" et "photo.png")
    // pourraient collisionner si les extensions sont masquées — pas de
    // résolution par PIDL pour cette itération.
    private static int FindItemIndex(IntPtr hProcess, IntPtr listView, string fullPath)
    {
        var withExtension = Path.GetFileName(fullPath);
        var index = FindItemIndexByName(hProcess, listView, withExtension);
        if (index >= 0)
            return index;

        var withoutExtension = Path.GetFileNameWithoutExtension(fullPath);
        return withoutExtension == withExtension
            ? -1
            : FindItemIndexByName(hProcess, listView, withoutExtension);
    }

    // LVM_FINDITEMW cible une fenêtre possédée par explorer.exe (un autre
    // processus) : la chaîne à chercher doit vivre dans SON espace mémoire,
    // pas le nôtre, d'où l'allocation/écriture distante avant l'envoi du
    // message. Voir Native/MouseHook.cs pour le même principe appliqué à
    // LVM_HITTEST.
    private static int FindItemIndexByName(IntPtr hProcess, IntPtr listView, string name)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name + "\0");
        var remoteNamePtr = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, (uint)nameBytes.Length, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
        if (remoteNamePtr == IntPtr.Zero)
            return -1;

        var findInfo = new LVFINDINFOW { Flags = NativeMethods.LVFI_STRING, Psz = remoteNamePtr };
        var findInfoBytes = new byte[Marshal.SizeOf<LVFINDINFOW>()];
        var handle = GCHandle.Alloc(findInfoBytes, GCHandleType.Pinned);
        Marshal.StructureToPtr(findInfo, handle.AddrOfPinnedObject(), false);
        var remoteFindInfoPtr = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, (uint)findInfoBytes.Length, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);

        try
        {
            if (remoteFindInfoPtr == IntPtr.Zero)
                return -1;

            NativeMethods.WriteProcessMemory(hProcess, remoteNamePtr, nameBytes, nameBytes.Length, out _);
            NativeMethods.WriteProcessMemory(hProcess, remoteFindInfoPtr, findInfoBytes, findInfoBytes.Length, out _);

            return NativeMethods.SendMessage(listView, NativeMethods.LVM_FINDITEMW, new IntPtr(-1), remoteFindInfoPtr).ToInt32();
        }
        finally
        {
            handle.Free();
            NativeMethods.VirtualFreeEx(hProcess, remoteNamePtr, 0, NativeMethods.MEM_RELEASE);
            NativeMethods.VirtualFreeEx(hProcess, remoteFindInfoPtr, 0, NativeMethods.MEM_RELEASE);
        }
    }

    private static bool TryGetItemPosition(IntPtr hProcess, IntPtr listView, int index, out int x, out int y)
    {
        x = 0;
        y = 0;
        var remotePointPtr = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, (uint)Marshal.SizeOf<POINT>(), NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
        if (remotePointPtr == IntPtr.Zero)
            return false;

        try
        {
            NativeMethods.SendMessage(listView, NativeMethods.LVM_GETITEMPOSITION, new IntPtr(index), remotePointPtr);
            var posBytes = new byte[Marshal.SizeOf<POINT>()];
            if (!NativeMethods.ReadProcessMemory(hProcess, remotePointPtr, posBytes, posBytes.Length, out _))
                return false;

            var handle = GCHandle.Alloc(posBytes, GCHandleType.Pinned);
            var point = Marshal.PtrToStructure<POINT>(handle.AddrOfPinnedObject());
            handle.Free();
            x = point.X;
            y = point.Y;
            return true;
        }
        finally
        {
            NativeMethods.VirtualFreeEx(hProcess, remotePointPtr, 0, NativeMethods.MEM_RELEASE);
        }
    }

    private static void SetItemPosition(IntPtr listView, int index, int x, int y)
    {
        var packed = (IntPtr)((y << 16) | (x & 0xFFFF));
        NativeMethods.SendMessage(listView, NativeMethods.LVM_SETITEMPOSITION, new IntPtr(index), packed);
    }
}
