using System.Drawing;
using Lada.Models;
using Lada.Resources;
using Xunit;

namespace Lada.Tests.Resources;

public class ThemeSurfaceColorsTests
{
    [Theory]
    [InlineData(AppTheme.Anderson, 0x00, 0x00, 0x00)]
    [InlineData(AppTheme.Modernism, 0xFF, 0xFF, 0xFF)]
    [InlineData(AppTheme.Midnight, 0x00, 0x00, 0x80)]
    public void ForTheme_ReturnsRequestedBaseBackground(
        AppTheme theme,
        int red,
        int green,
        int blue)
    {
        var background = ThemeSurfaceColors.ForTheme(theme).Background;

        Assert.Equal(Color.FromArgb(red, green, blue), background);
    }
}
