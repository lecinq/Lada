using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class AppearanceCustomizationManagerTests
{
    [Theory]
    [InlineData(-25, 0)]
    [InlineData(73.6, 74)]
    [InlineData(250, 200)]
    public void ApplyBrightness_ClampsAndRounds(double requested, double expected)
    {
        var manager = new AppearanceCustomizationManager();

        manager.ApplyBrightness(requested);

        Assert.Equal(expected, manager.BrightnessPercent);
    }

    [Fact]
    public void ApplyBrightness_RaisesChangedOnlyWhenValueChanges()
    {
        var manager = new AppearanceCustomizationManager();
        var changeCount = 0;
        manager.Changed += () => changeCount++;

        manager.ApplyBrightness(100);
        manager.ApplyBrightness(125);
        manager.ApplyBrightness(125);

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ApplyBackgroundColor_NormalizesAndRaisesChangedOnce()
    {
        var manager = new AppearanceCustomizationManager();
        var changeCount = 0;
        manager.Changed += () => changeCount++;

        manager.ApplyBackgroundColor("33ff33");
        manager.ApplyBackgroundColor("#33FF33");

        Assert.Equal("#33FF33", manager.BackgroundColorHex);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ResetBackgroundColor_RestoresThemeDefaultState()
    {
        var manager = new AppearanceCustomizationManager();
        manager.ApplyBackgroundColor("#123456");

        manager.ResetBackgroundColor();

        Assert.Null(manager.BackgroundColorHex);
    }
}
