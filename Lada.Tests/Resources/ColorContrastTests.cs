using System.Windows.Media;
using Lada.Resources;
using Xunit;

namespace Lada.Tests.Resources;

public class ColorContrastTests
{
    [Theory]
    [InlineData(255, 255, 255, true)]
    [InlineData(51, 255, 51, true)]
    [InlineData(255, 255, 0, true)]
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 128, false)]
    [InlineData(217, 30, 24, false)]
    public void ForegroundBrush_SelectsReadableBlackOrWhite(byte red, byte green, byte blue, bool expectsBlack)
    {
        var brush = ColorContrast.ForegroundBrush(Color.FromRgb(red, green, blue));

        Assert.Same(expectsBlack ? Brushes.Black : Brushes.White, brush);
    }
}
