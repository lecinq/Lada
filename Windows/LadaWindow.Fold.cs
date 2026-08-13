using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Lada.Windows;

public partial class LadaWindow
{
    private double _expandedHeight;
    private bool _isFolded;

    private void ToggleFold()
    {
        // A widget is already as small as its component needs -- there's
        // nothing smaller to collapse to, and with chrome hidden there's no
        // title bar left to double-click anyway.
        if (_isWidget)
            return;

        // _expandedHeight and every Height value here are the padded
        // (Height-DP) value throughout, consistent with how the rest of
        // LadaWindow treats Height -- see HudGlowMargin (LadaWindow.HudGlow.cs).
        if (!_isFolded)
        {
            _expandedHeight = Height;
            AnimateHeight(Height, TitleBarHeight + 2 * HudGlowMargin);
            _isFolded = true;
        }
        else
        {
            AnimateHeight(Height, _expandedHeight > 0 ? _expandedHeight : 240 + 2 * HudGlowMargin);
            _isFolded = false;
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AnimateHeight(double from, double to)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // WPF's default FillBehavior (HoldEnd) keeps this animation "owning"
        // HeightProperty even after it finishes -- any later direct Height =
        // assignment elsewhere (ApplyGridSizePreset, ResizeThumb_DragDelta,
        // EnsureContentFits) would silently no-op from then on, while Width
        // keeps working normally since it's never animated. Releasing the
        // animation clock and setting the plain property to the same final
        // value on Completed keeps the visual result identical but hands
        // control of Height back to ordinary code.
        animation.Completed += (_, _) =>
        {
            BeginAnimation(HeightProperty, null);
            Height = to;
        };
        BeginAnimation(HeightProperty, animation);
    }
}
