using System;
using System.Collections.Generic;
using System.Drawing;

namespace Lada.Services;

// All geometry here is in physical pixels (System.Drawing.Rectangle/Point),
// matching MonitorLayoutService's existing convention -- never WPF's
// Window.Left/Top (DIPs). See this plan's Global Constraints.
public static class WindowSnapCalculator
{
    public const int SnapThreshold = 15;

    public static Point ComputeSnappedPosition(Rectangle proposed, IReadOnlyList<SnapCandidate> candidates)
    {
        var left = SnapAxis(proposed.Left, proposed.Width, candidates, horizontal: true) ?? proposed.Left;
        var top = SnapAxis(proposed.Top, proposed.Height, candidates, horizontal: false) ?? proposed.Top;
        return new Point(left, top);
    }

    // Up to 4 possible snaps per candidate per axis: near-to-near (align),
    // near-to-far (attach after), far-to-near (attach before), far-to-far
    // (align on the far side) -- picks whichever candidate value is
    // closest to `near`, across every candidate, within SnapThreshold.
    //
    // Screen-edge candidates only ever offer the two "align inside" variants
    // (near-to-near, far-to-far). The two "attach outside" variants
    // (near-to-far, far-to-near) place the dragged window flush against the
    // OUTSIDE of the candidate -- correct for butting up against a neighbor
    // window, but for a screen edge that means locking the window fully off
    // that screen. This was the root cause of a lada getting snapped
    // off-screen and stuck there while dragging near a screen corner.
    private static int? SnapAxis(int near, int size, IReadOnlyList<SnapCandidate> candidates, bool horizontal)
    {
        int? best = null;
        var bestDistance = SnapThreshold;

        foreach (var candidate in candidates)
        {
            var (candidateNear, candidateFar) = horizontal
                ? (candidate.Bounds.Left, candidate.Bounds.Right)
                : (candidate.Bounds.Top, candidate.Bounds.Bottom);

            var targets = candidate.Kind == SnapCandidateKind.Screen
                ? new[] { candidateNear, candidateFar - size }
                : new[] { candidateNear, candidateFar, candidateNear - size, candidateFar - size };

            foreach (var target in targets)
            {
                var distance = Math.Abs(target - near);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target;
                }
            }
        }

        return best;
    }
}

public enum SnapCandidateKind
{
    Window,
    Screen
}

public readonly record struct SnapCandidate(Rectangle Bounds, SnapCandidateKind Kind);
