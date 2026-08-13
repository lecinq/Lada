using System;
using System.Text.Json;
using Lada.Models;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Models;

public class LadaLayoutCollectionSerializationTests
{
    [Fact]
    public void Serialize_UsesCamelCasePropertyNames()
    {
        var collection = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Title = "My Lada",
                    X = 10,
                    Y = 20,
                    Width = 300,
                    Height = 200,
                    IsFolded = false,
                    Items = { new LadaItem { Path = "C:\\a.lnk", DisplayName = "a", Column = 0, Row = 0 } }
                }
            }
        };

        var json = JsonSerializer.Serialize(collection, LadaJson.Options);

        Assert.Contains("\"ladas\"", json);
        Assert.Contains("\"title\"", json);
        Assert.Contains("\"isFolded\"", json);
        Assert.Contains("\"displayName\"", json);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Title = "My Lada",
                    X = 10, Y = 20, Width = 300, Height = 200, IsFolded = true,
                    Items = { new LadaItem { Path = "C:\\a.lnk", DisplayName = "a", Column = 1, Row = 2 } }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.NotNull(restored);
        var lada = Assert.Single(restored!.Ladas);
        Assert.Equal(original.Ladas[0].Id, lada.Id);
        Assert.Equal("My Lada", lada.Title);
        Assert.Equal(10, lada.X);
        Assert.True(lada.IsFolded);
        var item = Assert.Single(lada.Items);
        Assert.Equal("C:\\a.lnk", item.Path);
        Assert.Equal(1, item.Column);
    }

    [Fact]
    public void RoundTrip_PreservesIconIdAndColor()
    {
        var original = new LadaLayoutCollection
        {
            Ladas = { new LadaLayout { IconId = "star", IconColor = "#F59E0B" } }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var lada = Assert.Single(restored!.Ladas);
        Assert.Equal("star", lada.IconId);
        Assert.Equal("#F59E0B", lada.IconColor);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutIconFields_UsesDefaults()
    {
        const string legacyJson = """
            {
              "ladas": [
                { "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100, "width": 320, "height": 240, "isFolded": false, "items": [] }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var lada = Assert.Single(restored!.Ladas);
        Assert.Equal("table", lada.IconId);
        Assert.Equal("#5B8DEF", lada.IconColor);
    }

    [Fact]
    public void RoundTrip_PreservesAutoSortEnabled()
    {
        var original = new LadaLayoutCollection
        {
            Ladas = { new LadaLayout { AutoSortEnabled = true } }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.True(Assert.Single(restored!.Ladas).AutoSortEnabled);
    }

    [Fact]
    public void RoundTrip_PreservesIsDrawer()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Items = { new LadaItem { Path = "C:\\a", DisplayName = "a", IsDrawer = true } }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.True(item.IsDrawer);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutIsDrawerField_DefaultsToFalse()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100,
                  "width": 320, "height": 240, "isFolded": false, "iconId": "folder", "iconColor": "#5B8DEF",
                  "autoSortEnabled": false,
                  "items": [ { "path": "C:\\a", "displayName": "a", "column": 0, "row": 0 } ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.False(item.IsDrawer);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutAutoSortField_DefaultsToFalse()
    {
        const string legacyJson = """
            {
              "ladas": [
                { "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100, "width": 320, "height": 240, "isFolded": false, "iconId": "folder", "iconColor": "#5B8DEF", "items": [] }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        Assert.False(Assert.Single(restored!.Ladas).AutoSortEnabled);
    }

    [Fact]
    public void RoundTrip_PreservesTabs()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    ActiveTabIndex = 1,
                    Tabs =
                    {
                        new LadaTab { Title = "Général", AutoSortEnabled = true, Items = { new LadaItem { Path = "C:\\a", DisplayName = "a" } } },
                        new LadaTab { Title = "Jeux" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var lada = Assert.Single(restored!.Ladas);
        Assert.Equal(1, lada.ActiveTabIndex);
        Assert.Equal(2, lada.Tabs.Count);
        Assert.Equal("Général", lada.Tabs[0].Title);
        Assert.True(lada.Tabs[0].AutoSortEnabled);
        Assert.Equal("C:\\a", Assert.Single(lada.Tabs[0].Items).Path);
        Assert.Equal("Jeux", lada.Tabs[1].Title);
    }

    [Fact]
    public void RoundTrip_PreservesAutoOrganizeCategoriesAndDesktopAbsorption()
    {
        var activity = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Tabs =
                    {
                        new LadaTab
                        {
                            Title = "Images",
                            AutoOrganizeCategories = { FileCategory.Image, FileCategory.Video },
                            LastActivityUtc = activity,
                            Items =
                            {
                                new LadaItem
                                {
                                    Path = "C:\\Users\\Ken\\Desktop\\photo.jpg",
                                    DisplayName = "photo",
                                    IsDesktopAbsorbed = true,
                                    OriginalDesktopX = 120,
                                    OriginalDesktopY = 340
                                }
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var tab = Assert.Single(Assert.Single(restored!.Ladas).Tabs);
        Assert.Equal(new[] { FileCategory.Image, FileCategory.Video }, tab.AutoOrganizeCategories);
        Assert.Equal(activity, tab.LastActivityUtc);
        var item = Assert.Single(tab.Items);
        Assert.True(item.IsDesktopAbsorbed);
        Assert.Equal(120, item.OriginalDesktopX);
        Assert.Equal(340, item.OriginalDesktopY);
    }

    [Fact]
    public void RoundTrip_PreservesContentModeToDoTasksAndMemoText()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Tabs =
                    {
                        new LadaTab
                        {
                            Title = "Tâches",
                            ContentMode = TabContentMode.ToDoList,
                            ToDoTasks =
                            {
                                new ToDoTaskEntry { Text = "Acheter du lait", IsChecked = false },
                                new ToDoTaskEntry { Text = "Payer le loyer", IsChecked = true }
                            }
                        },
                        new LadaTab
                        {
                            Title = "Notes",
                            ContentMode = TabContentMode.Memo,
                            MemoText = "Idées pour le week-end:\n- Randonnée\n- Cinéma"
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.Contains("\"toDoList\"", json);
        var lada = Assert.Single(restored!.Ladas);
        Assert.Equal(2, lada.Tabs.Count);

        var toDoTab = lada.Tabs[0];
        Assert.Equal(TabContentMode.ToDoList, toDoTab.ContentMode);
        Assert.Equal(2, toDoTab.ToDoTasks.Count);
        Assert.Equal("Acheter du lait", toDoTab.ToDoTasks[0].Text);
        Assert.False(toDoTab.ToDoTasks[0].IsChecked);
        Assert.True(toDoTab.ToDoTasks[1].IsChecked);

        var memoTab = lada.Tabs[1];
        Assert.Equal(TabContentMode.Memo, memoTab.ContentMode);
        Assert.Equal("Idées pour le week-end:\n- Randonnée\n- Cinéma", memoTab.MemoText);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutContentModeField_DefaultsToIcons()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "tabs": [
                    { "title": "Général", "items": [ { "path": "C:\\a", "displayName": "a" } ] }
                  ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var tab = Assert.Single(Assert.Single(restored!.Ladas).Tabs);
        Assert.Equal(TabContentMode.Icons, tab.ContentMode);
        Assert.Empty(tab.ToDoTasks);
        Assert.Equal("", tab.MemoText);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutAutoOrganizeFields_DefaultsToEmptyAndNotAbsorbed()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "tabs": [
                    { "title": "Général", "items": [ { "path": "C:\\a", "displayName": "a" } ] }
                  ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var tab = Assert.Single(Assert.Single(restored!.Ladas).Tabs);
        Assert.Empty(tab.AutoOrganizeCategories);
        var item = Assert.Single(tab.Items);
        Assert.False(item.IsDesktopAbsorbed);
        Assert.Null(item.OriginalDesktopX);
        Assert.Null(item.OriginalDesktopY);
    }

    [Fact]
    public void ResolveTabs_WithExistingTabs_ReturnsThemUnchanged()
    {
        var lada = new LadaLayout
        {
            Items = { new LadaItem { Path = "C:\\legacy", DisplayName = "legacy" } },
            Tabs = { new LadaTab { Title = "Custom" } }
        };

        var resolved = lada.ResolveTabs();

        var tab = Assert.Single(resolved);
        Assert.Equal("Custom", tab.Title);
    }

    [Fact]
    public void ResolveTabs_LegacyLadaWithoutTabs_SynthesizesSingleImplicitTab()
    {
        var lada = new LadaLayout
        {
            AutoSortEnabled = true,
            Items = { new LadaItem { Path = "C:\\legacy", DisplayName = "legacy" } }
        };

        var resolved = lada.ResolveTabs();

        var tab = Assert.Single(resolved);
        Assert.True(tab.AutoSortEnabled);
        Assert.Equal("C:\\legacy", Assert.Single(tab.Items).Path);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutTabsField_ResolveTabsSynthesizesImplicitTab()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100,
                  "width": 320, "height": 240, "isFolded": false, "iconId": "table", "iconColor": "#5B8DEF",
                  "autoSortEnabled": true,
                  "items": [ { "path": "C:\\a", "displayName": "a", "column": 0, "row": 0 } ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);
        var lada = Assert.Single(restored!.Ladas);

        Assert.Empty(lada.Tabs);

        var resolvedTab = Assert.Single(lada.ResolveTabs());
        Assert.True(resolvedTab.AutoSortEnabled);
        Assert.Equal("C:\\a", Assert.Single(resolvedTab.Items).Path);
    }

    [Fact]
    public void RoundTrip_PreservesTheme()
    {
        var original = new LadaLayoutCollection { Theme = AppTheme.Modernism };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.Contains("\"modernism\"", json);
        Assert.Equal(AppTheme.Modernism, restored!.Theme);
    }

    [Fact]
    public void RoundTrip_PreservesTheme_Anderson()
    {
        var original = new LadaLayoutCollection { Theme = AppTheme.Anderson };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.Contains("\"anderson\"", json);
        Assert.Equal(AppTheme.Anderson, restored!.Theme);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutThemeField_DefaultsToMidnight()
    {
        const string legacyJson = """
            {
              "ladas": []
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        Assert.Equal(AppTheme.Midnight, restored!.Theme);
    }

    [Fact]
    public void RoundTrip_PreservesMailContentMode()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Tabs =
                    {
                        new LadaTab { Title = "Mail", ContentMode = TabContentMode.Mail }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.Contains("\"mail\"", json);
        var tab = Assert.Single(Assert.Single(restored!.Ladas).Tabs);
        Assert.Equal(TabContentMode.Mail, tab.ContentMode);
    }

    [Fact]
    public void RoundTrip_PreservesClockWidgetFields()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Items = { new LadaItem { DisplayName = "Paris", IsClockWidget = true, TimeZoneId = "Romance Standard Time" } }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.True(item.IsClockWidget);
        Assert.Equal("Romance Standard Time", item.TimeZoneId);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutClockFields_DefaultsToNotAClockWidget()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100,
                  "width": 320, "height": 240, "isFolded": false, "iconId": "folder", "iconColor": "#5B8DEF",
                  "autoSortEnabled": false,
                  "items": [ { "path": "C:\\a", "displayName": "a", "column": 0, "row": 0 } ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.False(item.IsClockWidget);
        Assert.Null(item.TimeZoneId);
    }

    [Fact]
    public void RoundTrip_PreservesLanguage()
    {
        var original = new LadaLayoutCollection { Language = AppLanguage.English };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.Contains("\"english\"", json);
        Assert.Equal(AppLanguage.English, restored!.Language);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutLanguageField_DefaultsToNull()
    {
        const string legacyJson = """
            {
              "ladas": []
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        Assert.Null(restored!.Language);
    }

    [Fact]
    public void RoundTrip_PreservesDiskWidgetFields()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Items = { new LadaItem { DisplayName = "C:\\", IsDiskWidget = true, DrivePath = "C:\\" } }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.True(item.IsDiskWidget);
        Assert.Equal("C:\\", item.DrivePath);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutDiskFields_DefaultsToNotADiskWidget()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100,
                  "width": 320, "height": 240, "isFolded": false, "iconId": "folder", "iconColor": "#5B8DEF",
                  "autoSortEnabled": false,
                  "items": [ { "path": "C:\\a", "displayName": "a", "column": 0, "row": 0 } ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.False(item.IsDiskWidget);
        Assert.Null(item.DrivePath);
    }

    [Fact]
    public void RoundTrip_PreservesTimerWidgetFields()
    {
        var original = new LadaLayoutCollection
        {
            Ladas =
            {
                new LadaLayout
                {
                    Items =
                    {
                        new LadaItem
                        {
                            DisplayName = "Minuteur",
                            IsTimerWidget = true,
                            TimerDurationSeconds = 300,
                            TimerRemainingSeconds = 120.5,
                            TimerEndUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.True(item.IsTimerWidget);
        Assert.Equal(300, item.TimerDurationSeconds);
        Assert.Equal(120.5, item.TimerRemainingSeconds);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), item.TimerEndUtc);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutTimerFields_DefaultsToNotATimerWidget()
    {
        const string legacyJson = """
            {
              "ladas": [
                {
                  "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100,
                  "width": 320, "height": 240, "isFolded": false, "iconId": "folder", "iconColor": "#5B8DEF",
                  "autoSortEnabled": false,
                  "items": [ { "path": "C:\\a", "displayName": "a", "column": 0, "row": 0 } ]
                }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        var item = Assert.Single(Assert.Single(restored!.Ladas).Items);
        Assert.False(item.IsTimerWidget);
        Assert.Null(item.TimerEndUtc);
    }

    [Fact]
    public void RoundTrip_PreservesIsWidget()
    {
        var original = new LadaLayoutCollection
        {
            Ladas = { new LadaLayout { IsWidget = true } }
        };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.True(Assert.Single(restored!.Ladas).IsWidget);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutIsWidgetField_DefaultsToFalse()
    {
        const string legacyJson = """
            {
              "ladas": [
                { "id": "8a8e051e-6ea5-4dea-9762-5a381d11d41c", "title": "Lada", "x": 100, "y": 100, "width": 320, "height": 240, "isFolded": false, "items": [] }
              ]
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        Assert.False(Assert.Single(restored!.Ladas).IsWidget);
    }

    [Fact]
    public void RoundTrip_PreservesWidgetChromeVisible()
    {
        var original = new LadaLayoutCollection { WidgetChromeVisible = false };

        var json = JsonSerializer.Serialize(original, LadaJson.Options);
        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(json, LadaJson.Options);

        Assert.False(restored!.WidgetChromeVisible);
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutWidgetChromeVisibleField_DefaultsToTrue()
    {
        const string legacyJson = """
            {
              "ladas": []
            }
            """;

        var restored = JsonSerializer.Deserialize<LadaLayoutCollection>(legacyJson, LadaJson.Options);

        Assert.True(restored!.WidgetChromeVisible);
    }
}
