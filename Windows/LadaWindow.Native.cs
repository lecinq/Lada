using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Lada.Native;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private static readonly TimeSpan BottomReassertInterval = TimeSpan.FromSeconds(4);
    private DispatcherTimer? _bottomReassertTimer;
    private IntPtr _hwnd;

    private void InitializeNativeWindowBehavior()
    {
        SourceInitialized += OnSourceInitialized;
        // SourceInitialized fires before the window is actually shown; the
        // OS then commonly raises a just-launched process's first window to
        // the top regardless of our earlier SetWindowPos(HWND_BOTTOM) call,
        // which otherwise only got corrected on the next periodic tick (up
        // to BottomReassertInterval later, visible as a multi-second flash
        // on top at startup). Re-pin once more right after the window is
        // actually shown to close that gap.
        Loaded += (_, _) => PinToBottom(_hwnd);
        Closed += OnNativeBehaviorClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        PinToBottom(_hwnd);

        _bottomReassertTimer = new DispatcherTimer { Interval = BottomReassertInterval };
        _bottomReassertTimer.Tick += (_, _) => PinToBottom(_hwnd);
        _bottomReassertTimer.Start();
    }

    // WS_EX_NOACTIVATE means an ordinary click never gives this window OS
    // keyboard focus, so title editing (see BeginTitleEdit in
    // LadaWindow.xaml.cs) calls this to explicitly request foreground
    // activation for the duration of the edit. The periodic reassertion
    // above pins it back below other windows afterward regardless.
    private void ActivateForTitleEdit()
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(_hwnd);
    }

    private static void PinToBottom(IntPtr hwnd)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_BOTTOM,
            0, 0, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
    }

    // Overlay temporarily overrides the always-pinned-below-active-windows
    // behavior so every lada becomes reachable on top of whatever else is
    // open. The periodic reassert timer would otherwise fight this (it pins
    // back to HWND_BOTTOM every few seconds), so it's paused for as long as
    // overlay mode is on and resumed when it's turned back off.
    public void SetOverlayMode(bool enabled)
    {
        if (_hwnd == IntPtr.Zero)
            return;

        SetOverlayActiveForHoverFade(enabled);

        if (enabled)
        {
            _bottomReassertTimer?.Stop();
            NativeMethods.SetWindowPos(
                _hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
        }
        else
        {
            PinToBottom(_hwnd);
            _bottomReassertTimer?.Start();
        }
    }

    private void OnNativeBehaviorClosed(object? sender, EventArgs e)
    {
        _bottomReassertTimer?.Stop();
        _bottomReassertTimer = null;
        _titleEditOutsideClickWatch?.Dispose();
        _titleEditOutsideClickWatch = null;
        _iconPickerOutsideClickWatch?.Dispose();
        _iconPickerOutsideClickWatch = null;
    }

    // See OutsideClickWatcher: WS_EX_NOACTIVATE means WPF's own
    // Popup-closes-on-outside-click never fires for a click on a genuinely
    // separate window, so a context menu is force-closed manually here
    // whenever a click lands outside the open popup's own screen bounds.
    //
    // A submenu (e.g. "Taille prédéfinie", "Auto-organisation") opens in its
    // own separate popup HWND, positioned beside the root menu -- checking
    // only the root menu's own bounds treated every click inside an open
    // submenu as "outside", closing the whole menu before the click could
    // even register (found via a checkbox in a submenu never staying
    // checked). Checking against every current WPF surface in this process
    // (main windows plus any open popups) covers submenus without needing
    // to track them individually, while still treating a click on a
    // genuinely different top-level window (the desktop, another app) as
    // outside.
    private void AttachOutsideClickAutoClose(ContextMenu menu)
    {
        IDisposable? watch = null;

        menu.Opened += (_, _) =>
        {
            watch = OutsideClickWatcher.Watch(
                IsPointInsideAnyAppSurface,
                () => Dispatcher.BeginInvoke(() => menu.IsOpen = false));
        };

        menu.Closed += (_, _) =>
        {
            watch?.Dispose();
            watch = null;
        };
    }

    private static bool IsPointInsideAnyAppSurface(int x, int y)
    {
        foreach (PresentationSource source in PresentationSource.CurrentSources)
        {
            if (source is HwndSource hwndSource && OutsideClickWatcher.IsPointInsideWindow(hwndSource.Handle, x, y))
            {
                return true;
            }
        }

        return false;
    }
}
