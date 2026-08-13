using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    // Reserved on all four sides of this window, always (not just when HUD
    // Glow is toggled on) -- this window is per-pixel-alpha at a fixed OS
    // pixel size, and its visible content used to fill that size exactly
    // with zero margin, so any effect trying to bleed outward (a real
    // CSS-style box-shadow) had nowhere to render into and got hard-clipped
    // at the window's own edge. Reserving this margin permanently avoids
    // the added complexity/risk of growing and shrinking the OS window
    // every time the toggle flips (including mid-drag). The cost is that
    // it also touches every other piece of code that treats Width/Height/
    // Left/Top as "the visible card's own size/position" -- WPF reactively
    // syncs those DPs to the real underlying HWND rect on every native
    // move/resize (this is why SetPhysicalPosition, LadaWindow.Monitor.cs,
    // already avoided touching them during a live drag), so the margin
    // can't be hidden behind those DPs; every read/write of window size or
    // position elsewhere (LayoutManager persistence via ToLayout/the
    // constructor, the resize chevron's presets and "Fit to content",
    // GetPhysicalBounds/SetPhysicalPosition, and Magnetism's own Left/Top
    // assignment) explicitly adds or subtracts this constant instead.
    private const double HudGlowMargin = 24;

    private const double HudGlowRingThickness = 3;
    private const double HudGlowRingBlurRadius = 16;

    private HudGlowManager? _hudGlowManager;

    private void InitializeHudGlow(HudGlowManager hudGlowManager)
    {
        _hudGlowManager = hudGlowManager;
        _hudGlowManager.Changed += UpdateHudGlow;
        Closed += (_, _) => _hudGlowManager.Changed -= UpdateHudGlow;

        UpdateHudGlow();
    }

    // HudGlowRing (LadaWindow.xaml) is sized to match MainBorder exactly
    // (see UpdateTiltGeometry) but lives as MainBorder's SIBLING, not its
    // child, inside TiltRootContent -- the margin reserved above is real
    // space around it now, not just a couple of pixels squeezed inside
    // MainBorder's own clip, so its blur can actually read as a halo
    // instead of being cut off. Independent of theme (a deliberate opt-in
    // toggle rather than something restricted to whichever theme "looks
    // right" with it) and independent of the Perspective 3D toggle, so
    // either can be used alone or combined. Also called from
    // ApplyThemeColors (LadaWindow.Theme.cs) so switching theme or picking
    // a new accent color re-applies it with the right color instead of
    // leaving a stale one from before.
    private void UpdateHudGlow()
    {
        if (_hudGlowManager is not { Enabled: true })
        {
            HudGlowRing.Visibility = Visibility.Collapsed;
            return;
        }

        var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!);

        HudGlowRing.BorderBrush = accent;
        HudGlowRing.BorderThickness = new Thickness(HudGlowRingThickness);
        HudGlowRing.Effect = new BlurEffect { Radius = HudGlowRingBlurRadius };
        HudGlowRing.Visibility = Visibility.Visible;
    }
}
