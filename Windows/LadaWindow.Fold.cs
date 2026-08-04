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
        if (!_isFolded)
        {
            _expandedHeight = Height;
            AnimateHeight(Height, TitleBarHeight);
            _isFolded = true;
        }
        else
        {
            AnimateHeight(Height, _expandedHeight > 0 ? _expandedHeight : 240);
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
