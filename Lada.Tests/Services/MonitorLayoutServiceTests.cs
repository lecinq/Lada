using System.Collections.Generic;
using System.Drawing;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class MonitorLayoutServiceTests
{
    private static readonly Rectangle PrimaryScreen = new(0, 0, 1920, 1080);
    private static readonly Rectangle SecondaryScreenToTheRight = new(1920, 0, 1920, 1080);

    [Fact]
    public void IsOffScreen_ReturnsFalse_WhenLadaIsFullyOnAScreen()
    {
        var lada = new Rectangle(100, 100, 320, 240);

        var result = MonitorLayoutService.IsOffScreen(lada, new[] { PrimaryScreen, SecondaryScreenToTheRight });

        Assert.False(result);
    }

    [Fact]
    public void IsOffScreen_ReturnsFalse_WhenLadaPartiallyOverlapsAScreen()
    {
        // Half on-screen, half hanging off the right edge — still recoverable
        // by the user, so not considered "off-screen".
        var lada = new Rectangle(1800, 100, 320, 240);

        var result = MonitorLayoutService.IsOffScreen(lada, new[] { PrimaryScreen });

        Assert.False(result);
    }

    [Fact]
    public void IsOffScreen_ReturnsTrue_WhenLadaIntersectsNoScreen()
    {
        // Where the primary screen used to be before it was unplugged,
        // now beyond both remaining screens' combined bounds.
        var lada = new Rectangle(5000, 5000, 320, 240);

        var result = MonitorLayoutService.IsOffScreen(lada, new[] { PrimaryScreen, SecondaryScreenToTheRight });

        Assert.True(result);
    }

    [Fact]
    public void IsOffScreen_ReturnsTrue_WhenScreenListIsEmpty()
    {
        var lada = new Rectangle(100, 100, 320, 240);

        var result = MonitorLayoutService.IsOffScreen(lada, new List<Rectangle>());

        Assert.True(result);
    }

    [Fact]
    public void ComputeFallbackPosition_AnchorsToPrimaryScreenTopLeft_WithNoCascade()
    {
        var position = MonitorLayoutService.ComputeFallbackPosition(PrimaryScreen, cascadeIndex: 0);

        Assert.Equal(100, position.X);
        Assert.Equal(100, position.Y);
    }

    [Fact]
    public void ComputeFallbackPosition_CascadesByIndex()
    {
        var position = MonitorLayoutService.ComputeFallbackPosition(PrimaryScreen, cascadeIndex: 2);

        Assert.Equal(148, position.X); // 100 + 2*24
        Assert.Equal(148, position.Y);
    }

    [Fact]
    public void ComputeFallbackPosition_IsRelativeToPrimaryScreenOrigin()
    {
        var offsetPrimaryScreen = new Rectangle(-1920, 0, 1920, 1080); // primary positioned to the left of origin

        var position = MonitorLayoutService.ComputeFallbackPosition(offsetPrimaryScreen, cascadeIndex: 0);

        Assert.Equal(-1820, position.X); // -1920 + 100
        Assert.Equal(100, position.Y);
    }
}
