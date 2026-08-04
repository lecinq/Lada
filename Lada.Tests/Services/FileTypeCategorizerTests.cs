using System.IO;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class FileTypeCategorizerTests
{
    [Theory]
    [InlineData("C:\\a\\report.pdf", FileCategory.Document)]
    [InlineData("C:\\a\\notes.docx", FileCategory.Document)]
    [InlineData("C:\\a\\budget.xlsx", FileCategory.Document)]
    [InlineData("C:\\a\\photo.jpg", FileCategory.Image)]
    [InlineData("C:\\a\\photo.PNG", FileCategory.Image)]
    [InlineData("C:\\a\\clip.mp4", FileCategory.Video)]
    [InlineData("C:\\a\\song.mp3", FileCategory.Audio)]
    [InlineData("C:\\a\\setup.exe", FileCategory.Executable)]
    [InlineData("C:\\a\\shortcut.lnk", FileCategory.Executable)]
    [InlineData("C:\\a\\data.xyz123", FileCategory.Other)]
    [InlineData("C:\\a\\noextension", FileCategory.Other)]
    public void Categorize_MapsExtensionToExpectedCategory(string path, FileCategory expected)
    {
        Assert.Equal(expected, FileTypeCategorizer.Categorize(path));
    }

    [Fact]
    public void Categorize_ReturnsFolder_ForExistingDirectory()
    {
        var realDirectory = Path.GetTempPath();

        Assert.Equal(FileCategory.Folder, FileTypeCategorizer.Categorize(realDirectory));
    }

    [Fact]
    public void CategoryDeclarationOrder_MatchesIntendedSortOrder()
    {
        Assert.True(FileCategory.Folder < FileCategory.Document);
        Assert.True(FileCategory.Document < FileCategory.Image);
        Assert.True(FileCategory.Image < FileCategory.Video);
        Assert.True(FileCategory.Video < FileCategory.Audio);
        Assert.True(FileCategory.Audio < FileCategory.Executable);
        Assert.True(FileCategory.Executable < FileCategory.Other);
    }
}
