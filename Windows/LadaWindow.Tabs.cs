using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Models;
using Lada.Resources;

namespace Lada.Windows;

public partial class LadaWindow
{
    private void RenderTabStrip()
    {
        TabStripPanel.Children.Clear();

        // The first tab is implicit and stays invisible until a second one
        // exists, so a lada that never uses tabs looks exactly as before,
        // just the '+' button next to the title.
        if (_tabs.Count > 1)
        {
            for (var i = 0; i < _tabs.Count; i++)
            {
                TabStripPanel.Children.Add(BuildTabHeader(_tabs[i], i));
            }
        }

        var addButton = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            Cursor = Cursors.Hand
        };
        addButton.Child = new TextBlock
        {
            Text = "+",
            Foreground = TabAddButtonBrush(),
            FontSize = 13
        };
        addButton.MouseLeftButtonDown += (_, e) =>
        {
            AddTab();
            e.Handled = true;
        };
        TabStripPanel.Children.Add(addButton);
    }

    // The '+' button is built in code (not a fixed XAML element), so it
    // can't pick up a per-lada accent via a named-element override the way
    // MainBorder/TitleTextBlock do in ApplyThemeColors (LadaWindow.Theme.cs)
    // -- it has to compute its own brush here instead, each time it's
    // rebuilt. In Anderson/Howard it follows the lada's own color like the
    // rest of the chrome; in Midnight/Modernism/Forecast it keeps the fixed muted color it
    // always had.
    private Brush TabAddButtonBrush() =>
        _themeManager?.Current is AppTheme.Anderson or AppTheme.Howard
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!)
            : (Brush)FindResource("SecondaryTextBrush");

    private Border BuildTabHeader(LadaTab tab, int index)
    {
        var isActive = index == _activeTabIndex;
        var accent = ItemLabelAccentOverride();
        var isAnderson = _themeManager?.Current == AppTheme.Anderson;
        var andersonColor = isAnderson
            ? (Color)ColorConverter.ConvertFromString(_iconColor)!
            : default;
        var andersonAccent = isAnderson
            ? new SolidColorBrush(andersonColor)
            : null;

        var border = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 4, 0),
            CornerRadius = isAnderson ? new CornerRadius(0) : new CornerRadius(4),
            Background = isActive
                ? isAnderson ? andersonAccent : WidgetTrackBrush(accent)
                : Brushes.Transparent,
            Cursor = Cursors.Hand,
            AllowDrop = true
        };

        var text = new TextBlock
        {
            Text = tab.Title,
            FontSize = 11,
            Foreground = isAnderson
                ? isActive ? ColorContrast.ForegroundBrush(andersonColor) : andersonAccent
                : isActive ? accent ?? (Brush)FindResource("TitleTextBrush") : WidgetDimmedAccentBrush(accent),
            VerticalAlignment = VerticalAlignment.Center
        };

        var textBox = new TextBox
        {
            Text = tab.Title,
            Visibility = Visibility.Collapsed,
            MinWidth = 50,
            FontSize = 11,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = isAnderson && isActive
                ? ColorContrast.ForegroundBrush(andersonColor)
                : isAnderson ? andersonAccent : (Brush)FindResource("TitleTextBrush")
        };

        void Commit()
        {
            tab.Title = string.IsNullOrWhiteSpace(textBox.Text) ? tab.Title : textBox.Text.Trim();
            text.Text = tab.Title;
            textBox.Visibility = Visibility.Collapsed;
            text.Visibility = Visibility.Visible;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        textBox.LostFocus += (_, _) => Commit();
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                textBox.Text = tab.Title;
                textBox.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        };

        var content = new Grid();
        content.Children.Add(text);
        content.Children.Add(textBox);
        border.Child = content;

        void BeginRename()
        {
            ActivateForTitleEdit();
            text.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
            textBox.Focus();
            textBox.SelectAll();
        }

        border.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                BeginRename();
                e.Handled = true;
                return;
            }

            SwitchTab(index);
            e.Handled = true;
        };

        border.ContextMenu = BuildTabContextMenu(index, BeginRename);

        border.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(typeof(List<LadaItem>)) && index != _activeTabIndex
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        };
        border.Drop += (_, e) => HandleDropOntoTab(index, e);

        return border;
    }

    private ContextMenu BuildTabContextMenu(int index, Action beginRename)
    {
        var menu = new ContextMenu();

        var renameItem = new MenuItem { Header = Strings.RenameTab };
        renameItem.Click += (_, _) => beginRename();
        menu.Items.Add(renameItem);

        menu.Items.Add(new Separator());

        var tab = _tabs[index];
        if (tab.ContentMode != TabContentMode.ToDoList)
        {
            var toToDoItem = new MenuItem { Header = Strings.ConvertTabToToDoList };
            toToDoItem.Click += (_, _) => TryConvertTabContentMode(index, TabContentMode.ToDoList);
            menu.Items.Add(toToDoItem);
        }
        if (tab.ContentMode != TabContentMode.Memo)
        {
            var toMemoItem = new MenuItem { Header = Strings.ConvertTabToMemo };
            toMemoItem.Click += (_, _) => TryConvertTabContentMode(index, TabContentMode.Memo);
            menu.Items.Add(toMemoItem);
        }
        if (tab.ContentMode != TabContentMode.Mail)
        {
            var toMailItem = new MenuItem { Header = Strings.ConvertTabToMail };
            toMailItem.Click += (_, _) => TryConvertTabContentMode(index, TabContentMode.Mail);
            menu.Items.Add(toMailItem);
        }
        if (tab.ContentMode != TabContentMode.Icons)
        {
            var toIconsItem = new MenuItem { Header = Strings.ConvertTabToIcons };
            toIconsItem.Click += (_, _) => TryConvertTabContentMode(index, TabContentMode.Icons);
            menu.Items.Add(toIconsItem);
        }

        menu.Items.Add(new Separator());

        var deleteItem = new MenuItem { Header = Strings.DeleteTabMenuItem, IsEnabled = _tabs.Count > 1 };
        deleteItem.Click += (_, _) => ConfirmAndDeleteTab(index);
        menu.Items.Add(deleteItem);

        return menu;
    }

    private void AddTab()
    {
        _tabs.Add(new LadaTab { Title = Strings.DefaultTabTitle(_tabs.Count + 1) });
        ActivateTab(_tabs.Count - 1);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SwitchTab(int index)
    {
        if (index == _activeTabIndex || index < 0 || index >= _tabs.Count)
            return;

        ActivateTab(index);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ActivateTab(int index, bool persistOutgoing = true)
    {
        if (persistOutgoing)
        {
            _tabs[_activeTabIndex].AutoSortEnabled = _autoSortEnabled;
        }

        DisposeAllDrawerWatchers();
        DisposeAllClockTimers();
        DisposeAllDiskTimers();
        DisposeAllBatteryTimers();
        DisposeAllMemoryTimers();
        DisposeAllCpuUpdates();
        DisposeAllGpuUpdates();
        DisposeAllNetworkUpdates();
        DisposeAllTimerWidgetTimers();
        ClearSelection();
        IconGrid.Children.Clear();

        _activeTabIndex = index;
        _items = _tabs[index].Items;
        _autoSortEnabled = _tabs[index].AutoSortEnabled;
        if (_autoSortMenuItem is not null)
        {
            _autoSortMenuItem.IsChecked = _autoSortEnabled;
        }
        RefreshAutoOrganizeMenuChecks();
        RefreshViewModeMenuChecks();

        RenderAllItems();

        UpdateTabContentModeVisuals();
        RenderTabStrip();
        EnsureContentFits();
    }

    // Toggles which content surface is visible for the active tab's mode.
    private void UpdateTabContentModeVisuals()
    {
        var tab = _tabs[_activeTabIndex];
        var mode = tab.ContentMode;
        var isGridView = mode == TabContentMode.Icons && tab.ViewMode == ItemViewMode.Grid;
        var isListView = mode == TabContentMode.Icons && tab.ViewMode == ItemViewMode.List;

        IconGrid.Visibility = isGridView ? Visibility.Visible : Visibility.Collapsed;
        SelectionOverlay.Visibility = mode == TabContentMode.Icons ? Visibility.Visible : Visibility.Collapsed;
        ItemListPanel.Visibility = isListView ? Visibility.Visible : Visibility.Collapsed;
        ToDoListPanel.Visibility = mode == TabContentMode.ToDoList ? Visibility.Visible : Visibility.Collapsed;
        MemoTextBox.Visibility = mode == TabContentMode.Memo ? Visibility.Visible : Visibility.Collapsed;
        MailPanel.Visibility = mode == TabContentMode.Mail ? Visibility.Visible : Visibility.Collapsed;

        if (isListView)
        {
            RenderListPanel();
        }
        else if (mode == TabContentMode.ToDoList)
        {
            RenderToDoList();
        }
        else if (mode == TabContentMode.Memo)
        {
            RenderMemo();
        }
        else if (mode == TabContentMode.Mail)
        {
            RenderMail();
        }
    }

    // Applies to every direction uniformly (Icons->ToDoList, ToDoList->Memo,
    // Memo->Icons, etc.): a tab can only change mode if it's currently empty
    // in its OWN mode, so content is never silently hidden or lost.
    private void TryConvertTabContentMode(int index, TabContentMode targetMode)
    {
        var tab = _tabs[index];

        var hasCurrentContent = tab.ContentMode switch
        {
            TabContentMode.ToDoList => tab.ToDoTasks.Count > 0,
            TabContentMode.Memo => !string.IsNullOrWhiteSpace(tab.MemoText),
            _ => tab.Items.Count > 0
        };

        if (hasCurrentContent)
        {
            MessageBox.Show(this, Strings.ConvertTabBlockedBody, Strings.ConvertTabBlockedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        tab.ContentMode = targetMode;

        if (index == _activeTabIndex)
        {
            UpdateTabContentModeVisuals();
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ConfirmAndDeleteTab(int index)
    {
        if (_tabs.Count <= 1)
            return;

        var tabToRemove = _tabs[index];

        if (tabToRemove.Items.Count > 0)
        {
            var result = MessageBox.Show(
                this,
                Strings.DeleteTabConfirmationBody(tabToRemove.Title, tabToRemove.Items.Count),
                Strings.DeleteTabMenuItem,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;
        }

        var wasActiveTabRemoved = index == _activeTabIndex;
        var activeTab = _tabs[_activeTabIndex];

        foreach (var item in tabToRemove.Items)
        {
            RestoreDesktopIconIfAbsorbed(item);
        }

        _tabs.RemoveAt(index);

        if (wasActiveTabRemoved)
        {
            ActivateTab(Math.Clamp(index - 1, 0, _tabs.Count - 1), persistOutgoing: false);
        }
        else
        {
            _activeTabIndex = _tabs.IndexOf(activeTab);
            RenderTabStrip();
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleDropOntoTab(int targetIndex, DragEventArgs e)
    {
        e.Handled = true;

        if (!e.Data.GetDataPresent(typeof(List<LadaItem>)) || targetIndex == _activeTabIndex)
            return;

        var draggedItems = (List<LadaItem>)e.Data.GetData(typeof(List<LadaItem>))!;
        MoveItemsToTab(draggedItems, targetIndex);
    }

    private MenuItem? BuildMoveToTabSubmenu(LadaItem item)
    {
        if (_tabs.Count <= 1)
            return null;

        var submenu = new MenuItem { Header = Strings.MoveToSubmenu };

        for (var i = 0; i < _tabs.Count; i++)
        {
            if (i == _activeTabIndex)
                continue;

            var targetIndex = i;
            var tabItem = new MenuItem { Header = _tabs[i].Title };
            tabItem.Click += (_, _) =>
            {
                var itemsToMove = _selectedItems.Contains(item) && _selectedItems.Count > 1
                    ? _selectedItems.ToList()
                    : new List<LadaItem> { item };
                MoveItemsToTab(itemsToMove, targetIndex);
            };
            submenu.Items.Add(tabItem);
        }

        return submenu;
    }

    private void MoveItemsToTab(List<LadaItem> items, int targetIndex)
    {
        foreach (var item in items)
        {
            StopWatchingDrawer(item);
            StopClockTimer(item);
            StopDiskTimer(item);
            StopTimerTicking(item);
            _items.Remove(item);
            _selectedItems.Remove(item);
            _tabs[targetIndex].Items.Add(item);
        }

        _tabs[_activeTabIndex].LastActivityUtc = DateTime.UtcNow;
        _tabs[targetIndex].LastActivityUtc = DateTime.UtcNow;
        _selectionAnchor = null;
        ReflowGrid();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }
}
