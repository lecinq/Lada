using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
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

        var ladaBounds = GetPhysicalBounds();

        var screenBounds = Screen.AllScreens.Select(s => s.Bounds).ToList();

        if (!MonitorLayoutService.IsOffScreen(ladaBounds, screenBounds))
            return false;

        var primaryBounds = Screen.PrimaryScreen!.Bounds;
        var target = MonitorLayoutService.ComputeFallbackPosition(primaryBounds, cascadeIndex);

        SetPhysicalPosition(target.X, target.Y);

        return true;
    }

    // Physical pixels (via GetWindowRect), for cross-window position
    // comparisons -- never compare this against Window.Left/Top (DIPs)
    // directly, see this plan's Global Constraints. Returns the LOGICAL
    // (visible card) rect, not the real HWND rect -- HudGlowMargin
    // (LadaWindow.HudGlow.cs) pads the real window on every side, so every
    // caller of this method (magnetism, off-screen detection, arrange)
    // keeps working against the card's own visible bounds without needing
    // to know the margin exists.
    public Rectangle GetPhysicalBounds()
    {
        NativeMethods.GetWindowRect(_hwnd, out var rect);
        var (marginX, marginY) = GetPhysicalMargin();
        return new Rectangle(
            rect.Left + marginX, rect.Top + marginY,
            (rect.Right - rect.Left) - 2 * marginX, (rect.Bottom - rect.Top) - 2 * marginY);
    }

    // Moves the window via SetWindowPos (physical pixels) so the LOGICAL
    // (visible card) top-left ends up at (x, y) -- offsets by
    // HudGlowMargin internally so callers keep passing the same logical
    // coordinates GetPhysicalBounds returns, unaware of the padding.
    // WPF's own Left/Top update themselves reactively afterward
    // (fact-checked live this session), so no manual assignment is needed,
    // and LocationChanged still fires normally for persistence.
    public void SetPhysicalPosition(int x, int y)
    {
        var (marginX, marginY) = GetPhysicalMargin();
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, x - marginX, y - marginY, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private (int X, int Y) GetPhysicalMargin()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return ((int)Math.Round(HudGlowMargin * dpi.DpiScaleX), (int)Math.Round(HudGlowMargin * dpi.DpiScaleY));
    }
}
