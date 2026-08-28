using System.Linq;
using System.Windows.Media;
using Lada.Resources;
using Xunit;

namespace Lada.Tests.Resources;

public class IconLibraryTests
{
    [Fact]
    public void AllIcons_HaveUniqueIds()
    {
        var ids = IconLibrary.Icons.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AllIcons_HaveParseablePathData()
    {
        foreach (var icon in IconLibrary.Icons)
        {
            var geometry = Geometry.Parse(icon.PathData);
            Assert.False(geometry.IsEmpty(), $"Icon '{icon.Id}' parsed to an empty geometry.");
        }
    }

    [Fact]
    public void DefaultIcon_ExistsInLibrary()
    {
        Assert.Contains(IconLibrary.Icons, i => i.Id == "table");
    }

    [Fact]
    public void ColorSaveAction_IsNotASelectableLadaIcon()
    {
        Assert.DoesNotContain(IconLibrary.Icons, icon => icon.Id == "add");
    }

    [Fact]
    public void ColorSynchronizationActionIcons_HaveParseablePathData()
    {
        Assert.False(Geometry.Parse(IconLibrary.ShareColorAction.PathData).IsEmpty());
        Assert.False(Geometry.Parse(IconLibrary.IndependentColorsAction.PathData).IsEmpty());
    }
}
