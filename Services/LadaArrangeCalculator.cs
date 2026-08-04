using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Lada.Services;

// Physical pixels throughout, same reasoning as WindowSnapCalculator.
public static class LadaArrangeCalculator
{
    public const int Gap = 20;

    // The i-th input Rectangle (position+size) corresponds to the i-th
    // output Rectangle (new position, same size). Fill order (which one
    // gets the top-left-most slot) is computed internally from current
    // position (reading order: Top then Left), independent of input order.
    public static List<Rectangle> Arrange(IReadOnlyList<Rectangle> currentBounds, Rectangle screenBounds)
    {
        var order = Enumerable.Range(0, currentBounds.Count)
            .OrderBy(i => currentBounds[i].Top)
            .ThenBy(i => currentBounds[i].Left)
            .ToList();

        var results = new Rectangle[currentBounds.Count];
        var x = screenBounds.Left + Gap;
        var y = screenBounds.Top + Gap;
        var rowHeight = 0;

        foreach (var i in order)
        {
            var size = currentBounds[i];
            if (x + size.Width > screenBounds.Right && x > screenBounds.Left + Gap)
            {
                x = screenBounds.Left + Gap;
                y += rowHeight + Gap;
                rowHeight = 0;
            }

            results[i] = new Rectangle(x, y, size.Width, size.Height);
            x += size.Width + Gap;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        return results.ToList();
    }
}
