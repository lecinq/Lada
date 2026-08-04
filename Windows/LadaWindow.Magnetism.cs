using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private MagnetismManager? _magnetismManager;
    private Func<IEnumerable<LadaWindow>>? _getAllLadaWindows;
    private List<SnapCandidate>? _dragSnapCandidates;

    // Called once from TitleBar_MouseLeftButtonDown -- gathering candidates
    // is cheap but not free (Screen.AllScreens re-queries the OS, and
    // iterating every other lada allocates), so it's done once per drag
    // rather than on every MouseMove. Nothing else can move another lada
    // mid-drag (only one mouse), so this stays valid for the whole drag.
    private List<SnapCandidate> GatherSnapCandidates()
    {
        var candidates = new List<SnapCandidate>();

        if (_getAllLadaWindows is not null)
        {
            candidates.AddRange(_getAllLadaWindows()
                .Where(w => !ReferenceEquals(w, this) && w.Visibility == Visibility.Visible)
                .Select(w => new SnapCandidate(w.GetPhysicalBounds(), SnapCandidateKind.Window)));
        }

        candidates.AddRange(System.Windows.Forms.Screen.AllScreens
            .Select(s => new SnapCandidate(s.WorkingArea, SnapCandidateKind.Screen)));

        return candidates;
    }

    // screenDelta comes from TitleBar_MouseMove's PointToScreen-based
    // tracking, already in device (physical) pixels -- see this plan's
    // Global Constraints on never mixing DIPs with physical pixels.
    private void ApplyManualDragMove(Vector screenDelta)
    {
        var current = GetPhysicalBounds();
        var proposedX = current.X + (int)Math.Round(screenDelta.X);
        var proposedY = current.Y + (int)Math.Round(screenDelta.Y);

        int targetX, targetY;
        if (_dragSnapCandidates is not null)
        {
            var proposed = new Rectangle(proposedX, proposedY, current.Width, current.Height);
            var snapped = WindowSnapCalculator.ComputeSnappedPosition(proposed, _dragSnapCandidates);
            var snappedBounds = new Rectangle(snapped.X, snapped.Y, current.Width, current.Height);

            // A snap must never leave the window LESS visible than the raw
            // drag already would have. E.g. attaching to a neighbor lada
            // that itself sits flush against a screen edge computes a
            // position beyond that edge, with no awareness that there's no
            // screen left past it. If a snap would push an otherwise-visible
            // window fully off every screen, ignore it and fall back to the
            // raw (unsnapped) position instead.
            var screenBounds = System.Windows.Forms.Screen.AllScreens.Select(s => s.Bounds).ToList();
            if (MonitorLayoutService.IsOffScreen(snappedBounds, screenBounds) &&
                !MonitorLayoutService.IsOffScreen(proposed, screenBounds))
            {
                (targetX, targetY) = (proposedX, proposedY);
            }
            else
            {
                (targetX, targetY) = (snapped.X, snapped.Y);
            }
        }
        else
        {
            (targetX, targetY) = (proposedX, proposedY);
        }

        // Moved via WPF's own Left/Top (converted back from physical pixels
        // to DIPs) rather than a raw SetWindowPos P/Invoke call, to keep the
        // move on WPF's own render-scheduling pipeline. Only the interactive
        // drag path needs this; the one-shot, non-interactive moves
        // (EnsureVisible, Ranger arrange) keep using SetPhysicalPosition
        // since a single move has no continuous redraw to desync from.
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = targetX / dpi.DpiScaleX;
        Top = targetY / dpi.DpiScaleY;
    }
}
