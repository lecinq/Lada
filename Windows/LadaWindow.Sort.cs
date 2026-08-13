using System;
using System.Linq;
using System.Windows.Controls;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private bool _autoSortEnabled;
    private MenuItem? _autoSortMenuItem;
    private MenuItem? _viewModeGridItem;
    private MenuItem? _viewModeListItem;
    private MenuItem? _showTypeColumnItem;
    private MenuItem? _showSizeColumnItem;
    private MenuItem? _showModifiedDateColumnItem;

    private void InitializeSortMenu()
    {
        var contextMenu = new ContextMenu();

        var sortNowItem = new MenuItem { Header = Strings.SortByType };
        sortNowItem.Click += (_, _) =>
        {
            SortItemsByType();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        };
        contextMenu.Items.Add(sortNowItem);

        _autoSortMenuItem = new MenuItem
        {
            Header = Strings.AutoSortToggle,
            IsCheckable = true,
            IsChecked = _autoSortEnabled
        };
        _autoSortMenuItem.Click += (_, _) =>
        {
            _autoSortEnabled = _autoSortMenuItem.IsChecked;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        };
        contextMenu.Items.Add(_autoSortMenuItem);

        var sizeSubmenu = new MenuItem { Header = Strings.SizePresetSubmenu };
        foreach (var (columns, rows) in SizePresets)
        {
            var presetItem = new MenuItem { Header = $"{columns} x {rows}" };
            presetItem.Click += (_, _) => ApplyGridSizePreset(columns, rows);
            sizeSubmenu.Items.Add(presetItem);
        }
        sizeSubmenu.Items.Add(new Separator());
        var fitToContentItem = new MenuItem { Header = Strings.FitToContentMenuItem };
        fitToContentItem.Click += (_, _) => FitWindowToContent();
        sizeSubmenu.Items.Add(fitToContentItem);
        contextMenu.Items.Add(sizeSubmenu);

        InitializeViewModeMenu(contextMenu);
        InitializeAutoOrganizeMenu(contextMenu);

        contextMenu.Items.Add(new Separator());

        var newWidgetSubmenu = new MenuItem { Header = Strings.NewComponentSubmenu };
        var newClockItem = new MenuItem { Header = Strings.ClockWidgetMenuItem };
        newClockItem.Click += (_, _) => AddClockWidget();
        newWidgetSubmenu.Items.Add(newClockItem);

        var newDiskSubmenu = new MenuItem { Header = Strings.DiskWidgetMenuItem };
        foreach (var driveItem in BuildDriveMenuItems(AddDiskWidget))
        {
            newDiskSubmenu.Items.Add(driveItem);
        }
        newWidgetSubmenu.Items.Add(newDiskSubmenu);

        var newTimerItem = new MenuItem { Header = Strings.TimerWidgetMenuItem };
        newTimerItem.Click += (_, _) => AddTimerWidget();
        newWidgetSubmenu.Items.Add(newTimerItem);

        var newBatteryItem = new MenuItem { Header = Strings.BatteryWidgetMenuItem };
        newBatteryItem.Click += (_, _) => AddBatteryWidget();
        newWidgetSubmenu.Items.Add(newBatteryItem);

        var newMemoryItem = new MenuItem { Header = Strings.MemoryWidgetMenuItem };
        newMemoryItem.Click += (_, _) => AddMemoryWidget();
        newWidgetSubmenu.Items.Add(newMemoryItem);

        var newCpuItem = new MenuItem { Header = Strings.CpuWidgetMenuItem };
        newCpuItem.Click += (_, _) => AddCpuWidget();
        newWidgetSubmenu.Items.Add(newCpuItem);

        var newGpuSubmenu = new MenuItem { Header = Strings.GpuWidgetMenuItem };
        foreach (var gpuItem in BuildGpuMenuItems(AddGpuWidget))
        {
            newGpuSubmenu.Items.Add(gpuItem);
        }
        newWidgetSubmenu.Items.Add(newGpuSubmenu);

        var newNetworkSubmenu = new MenuItem { Header = Strings.NetworkWidgetMenuItem };
        foreach (var adapterItem in BuildNetworkAdapterMenuItems(AddNetworkWidget))
        {
            newNetworkSubmenu.Items.Add(adapterItem);
        }
        newWidgetSubmenu.Items.Add(newNetworkSubmenu);

        contextMenu.Items.Add(newWidgetSubmenu);

        // Distinct from "Nouveau composant" above: this creates a
        // standalone widget window (see the standalone-widgets spec),
        // reachable from any normal lada's own menu, not just the tray's
        // copy of the same submenu.
        var newStandaloneWidgetSubmenu = new MenuItem { Header = Strings.NewWidgetTrayMenuItem };
        foreach (WidgetComponentType type in Enum.GetValues<WidgetComponentType>())
        {
            var widgetTypeItem = new MenuItem { Header = Strings.WidgetComponentLabel(type) };
            widgetTypeItem.Click += (_, _) => NewWidgetRequested?.Invoke(type);
            newStandaloneWidgetSubmenu.Items.Add(widgetTypeItem);
        }
        contextMenu.Items.Add(newStandaloneWidgetSubmenu);

        contextMenu.Items.Add(new Separator());

        // Only reachable while the active tab is in Icons mode (this is
        // IconGrid's own empty-space menu, and IconGrid is only visible in
        // that mode), so no "back to icons" entry belongs here -- unlike
        // BuildTabContextMenu (LadaWindow.Tabs.cs), this menu is built once
        // at startup rather than rebuilt per open, so _activeTabIndex is
        // captured live in the closure rather than baked in at build time.
        var toToDoItem = new MenuItem { Header = Strings.ConvertTabToToDoList };
        toToDoItem.Click += (_, _) => TryConvertTabContentMode(_activeTabIndex, TabContentMode.ToDoList);
        contextMenu.Items.Add(toToDoItem);

        var toMemoItem = new MenuItem { Header = Strings.ConvertTabToMemo };
        toMemoItem.Click += (_, _) => TryConvertTabContentMode(_activeTabIndex, TabContentMode.Memo);
        contextMenu.Items.Add(toMemoItem);

        contextMenu.Items.Add(new Separator());

        var newLadaItem = new MenuItem { Header = Strings.NewLada };
        newLadaItem.Click += (_, _) => NewLadaRequested?.Invoke();
        contextMenu.Items.Add(newLadaItem);

        contextMenu.Items.Add(new Separator());

        var deleteLadaItem = new MenuItem { Header = Strings.DeleteLadaMenuItem };
        deleteLadaItem.Click += (_, _) => ConfirmAndRequestDelete();
        contextMenu.Items.Add(deleteLadaItem);

        AttachOutsideClickAutoClose(contextMenu);
        IconGrid.ContextMenu = contextMenu;
        // Also attached to ItemListPanel, not just IconGrid: this is the
        // only menu that can switch ViewMode back to Grid, and IconGrid is
        // Collapsed while list view is active -- without this, list view
        // would have no way back to grid view via the menu.
        ItemListPanel.ContextMenu = contextMenu;
    }

    private void InitializeViewModeMenu(ContextMenu contextMenu)
    {
        var viewSubmenu = new MenuItem { Header = Strings.ViewModeSubmenu };

        _viewModeGridItem = new MenuItem { Header = Strings.ViewModeGrid, IsCheckable = true, IsChecked = _tabs[_activeTabIndex].ViewMode == ItemViewMode.Grid };
        _viewModeGridItem.Click += (_, _) => SetViewMode(ItemViewMode.Grid);
        viewSubmenu.Items.Add(_viewModeGridItem);

        _viewModeListItem = new MenuItem { Header = Strings.ViewModeList, IsCheckable = true, IsChecked = _tabs[_activeTabIndex].ViewMode == ItemViewMode.List };
        _viewModeListItem.Click += (_, _) => SetViewMode(ItemViewMode.List);
        viewSubmenu.Items.Add(_viewModeListItem);

        contextMenu.Items.Add(viewSubmenu);

        var columnsSubmenu = new MenuItem { Header = Strings.ColumnsSubmenu };

        _showTypeColumnItem = new MenuItem { Header = Strings.ColumnType, IsCheckable = true, IsChecked = _tabs[_activeTabIndex].ShowTypeColumn };
        _showTypeColumnItem.Click += (_, _) => ToggleColumn(tab => tab.ShowTypeColumn = _showTypeColumnItem!.IsChecked);
        columnsSubmenu.Items.Add(_showTypeColumnItem);

        _showSizeColumnItem = new MenuItem { Header = Strings.ColumnSize, IsCheckable = true, IsChecked = _tabs[_activeTabIndex].ShowSizeColumn };
        _showSizeColumnItem.Click += (_, _) => ToggleColumn(tab => tab.ShowSizeColumn = _showSizeColumnItem!.IsChecked);
        columnsSubmenu.Items.Add(_showSizeColumnItem);

        _showModifiedDateColumnItem = new MenuItem { Header = Strings.ColumnModifiedDate, IsCheckable = true, IsChecked = _tabs[_activeTabIndex].ShowModifiedDateColumn };
        _showModifiedDateColumnItem.Click += (_, _) => ToggleColumn(tab => tab.ShowModifiedDateColumn = _showModifiedDateColumnItem!.IsChecked);
        columnsSubmenu.Items.Add(_showModifiedDateColumnItem);

        contextMenu.Items.Add(columnsSubmenu);
    }

    private void SetViewMode(ItemViewMode mode)
    {
        _tabs[_activeTabIndex].ViewMode = mode;
        _viewModeGridItem!.IsChecked = mode == ItemViewMode.Grid;
        _viewModeListItem!.IsChecked = mode == ItemViewMode.List;
        UpdateTabContentModeVisuals();
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleColumn(Action<LadaTab> applyToActiveTab)
    {
        applyToActiveTab(_tabs[_activeTabIndex]);

        if (_tabs[_activeTabIndex].ViewMode == ItemViewMode.List)
        {
            RenderListPanel();
            EnsureContentFits();
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    // Refreshes the 5 checkmarks above to reflect whichever tab just became
    // active -- same principle as RefreshAutoOrganizeMenuChecks.
    private void RefreshViewModeMenuChecks()
    {
        if (_viewModeGridItem is null)
            return;

        var tab = _tabs[_activeTabIndex];
        _viewModeGridItem.IsChecked = tab.ViewMode == ItemViewMode.Grid;
        _viewModeListItem!.IsChecked = tab.ViewMode == ItemViewMode.List;
        _showTypeColumnItem!.IsChecked = tab.ShowTypeColumn;
        _showSizeColumnItem!.IsChecked = tab.ShowSizeColumn;
        _showModifiedDateColumnItem!.IsChecked = tab.ShowModifiedDateColumn;
    }

    private void SortItemsByType()
    {
        var sorted = _items
            .OrderBy(item => FileTypeCategorizer.Categorize(item.Path))
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _items.Clear();
        _items.AddRange(sorted);

        ReflowGrid();
    }

    private void ReflowGrid()
    {
        DisposeAllDrawerWatchers();
        DisposeAllClockTimers();
        DisposeAllDiskTimers();
        DisposeAllBatteryTimers();
        DisposeAllMemoryTimers();
        DisposeAllCpuUpdates();
        DisposeAllGpuUpdates();
        DisposeAllNetworkUpdates();
        DisposeAllTimerWidgetTimers();

        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Column = i % ColumnsPerRow;
            _items[i].Row = i / ColumnsPerRow;
        }

        RenderAllItems();
        UpdateTabContentModeVisuals();

        EnsureContentFits();
    }
}
