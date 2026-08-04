using System.Collections.Generic;
using System.Drawing;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class WindowSnapCalculatorTests
{
    private static readonly Rectangle Neighbor = new(500, 300, 320, 240);
    private static readonly SnapCandidate NeighborCandidate = new(Neighbor, SnapCandidateKind.Window);

    [Fact]
    public void ComputeSnappedPosition_RightEdgeNearNeighborLeft_SnapsFlush()
    {
        // proposed's right edge (proposed.X + 320) sits 5px left of Neighbor.Left (500)
        var proposed = new Rectangle(500 - 320 - 5, 300, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { NeighborCandidate });
        Assert.Equal(500 - 320, result.X);
    }

    [Fact]
    public void ComputeSnappedPosition_LeftEdgeNearNeighborRight_SnapsFlush()
    {
        var proposed = new Rectangle(Neighbor.Right + 5, 300, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { NeighborCandidate });
        Assert.Equal(Neighbor.Right, result.X);
    }

    [Fact]
    public void ComputeSnappedPosition_LeftEdgesAligned_SnapsToSameLeft()
    {
        var proposed = new Rectangle(Neighbor.Left + 6, 0, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { NeighborCandidate });
        Assert.Equal(Neighbor.Left, result.X);
    }

    [Fact]
    public void ComputeSnappedPosition_RightEdgesAligned_SnapsToSameRight()
    {
        var proposed = new Rectangle(Neighbor.Right - 320 - 7, 0, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { NeighborCandidate });
        Assert.Equal(Neighbor.Right - 320, result.X);
    }

    [Fact]
    public void ComputeSnappedPosition_FarFromEverything_DoesNotSnap()
    {
        var proposed = new Rectangle(0, 0, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { NeighborCandidate });
        Assert.Equal(proposed.X, result.X);
        Assert.Equal(proposed.Y, result.Y);
    }

    [Fact]
    public void ComputeSnappedPosition_TwoCandidates_PicksClosestOne()
    {
        var farNeighbor = new SnapCandidate(new Rectangle(1000, 300, 320, 240), SnapCandidateKind.Window);
        // 3px from Neighbor.Right (closer), 12px from farNeighbor.Left - width (both within threshold)
        var proposed = new Rectangle(Neighbor.Right + 3, 300, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { NeighborCandidate, farNeighbor });
        Assert.Equal(Neighbor.Right, result.X);
    }

    [Fact]
    public void ComputeSnappedPosition_XAndYSnapToDifferentCandidatesIndependently()
    {
        var horizontalNeighbor = new SnapCandidate(new Rectangle(500, 1000, 320, 240), SnapCandidateKind.Window); // far vertically
        var verticalNeighbor = new SnapCandidate(new Rectangle(2000, 315, 320, 240), SnapCandidateKind.Window);   // far horizontally
        var proposed = new Rectangle(horizontalNeighbor.Bounds.Right + 4, verticalNeighbor.Bounds.Top + 4, 320, 240);

        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { horizontalNeighbor, verticalNeighbor });

        Assert.Equal(horizontalNeighbor.Bounds.Right, result.X);
        Assert.Equal(verticalNeighbor.Bounds.Top, result.Y);
    }

    [Fact]
    public void ComputeSnappedPosition_ScreenCandidate_AlignsInsideNearEdge_DoesNotGoOffScreen()
    {
        var screen = new SnapCandidate(new Rectangle(0, 0, 1920, 1040), SnapCandidateKind.Screen);
        var proposed = new Rectangle(1595, 500, 320, 240); // right edge 5px from screen's right edge
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { screen });
        Assert.Equal(1920 - 320, result.X);
    }

    // Regression test for the reported bug: dragging a lada into a screen
    // corner snapped it fully off-screen and stuck it there, because the
    // "attach outside" snap variants (valid for butting up against another
    // window) were also being applied to screen-edge candidates.
    [Fact]
    public void ComputeSnappedPosition_ScreenCandidate_NeverSnapsToOutsideVariant()
    {
        var screen = new SnapCandidate(new Rectangle(0, 0, 1920, 1040), SnapCandidateKind.Screen);
        // proposed's left edge sits 10px from the screen's right edge (1920) --
        // close enough that the old "near-to-far" variant (target =
        // screen.Right = 1920, placing the window entirely off-screen) would
        // have won over the far weaker "align inside" target (1600). The fix
        // removes that variant for screen candidates entirely, so this
        // proposal is simply too far from the only remaining valid targets
        // (0 and 1600) to snap at all -- it must pass through unchanged.
        var proposed = new Rectangle(1910, 20, 320, 240);
        var result = WindowSnapCalculator.ComputeSnappedPosition(proposed, new[] { screen });
        Assert.Equal(proposed.X, result.X);
    }
}
