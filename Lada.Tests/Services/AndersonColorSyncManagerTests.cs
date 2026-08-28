using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class AndersonColorSyncManagerTests
{
    [Fact]
    public void DefaultsToSynchronized()
    {
        var manager = new AndersonColorSyncManager();

        Assert.True(manager.Enabled);
        Assert.Equal("#33FF33", manager.Color);
    }

    [Fact]
    public void ToggleOffPreservesColor_AndToggleOnPublishesSourceColor()
    {
        var manager = new AndersonColorSyncManager();
        string? published = null;
        manager.ColorChanged += color => published = color;

        manager.Toggle("#FF0000");
        Assert.False(manager.Enabled);
        Assert.Equal("#33FF33", manager.Color);
        Assert.Null(published);

        manager.Toggle("#0088FF");
        Assert.True(manager.Enabled);
        Assert.Equal("#0088FF", manager.Color);
        Assert.Equal("#0088FF", published);
    }

    [Fact]
    public void SetColorPublishesSharedColor()
    {
        var manager = new AndersonColorSyncManager();
        string? published = null;
        manager.ColorChanged += color => published = color;

        manager.SetColor("#D91E18");

        Assert.Equal("#D91E18", manager.Color);
        Assert.Equal("#D91E18", published);
    }
}
