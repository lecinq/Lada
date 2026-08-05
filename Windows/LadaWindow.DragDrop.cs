using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lada.Models;
using Lada.Native;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private const int ColumnsPerRow = 4;

    private Point _itemDragStart;
    private bool _isDraggingItem;
    private Panel? _dragOverHighlightedStack;

    public event Action<string>? ItemLaunchFailed;

    private void IconGrid_DragOver(object sender, DragEventArgs e)
    {
        // Without this, WPF shows the "no-drop" cursor for the whole drag
        // even though Drop would still fire correctly on release — the
        // effect feedback (DragOver -> GiveFeedback) is a separate, opt-in
        // mechanism from whether AllowDrop actually accepts the drop.
        e.Effects = e.Data.GetDataPresent(typeof(List<LadaItem>)) || e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;

        if (e.Data.GetDataPresent(typeof(List<LadaItem>)))
        {
            UpdateDragOverHighlight(FindStackAtPoint(e.GetPosition(ActiveItemsPanel)));
        }
    }

    private void IconGrid_DragLeave(object sender, DragEventArgs e)
    {
        UpdateDragOverHighlight(null);
    }

    private void UpdateDragOverHighlight(Panel? target)
    {
        if (ReferenceEquals(_dragOverHighlightedStack, target))
            return;

        if (_dragOverHighlightedStack is not null)
        {
            // Restore selection tint rather than hardcoding Transparent —
            // otherwise un-highlighting a selected item after a drag passes
            // over it would silently wipe its selection highlight.
            RestoreItemBackground(_dragOverHighlightedStack);
        }

        _dragOverHighlightedStack = target;

        if (_dragOverHighlightedStack is not null)
        {
            _dragOverHighlightedStack.Background = (Brush)FindResource("IconHoverBackgroundBrush");
        }
    }

    private void IconGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(List<LadaItem>)))
        {
            HandleItemReorderDrop(e);
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        foreach (var path in paths)
        {
            AddItem(path);
        }

        if (_autoSortEnabled)
        {
            SortItemsByType();
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandleItemReorderDrop(DragEventArgs e)
    {
        var draggedItems = (List<LadaItem>)e.Data.GetData(typeof(List<LadaItem>))!;
        var targetItem = FindStackAtPoint(e.GetPosition(ActiveItemsPanel))?.Tag as LadaItem;

        foreach (var draggedItem in draggedItems)
        {
            _items.Remove(draggedItem);
        }

        var insertIndex = targetItem is not null && !draggedItems.Contains(targetItem)
            ? _items.IndexOf(targetItem)
            : _items.Count;

        _items.InsertRange(insertIndex, draggedItems);

        ReflowGrid();
        _dragOverHighlightedStack = null;

        if (_autoSortEnabled)
        {
            _autoSortEnabled = false;
            if (_autoSortMenuItem is not null)
            {
                _autoSortMenuItem.IsChecked = false;
            }
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private Panel? FindStackAtPoint(Point point)
    {
        foreach (var child in ActiveItemsPanel.Children.OfType<Panel>())
        {
            var topLeft = child.TranslatePoint(new Point(0, 0), ActiveItemsPanel);
            var bounds = new Rect(topLeft, child.RenderSize);

            if (bounds.Contains(point))
            {
                return child;
            }
        }

        return null;
    }

    private void AddItem(string path)
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            Path = path,
            DisplayName = Path.GetFileNameWithoutExtension(path),
            Column = column,
            Row = row
        };
        _items.Add(item);
        _tabs[_activeTabIndex].LastActivityUtc = DateTime.UtcNow;
        RenderSingleItem(item);
        EnsureContentFits();
    }

    private static (int column, int row) FindNextFreeCell(List<LadaItem> items)
    {
        var occupied = new HashSet<(int, int)>(items.Select(i => (i.Column, i.Row)));
        for (var row = 0; ; row++)
        {
            for (var column = 0; column < ColumnsPerRow; column++)
            {
                if (!occupied.Contains((column, row)))
                    return (column, row);
            }
        }
    }

    private void RenderItem(LadaItem item)
    {
        if (item.IsDrawer)
        {
            RenderDrawerItem(item);
            return;
        }

        if (item.IsClockWidget)
        {
            RenderClockWidget(item);
            return;
        }

        if (item.IsDiskWidget)
        {
            RenderDiskWidget(item);
            return;
        }

        if (item.IsTimerWidget)
        {
            RenderTimerWidget(item);
            return;
        }

        if (item.IsBatteryWidget)
        {
            RenderBatteryWidget(item);
            return;
        }

        if (item.IsMemoryWidget)
        {
            RenderMemoryWidget(item);
            return;
        }

        if (item.IsCpuWidget)
        {
            RenderCpuWidget(item);
            return;
        }

        if (item.IsGpuWidget)
        {
            RenderGpuWidget(item);
            return;
        }

        if (item.IsNetworkWidget)
        {
            RenderNetworkWidget(item);
            return;
        }

        var icon = FileTypeCategorizer.Categorize(item.Path) == FileCategory.Image
            ? ImageThumbnailService.GetThumbnail(item.Path) ?? ShellIconService.GetIcon(item.Path)
            : ShellIconService.GetIcon(item.Path);

        // WrapPanel gives every item in a row the height of that row's tallest
        // item; without an explicit Top alignment this StackPanel stretches to
        // match, so its selection-highlight Background would bleed past its
        // own (shorter) content instead of hugging just the icon and label.
        var stack = new StackPanel { Width = 64, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Top, Cursor = System.Windows.Input.Cursors.Hand, Tag = item };

        if (icon is BitmapSource bitmap)
        {
            stack.Children.Add(new Image { Source = bitmap, Width = 32, Height = 32, Margin = new Thickness(0, 0, 0, 4) });
        }

        var label = new TextBlock
        {
            Text = item.DisplayName,
            Style = (Style)FindResource("IconLabelStyle")
        };
        if (ItemLabelAccentOverride() is { } labelBrush)
        {
            label.Foreground = labelBrush;
        }
        stack.Children.Add(label);

        stack.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
                LaunchItem(item);
        };

        AttachItemDragSource(stack, stack, item);

        stack.ContextMenu = FileTypeCategorizer.Categorize(item.Path) == FileCategory.Folder
            ? BuildFolderItemContextMenu(item, stack)
            : BuildDefaultItemContextMenu(item, stack);

        IconGrid.Children.Add(stack);
    }

    private ContextMenu BuildDefaultItemContextMenu(LadaItem item, Panel stack)
    {
        var contextMenu = new ContextMenu();

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            contextMenu.Items.Add(moveToSubmenu);
        }

        var removeMenuItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeMenuItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        contextMenu.Items.Add(removeMenuItem);
        return contextMenu;
    }

    private string BuildRemoveMenuLabel(LadaItem item) =>
        _selectedItems.Contains(item) && _selectedItems.Count > 1
            ? Strings.RemoveFromLadaCount(_selectedItems.Count)
            : Strings.RemoveFromLada;

    private void RemoveItemOrSelection(LadaItem item, Panel stack)
    {
        if (_selectedItems.Contains(item) && _selectedItems.Count > 1)
        {
            RemoveSelectedItems();
        }
        else
        {
            RemoveItem(item, stack);
        }
    }

    private void RemoveItem(LadaItem item, Panel stack)
    {
        // An expanded drawer in list view has a second sibling element (its
        // children ScrollViewer, see BuildDrawerChildrenPanel in
        // LadaWindow.Drawer.cs) that a surgical single-element removal below
        // wouldn't know about, orphaning it. A full RenderListPanel rebuild
        // avoids that; every other case keeps the cheap surgical removal.
        var needsListRebuild = item.IsDrawer && _tabs[_activeTabIndex].ViewMode == ItemViewMode.List;

        StopWatchingDrawer(item);
        StopClockTimer(item);
        StopDiskTimer(item);
        StopTimerTicking(item);
        RestoreDesktopIconIfAbsorbed(item);
        _items.Remove(item);
        _tabs[_activeTabIndex].LastActivityUtc = DateTime.UtcNow;

        if (needsListRebuild)
        {
            RenderListPanel();
            EnsureContentFits();
        }
        else
        {
            ActiveItemsPanel.Children.Remove(stack);
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LaunchItem(LadaItem item) => LaunchPath(item.Path, item.DisplayName);

    private void LaunchPath(string path, string displayName)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(LaunchPath), ex);
            ItemLaunchFailed?.Invoke(Strings.ItemLaunchFailed(displayName));
        }
    }

    private void AttachItemDragSource(FrameworkElement dragHandle, Panel stack, LadaItem item)
    {
        dragHandle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _itemDragStart = e.GetPosition(ActiveItemsPanel);
            _isDraggingItem = false;
            HandleItemSelectionMouseDown(item);
        };

        dragHandle.MouseLeftButtonUp += (_, _) => HandleItemSelectionMouseUp(item);

        dragHandle.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDraggingItem)
                return;

            var current = e.GetPosition(ActiveItemsPanel);
            var delta = current - _itemDragStart;

            if (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4)
            {
                _isDraggingItem = true;
                _selectionCollapsePending = false;
                // This window is WS_EX_NOACTIVATE, and OLE drag-and-drop
                // (which DoDragDrop uses) is documented to behave
                // unreliably when the drag source isn't the foreground
                // window — same underlying issue already worked around for
                // title editing (see ActivateForTitleEdit). The periodic
                // HWND_BOTTOM reassertion pins it back below other windows
                // afterward regardless.
                if (_hwnd != IntPtr.Zero)
                    NativeMethods.SetForegroundWindow(_hwnd);

                var draggedItems = (_selectedItems.Contains(item) ? _selectedItems : new HashSet<LadaItem> { item })
                    .OrderBy(i => _items.IndexOf(i))
                    .ToList();

                stack.Opacity = 0.4;
                DragDrop.DoDragDrop(stack, draggedItems, DragDropEffects.Move);
                stack.Opacity = 1.0;

                _isDraggingItem = false;
            }
        };
    }
}
