using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Lada.Native;

namespace Lada.Services;

// WS_EX_NOACTIVATE (see Native/NativeMethods.cs) keeps a lada's own window
// from ever stealing keyboard focus on an ordinary click, but it has a side
// effect: Windows never sends that window the WM_ACTIVATE/WM_KILLFOCUS
// notification it would normally send when the user clicks a different
// top-level window. WPF's own Popup-closes-on-outside-click and a TextBox's
// LostFocus both depend on that notification, so neither fires for a click
// on a genuinely separate window (another lada, the desktop, another app) --
// confirmed via a throwaway repro: GetForegroundWindow() changed but
// Popup.IsOpen and TextBox.IsKeyboardFocused both stayed stuck.
//
// This watches every left/right mouse-down system-wide through a low-level
// hook and calls back into whichever registered listener the click landed
// outside of, standing in for the native signal this window style suppresses.
// The hook is only installed while at least one listener is registered
// (a context menu open, a title being renamed) and removed once none are.
public static class OutsideClickWatcher
{
    private const int WH_MOUSE_LL = 14;
    private static readonly IntPtr WM_LBUTTONDOWN = new(0x0201);
    private static readonly IntPtr WM_RBUTTONDOWN = new(0x0204);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    private sealed class Registration : IDisposable
    {
        private readonly Func<int, int, bool> _isInside;
        private readonly Action _onOutsideClick;
        private bool _disposed;

        public Registration(Func<int, int, bool> isInside, Action onOutsideClick)
        {
            _isInside = isInside;
            _onOutsideClick = onOutsideClick;
        }

        public bool Contains(int x, int y) => _isInside(x, y);
        public void NotifyOutsideClick() => _onOutsideClick();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Unregister(this);
        }
    }

    private static readonly List<Registration> _registrations = new();
    // Kept as a field, not a local: SetWindowsHookEx doesn't root the
    // delegate itself, so without this the GC is free to collect it while
    // the hook is still installed and native code still calls into it.
    private static readonly LowLevelMouseProc _hookProc = HookCallback;
    private static IntPtr _hookHandle = IntPtr.Zero;

    // isInside receives the click's screen coordinates and reports whether
    // that click is inside the region this listener owns (its own window,
    // an open popup's bounds). Any click outside that region invokes
    // onOutsideClick. Both callbacks run on the UI thread.
    public static IDisposable Watch(Func<int, int, bool> isInside, Action onOutsideClick)
    {
        var registration = new Registration(isInside, onOutsideClick);
        _registrations.Add(registration);
        EnsureHookInstalled();
        return registration;
    }

    public static bool IsPointInsideWindow(IntPtr hwnd, int x, int y)
    {
        if (hwnd == IntPtr.Zero)
            return false;
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            return false;
        return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
    }

    private static void Unregister(Registration registration)
    {
        _registrations.Remove(registration);
        if (_registrations.Count == 0)
        {
            EnsureHookRemoved();
        }
    }

    private static void EnsureHookInstalled()
    {
        if (_hookHandle != IntPtr.Zero)
            return;

        // Low-level hooks run on the installing thread's own message loop
        // (WPF's Dispatcher here) rather than being injected into other
        // processes, so hMod/dwThreadId are left null/zero as documented
        // for WH_MOUSE_LL.
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            Logger.LogError(nameof(OutsideClickWatcher), new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static void EnsureHookRemoved()
    {
        if (_hookHandle == IntPtr.Zero)
            return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN) && _registrations.Count > 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            // Snapshot first: a callback can synchronously Dispose its own
            // registration (or another one), which would otherwise mutate
            // _registrations while this loop is iterating it.
            foreach (var registration in _registrations.ToArray())
            {
                if (!registration.Contains(data.Pt.X, data.Pt.Y))
                {
                    registration.NotifyOutsideClick();
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
