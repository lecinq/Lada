using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Lada.Native;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    // All position math here happens in physical pixels via GetWindowRect
    // and SetWindowPos, never via this Window's own Left/Top (DIPs) — see
    // the plan's global constraints for why mixing the two is wrong on any
    // monitor that isn't at 100% scaling.
    public bool EnsureVisible(int cascadeIndex)
    {
        if (_hwnd == IntPtr.Zero)
            return false;

        NativeMethods.GetWindowRect(_hwnd, out var rect);
        var ladaBounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

        var screenBounds = Screen.AllScreens.Select(s => s.Bounds).ToList();

        if (!MonitorLayoutService.IsOffScreen(ladaBounds, screenBounds))
            return false;

        var primaryBounds = Screen.PrimaryScreen!.Bounds;
        var target = MonitorLayoutService.ComputeFallbackPosition(primaryBounds, cascadeIndex);

        NativeMethods.SetWindowPos(
            _hwnd, IntPtr.Zero, target.X, target.Y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        return true;
    }

    // Physical pixels (via GetWindowRect), for cross-window position
    // comparisons -- never compare this against Window.Left/Top (DIPs)
    // directly, see this plan's Global Constraints.
    public Rectangle GetPhysicalBounds()
    {
        NativeMethods.GetWindowRect(_hwnd, out var rect);
        return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    // Moves the window via SetWindowPos (physical pixels). WPF's own
    // Left/Top update themselves reactively afterward (fact-checked live
    // this session), so no manual assignment is needed, and
    // LocationChanged still fires normally for persistence.
    public void SetPhysicalPosition(int x, int y)
    {
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }
}
