using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    // Grid-mode rendering only. Always safe to call regardless of the
    // active tab's actual ViewMode -- same "acts on a hidden surface"
    // principle RefreshDynamicContent already relies on for IconGrid vs
    // ToDoListPanel/MemoTextBox. List-mode rendering is a separate call
    // (RenderListPanel, triggered from UpdateTabContentModeVisuals in
    // LadaWindow.Tabs.cs) so the two never race or double-render.
    private void RenderAllItems()
    {
        IconGrid.Children.Clear();
        foreach (var item in _items)
        {
            RenderItem(item);
        }
    }

    // Used by every call site that previously appended a single newly
    // created item via a bare RenderItem(item) call (AddItem, AbsorbDesktopItem,
    // AddClockWidget, AddDiskWidget). The item must already be present in
    // _items/the target tab's Items by the time this runs.
    private void RenderSingleItem(LadaItem item)
    {
        // Always keep IconGrid in sync, even while it's hidden behind list
        // view -- otherwise switching back to grid view after adding items
        // while in list mode would show a stale grid missing them, since
        // nothing else re-renders IconGrid on that transition.
        RenderItem(item);

        if (_tabs[_activeTabIndex].ViewMode == ItemViewMode.List)
        {
            RenderListPanel();
        }
    }

    private void RenderListPanel()
    {
        var tab = _tabs[_activeTabIndex];

        // Self-contained: every call site can just call this and get a
        // correct rebuild, without needing to remember to dispose drawer
        // watchers first (this fully rebuilds every row, including any
        // expanded drawer's children panel, so any watcher targeting the
        // previous panel instance must be stopped before re-registering).
        DisposeAllDrawerWatchers();

        ItemListPanel.Children.Clear();
        ItemListPanel.Children.Add(BuildListHeaderRow(tab));

        foreach (var item in _items)
        {
            ItemListPanel.Children.Add(BuildListRow(item));

            if (item.IsDrawer)
            {
                ItemListPanel.Children.Add(BuildDrawerChildrenPanel(item));
            }
        }
    }

    private Grid BuildListHeaderRow(LadaTab tab)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        AddListColumns(grid, tab);

        var columnIndex = 1; // column 0 is the icon; no header label there
        grid.Children.Add(HeaderLabel(Strings.ColumnName, columnIndex, leadingMargin: false));
        columnIndex++;

        if (tab.ShowTypeColumn)
        {
            grid.Children.Add(HeaderLabel(Strings.ColumnType, columnIndex, leadingMargin: true));
            columnIndex++;
        }
        if (tab.ShowSizeColumn)
        {
            grid.Children.Add(HeaderLabel(Strings.ColumnSize, columnIndex, leadingMargin: true));
            columnIndex++;
        }
        if (tab.ShowModifiedDateColumn)
        {
            grid.Children.Add(HeaderLabel(Strings.ColumnModifiedDate, columnIndex, leadingMargin: true));
        }

        return grid;
    }

    private TextBlock HeaderLabel(string text, int column, bool leadingMargin)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            Margin = leadingMargin ? new Thickness(8, 0, 0, 0) : new Thickness(0)
        };
        Grid.SetColumn(label, column);
        return label;
    }

    // Shared by the header row and every data row so their columns line up
    // (Grid.IsSharedSizeScope="True" on ItemListPanel, set in LadaWindow.xaml,
    // makes every Auto column sharing a SharedSizeGroup name take the max
    // width across all sibling rows -- fact-checked live this session with a
    // throwaway WPF app: building rows independently, clearing, and
    // rebuilding with different content all re-align correctly).
    private void AddListColumns(Grid grid, LadaTab tab)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (tab.ShowTypeColumn)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ListTypeCol" });
        if (tab.ShowSizeColumn)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ListSizeCol" });
        if (tab.ShowModifiedDateColumn)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ListDateCol" });
    }

    private Grid BuildListRow(LadaItem item)
    {
        var tab = _tabs[_activeTabIndex];
        var isWidget = item.IsClockWidget || item.IsDiskWidget || item.IsTimerWidget || item.IsBatteryWidget || item.IsMemoryWidget || item.IsCpuWidget || item.IsGpuWidget || item.IsNetworkWidget;

        var grid = new Grid { Tag = item, Margin = new Thickness(0, 2, 0, 2), Cursor = Cursors.Hand };
        AddListColumns(grid, tab);

        if (!isWidget)
        {
            var icon = FileTypeCategorizer.Categorize(item.Path) == FileCategory.Image
                ? ImageThumbnailService.GetThumbnail(item.Path) ?? ShellIconService.GetIcon(item.Path)
                : ShellIconService.GetIcon(item.Path);
            if (icon is BitmapSource bitmap)
            {
                var image = new Image { Source = bitmap, Width = 20, Height = 20, Margin = new Thickness(0, 0, 6, 0) };
                Grid.SetColumn(image, 0);
                grid.Children.Add(image);
            }
        }

        var name = new TextBlock
        {
            Text = item.DisplayName,
            FontFamily = (FontFamily)FindResource("LadaFontFamily"),
            FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (ItemLabelAccentOverride() is { } labelBrush)
        {
            name.Foreground = labelBrush;
        }
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var columnIndex = 2;
        if (tab.ShowTypeColumn)
        {
            grid.Children.Add(ListCell(GetListTypeLabel(item), columnIndex, rightAlign: false));
            columnIndex++;
        }
        if (tab.ShowSizeColumn)
        {
            grid.Children.Add(ListCell(GetListSizeLabel(item), columnIndex, rightAlign: true));
            columnIndex++;
        }
        if (tab.ShowModifiedDateColumn)
        {
            grid.Children.Add(ListCell(GetListDateLabel(item), columnIndex, rightAlign: false));
        }

        if (!isWidget)
        {
            grid.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2)
                    LaunchItem(item);
            };
        }

        AttachItemDragSource(grid, grid, item);

        // Widgets have no real Path to categorize and use their own
        // dedicated grid-mode context menus (BuildClockWidgetContextMenu /
        // BuildDiskWidgetContextMenu) that assume a live-updating label --
        // reproducing that in a table row is out of scope for v1 (see
        // README Known Limitations), so a list-mode widget row only offers
        // the generic "Retirer de ce lada" here. Switch back to grid view to
        // change a clock's timezone or a disk widget's drive.
        grid.ContextMenu = !isWidget && FileTypeCategorizer.Categorize(item.Path) == FileCategory.Folder
            ? BuildFolderItemContextMenu(item, grid)
            : BuildDefaultItemContextMenu(item, grid);

        return grid;
    }

    private TextBlock ListCell(string text, int column, bool rightAlign)
    {
        var cell = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = rightAlign ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(cell, column);
        return cell;
    }

    private string GetListTypeLabel(LadaItem item)
    {
        if (item.IsClockWidget)
            return Strings.ClockWidgetMenuItem;
        if (item.IsDiskWidget)
            return Strings.DiskWidgetMenuItem;
        if (item.IsTimerWidget)
            return Strings.TimerWidgetMenuItem;
        if (item.IsBatteryWidget)
            return Strings.BatteryWidgetMenuItem;
        if (item.IsMemoryWidget)
            return Strings.MemoryWidgetMenuItem;
        if (item.IsCpuWidget)
            return Strings.CpuWidgetMenuItem;
        if (item.IsGpuWidget)
            return Strings.GpuWidgetMenuItem;
        if (item.IsNetworkWidget)
            return Strings.NetworkWidgetMenuItem;

        // A drawer (IsDrawer) still has a real folder Path, so it goes
        // through the normal lookup too -- SHGetFileInfo already returns
        // "Dossier de fichiers" for it, no special-casing needed.
        return ShellIconService.GetTypeName(item.Path) ?? "";
    }

    private string GetListSizeLabel(LadaItem item)
    {
        if (item.IsClockWidget || item.IsDiskWidget || item.IsTimerWidget || item.IsBatteryWidget || item.IsMemoryWidget || item.IsCpuWidget || item.IsGpuWidget || item.IsNetworkWidget || !File.Exists(item.Path))
            return ""; // File.Exists is false for directories too, so a
                        // drawer/folder row's size is blank without needing
                        // a try/catch around FileInfo.Length.

        return FileSizeFormatter.FormatBytes(new FileInfo(item.Path).Length);
    }

    private string GetListDateLabel(LadaItem item)
    {
        if (item.IsClockWidget || item.IsDiskWidget || item.IsTimerWidget || item.IsBatteryWidget || item.IsMemoryWidget || item.IsCpuWidget || item.IsGpuWidget || item.IsNetworkWidget)
            return "";
        if (!File.Exists(item.Path) && !Directory.Exists(item.Path))
            return "";

        return File.GetLastWriteTime(item.Path).ToShortDateString();
    }
}
