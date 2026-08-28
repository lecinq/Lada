using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class CustomColorPaletteServiceTests
{
    [Fact]
    public void Remove_IsCaseInsensitiveAndPublishesChange()
    {
        var palette = new CustomColorPaletteService();
        palette.Apply(new[] { "#D91E18", "#0088FF" });
        var changed = false;
        palette.Changed += () => changed = true;

        var removed = palette.Remove("#d91e18");

        Assert.True(removed);
        Assert.True(changed);
        Assert.Equal(new[] { "#0088FF" }, palette.Colors);
    }

    [Fact]
    public void Remove_UnknownColorDoesNotPublishChange()
    {
        var palette = new CustomColorPaletteService();
        palette.Apply(new[] { "#D91E18" });
        var changed = false;
        palette.Changed += () => changed = true;

        var removed = palette.Remove("#FFFFFF");

        Assert.False(removed);
        Assert.False(changed);
        Assert.Single(palette.Colors);
    }
}
