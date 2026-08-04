using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Lada.Native;

internal sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const uint LVM_HITTEST = 0x1000 + 18;
    private const int LVHT_ONITEM = 0x0002 | 0x0004 | 0x0008;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LVHITTESTINFO
    {
        public POINT pt;
        public uint flags;
        public int iItem;
        public int iSubItem;
        public int iGroup;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    // Plain IntPtr lParam, not "ref LVHITTESTINFO" — the buffer this points
    // to must live in the TARGET window's process (see IsPointOnListViewItem),
    // never in ours, so there's no managed struct for the marshaler to copy.
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, ref LVHITTESTINFO lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, out LVHITTESTINFO lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private DateTime _lastClickTimeUtc = DateTime.MinValue;
    private POINT _lastClickPoint;

    public event Action<int, int>? DesktopDoubleClicked;

    public MouseHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
    }

    // Runs synchronously on the thread that called Install() (the WPF UI
    // thread), because a low-level hook is pumped through that thread's
    // own message loop — no explicit Dispatcher.Invoke needed here.
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var now = DateTime.UtcNow;
            var elapsedMs = (now - _lastClickTimeUtc).TotalMilliseconds;
            var movedFar = Math.Abs(data.pt.X - _lastClickPoint.X) > 4 || Math.Abs(data.pt.Y - _lastClickPoint.Y) > 4;

            if (elapsedMs <= GetDoubleClickTime() && !movedFar)
            {
                if (IsDesktopEmptyAreaClick(data.pt))
                {
                    DesktopDoubleClicked?.Invoke(data.pt.X, data.pt.Y);
                }
                _lastClickTimeUtc = DateTime.MinValue;
            }
            else
            {
                _lastClickTimeUtc = now;
                _lastClickPoint = data.pt;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsDesktopEmptyAreaClick(POINT screenPoint)
    {
        var hwnd = WindowFromPoint(screenPoint);
        if (hwnd == IntPtr.Zero)
            return false;

        var classNameBuilder = new StringBuilder(256);
        GetClassName(hwnd, classNameBuilder, classNameBuilder.Capacity);

        if (classNameBuilder.ToString() != "SysListView32")
            return false;

        return !IsPointOnListViewItem(hwnd, screenPoint);
    }

    // LVM_HITTEST's lParam is a pointer that the list view's own window
    // procedure reads AND writes directly. The desktop's SysListView32
    // belongs to explorer.exe, a different process — a pointer to a struct
    // on our stack is meaningless (and unmapped) in its address space, so
    // passing one crashes explorer.exe outright rather than failing safely.
    // The documented fix is the same one used by remote-process tooling in
    // general: allocate the buffer IN the target process, write the input
    // there, send the message, then read the result back.
    private static bool IsPointOnListViewItem(IntPtr hwnd, POINT screenPoint)
    {
        var clientPoint = screenPoint;
        ScreenToClient(hwnd, ref clientPoint);

        GetWindowThreadProcessId(hwnd, out var processId);
        var hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE, false, processId);
        if (hProcess == IntPtr.Zero)
            return false;

        try
        {
            var size = Marshal.SizeOf<LVHITTESTINFO>();
            var remoteBuffer = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remoteBuffer == IntPtr.Zero)
                return false;

            try
            {
                var hitTest = new LVHITTESTINFO { pt = clientPoint };
                if (!WriteProcessMemory(hProcess, remoteBuffer, ref hitTest, size, out _))
                    return false;

                SendMessage(hwnd, LVM_HITTEST, IntPtr.Zero, remoteBuffer);

                if (!ReadProcessMemory(hProcess, remoteBuffer, out hitTest, size, out _))
                    return false;

                return (hitTest.flags & LVHT_ONITEM) != 0;
            }
            finally
            {
                VirtualFreeEx(hProcess, remoteBuffer, 0, MEM_RELEASE);
            }
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
