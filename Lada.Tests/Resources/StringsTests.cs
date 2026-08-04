using Lada.Models;
using Lada.Resources;
using Xunit;

namespace Lada.Tests.Resources;

// Shares a collection with any other test class that mutates Strings.Language
// (e.g. FileSizeFormatterTests) -- xunit runs different test classes in
// parallel by default, and Strings.Language is process-wide mutable static
// state, so without this two such classes running concurrently can read each
// other's in-progress Language value mid-test.
[Collection("Strings.Language")]
public class StringsTests
{
    [Fact]
    public void Language_SwitchesEveryStringBetweenFrenchAndEnglish()
    {
        var originalLanguage = Strings.Language;
        try
        {
            Strings.Language = AppLanguage.French;
            Assert.Equal("Nouveau lada", Strings.NewLada);

            Strings.Language = AppLanguage.English;
            Assert.Equal("New lada", Strings.NewLada);
        }
        finally
        {
            Strings.Language = originalLanguage;
        }
    }

    [Fact]
    public void RemoveFromLadaCount_IncludesTheCount()
    {
        var originalLanguage = Strings.Language;
        try
        {
            Strings.Language = AppLanguage.English;
            Assert.Equal("Remove from this lada (3)", Strings.RemoveFromLadaCount(3));
        }
        finally
        {
            Strings.Language = originalLanguage;
        }
    }
}
