using System.Collections.Generic;
using System.Drawing;

namespace Lada.Services;

public static class MonitorLayoutService
{
    private const int FallbackOffset = 100;
    private const int CascadeStep = 24;

    public static bool IsOffScreen(Rectangle ladaBounds, IReadOnlyList<Rectangle> screenBounds)
    {
        foreach (var screen in screenBounds)
        {
            if (ladaBounds.IntersectsWith(screen))
                return false;
        }

        return true;
    }

    public static Point ComputeFallbackPosition(Rectangle primaryScreenBounds, int cascadeIndex)
    {
        var offset = FallbackOffset + cascadeIndex * CascadeStep;
        return new Point(primaryScreenBounds.Left + offset, primaryScreenBounds.Top + offset);
    }
}
