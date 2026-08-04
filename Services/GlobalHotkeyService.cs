using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Lada.Native;
using Lada.Resources;

namespace Lada.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int OverlayHotkeyId = 1;
    private const int ToggleAllHotkeyId = 2;

    // A hidden, never-shown window just to own a message loop that can
    // receive WM_HOTKEY — RegisterHotKey needs a real HWND on the calling
    // thread, but it doesn't need to be visible.
    private readonly Window _messageWindow = new()
    {
        Width = 0,
        Height = 0,
        WindowStyle = WindowStyle.None,
        ShowInTaskbar = false,
        Visibility = Visibility.Hidden
    };

    private HwndSource? _source;

    public event Action? OverlayRequested;
    public event Action? ToggleAllRequested;
    public event Action<string>? HotkeyRegistrationFailed;

    public void Start()
    {
        var hwnd = new WindowInteropHelper(_messageWindow).EnsureHandle();
        _source = HwndSource.FromHwnd(hwnd);
        _source!.AddHook(WndProc);

        RegisterOrWarn(hwnd, OverlayHotkeyId, NativeMethods.VK_O, Strings.OverlayHotkeyDescription);
        RegisterOrWarn(hwnd, ToggleAllHotkeyId, NativeMethods.VK_D, Strings.ToggleAllHotkeyDescription);
    }

    // RegisterHotKey fails silently (just returns false) if another process
    // already grabbed the same combination — no exception, no WM_HOTKEY ever
    // arrives, and there's no other symptom. Surfacing the actual Win32
    // error here (rather than guessing at a fix blind) is how we'd tell a
    // real collision apart from something else going wrong.
    private void RegisterOrWarn(IntPtr hwnd, int id, uint vk, string description)
    {
        var modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
        if (NativeMethods.RegisterHotKey(hwnd, id, modifiers, vk))
            return;

        var error = Marshal.GetLastWin32Error();
        var win32Message = new Win32Exception(error).Message;
        Logger.LogError(nameof(GlobalHotkeyService), new Win32Exception(error));
        HotkeyRegistrationFailed?.Invoke(Strings.HotkeyUnavailable(description, win32Message, error));
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            switch (wParam.ToInt32())
            {
                case OverlayHotkeyId:
                    OverlayRequested?.Invoke();
                    handled = true;
                    break;
                case ToggleAllHotkeyId:
                    ToggleAllRequested?.Invoke();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        var hwnd = new WindowInteropHelper(_messageWindow).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(hwnd, OverlayHotkeyId);
            NativeMethods.UnregisterHotKey(hwnd, ToggleAllHotkeyId);
        }

        _source?.RemoveHook(WndProc);
        _messageWindow.Close();
    }
}
