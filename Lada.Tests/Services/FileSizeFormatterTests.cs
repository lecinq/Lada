using Lada.Models;
using Lada.Resources;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

// See StringsTests -- shares the same collection since both mutate the
// process-wide Strings.Language static.
[Collection("Strings.Language")]
public class FileSizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 o")]
    [InlineData(512, "512 o")]
    [InlineData(1023, "1023 o")]
    [InlineData(1024, "1 Ko")]
    [InlineData(1536, "1,5 Ko")]
    [InlineData(1024 * 1024, "1 Mo")]
    [InlineData(4_404_019, "4,2 Mo")] // 4.2 MB
    [InlineData(1024L * 1024 * 1024, "1 Go")]
    [InlineData(3_221_225_472, "3 Go")] // exactly 3 GB
    public void FormatBytes_French_UsesCorrectUnitAndRounding(long bytes, string expected)
    {
        var originalLanguage = Strings.Language;
        try
        {
            Strings.Language = AppLanguage.French;
            Assert.Equal(expected, FileSizeFormatter.FormatBytes(bytes));
        }
        finally
        {
            Strings.Language = originalLanguage;
        }
    }

    [Fact]
    public void FormatBytes_English_UsesEnglishUnit()
    {
        var originalLanguage = Strings.Language;
        try
        {
            Strings.Language = AppLanguage.English;
            Assert.Equal("1 MB", FileSizeFormatter.FormatBytes(1024 * 1024));
        }
        finally
        {
            Strings.Language = originalLanguage;
        }
    }
}
