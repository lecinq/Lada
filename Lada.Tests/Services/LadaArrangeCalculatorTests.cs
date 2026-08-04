using System.Collections.Generic;
using System.Drawing;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class LadaArrangeCalculatorTests
{
    private static readonly Rectangle Screen = new(0, 0, 1000, 800);

    [Fact]
    public void Arrange_SingleWindow_PlacesAtTopLeftCornerWithGap()
    {
        var input = new List<Rectangle> { new(500, 500, 320, 240) };
        var result = LadaArrangeCalculator.Arrange(input, Screen);

        Assert.Equal(new Rectangle(20, 20, 320, 240), result[0]);
    }

    [Fact]
    public void Arrange_MultipleWindowsFittingOneRow_LinesUpLeftToRight()
    {
        var input = new List<Rectangle>
        {
            new(100, 100, 200, 100),
            new(400, 100, 200, 100),
            new(700, 100, 200, 100),
        };
        var result = LadaArrangeCalculator.Arrange(input, Screen);

        Assert.Equal(20, result[0].Y);
        Assert.Equal(20, result[1].Y);
        Assert.Equal(20, result[2].Y);
        Assert.Equal(20, result[0].X);
        Assert.Equal(20 + 200 + 20, result[1].X);
        Assert.Equal(20 + 200 + 20 + 200 + 20, result[2].X);
    }

    [Fact]
    public void Arrange_WindowThatDoesNotFitRemainingWidth_WrapsToNextRow()
    {
        var narrowScreen = new Rectangle(0, 0, 500, 800);
        var input = new List<Rectangle>
        {
            new(0, 0, 300, 100),
            new(0, 0, 300, 100), // 20 + 300 + 20 + 300 > 500, must wrap
        };
        var result = LadaArrangeCalculator.Arrange(input, narrowScreen);

        Assert.Equal(20, result[0].Y);
        Assert.True(result[1].Y > result[0].Y, "second window should have wrapped to a new row");
        Assert.Equal(20, result[1].X);
    }

    [Fact]
    public void Arrange_OutputOrderMatchesInputOrder_RegardlessOfFillOrder()
    {
        // Input order deliberately reversed vs. reading order (by position).
        var input = new List<Rectangle>
        {
            new(500, 500, 100, 100), // bottom-right, index 0
            new(0, 0, 100, 100),     // top-left, index 1 -- fills first
        };
        var result = LadaArrangeCalculator.Arrange(input, Screen);

        // index 1 (top-left originally) fills first, so it gets the smaller X/Y
        Assert.True(result[1].X <= result[0].X);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Arrange_VariableSizes_RowHeightTracksTallestInRow()
    {
        var input = new List<Rectangle>
        {
            new(0, 0, 200, 100),
            new(0, 0, 200, 300), // taller -- should push the next row down by 300, not 100
            new(0, 0, 200, 100),
        };
        var wideScreen = new Rectangle(0, 0, 500, 2000); // forces a wrap after 2 (20+200+20+200+20+200 > 500)
        var result = LadaArrangeCalculator.Arrange(input, wideScreen);

        Assert.Equal(20 + 300 + 20, result[2].Y);
    }
}
