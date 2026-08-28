using System.Windows.Media;
using Lada.Models;
using Lada.Resources;
using Xunit;

namespace Lada.Tests.Resources;

public class ColorPaletteTests
{
    [Theory]
    [InlineData(AppTheme.Midnight)]
    [InlineData(AppTheme.Modernism)]
    [InlineData(AppTheme.Anderson)]
    [InlineData(AppTheme.Forecast)]
    [InlineData(AppTheme.Howard)]
    public void ForTheme_ReturnsEightParseableColors(AppTheme theme)
    {
        var colors = ColorPalette.ForTheme(theme);

        Assert.Equal(8, colors.Count);
        foreach (var hex in colors)
        {
            ColorConverter.ConvertFromString(hex);
        }
    }

    [Fact]
    public void ForTheme_MidnightAndModernism_ReturnDifferentPalettes()
    {
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Midnight), ColorPalette.ForTheme(AppTheme.Modernism));
    }

    [Fact]
    public void ForTheme_AndersonReturnsDifferentPaletteFromMidnightAndModernism()
    {
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Anderson), ColorPalette.ForTheme(AppTheme.Midnight));
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Anderson), ColorPalette.ForTheme(AppTheme.Modernism));
    }

    [Fact]
    public void ForTheme_ForecastReturnsItsOwnPalette()
    {
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Forecast), ColorPalette.ForTheme(AppTheme.Midnight));
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Forecast), ColorPalette.ForTheme(AppTheme.Modernism));
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Forecast), ColorPalette.ForTheme(AppTheme.Anderson));
    }

    [Fact]
    public void ForTheme_HowardReturnsItsOwnPalette()
    {
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Howard), ColorPalette.ForTheme(AppTheme.Midnight));
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Howard), ColorPalette.ForTheme(AppTheme.Modernism));
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Howard), ColorPalette.ForTheme(AppTheme.Anderson));
        Assert.NotEqual(ColorPalette.ForTheme(AppTheme.Howard), ColorPalette.ForTheme(AppTheme.Forecast));
    }
}
