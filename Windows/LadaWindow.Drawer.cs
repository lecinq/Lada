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
using System.Windows.Threading;
using Lada.Models;
using Lada.Native;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private const double DrawerItemWidth = 220;
    private const double DrawerContentWidth = 200;
    private const double DrawerChildCellWidth = 56;
    private const double DrawerMaxVisibleHeight = 160;
    private static readonly TimeSpan DrawerWatcherDebounceInterval = TimeSpan.FromMilliseconds(300);

    private readonly Dictionary<LadaItem, FileSystemWatcher> _drawerWatchers = new();
    private readonly Dictionary<LadaItem, DispatcherTimer> _drawerRefreshTimers = new();

    public event Action<string>? DrawerOperationFailed;

    private ContextMenu BuildFolderItemContextMenu(LadaItem item, Panel stack)
    {
        var menu = new ContextMenu();

        var toggleItem = new MenuItem { Header = item.IsDrawer ? Strings.CollapseDrawer : Strings.ShowDrawerContent };
        toggleItem.Click += (_, _) => ToggleDrawer(item);
        menu.Items.Add(toggleItem);

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            menu.Items.Add(moveToSubmenu);
        }

        var removeItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        menu.Items.Add(removeItem);

        return menu;
    }

    private void ToggleDrawer(LadaItem item)
    {
        item.IsDrawer = !item.IsDrawer;
        ReflowGrid();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StartWatchingDrawer(LadaItem item, Panel innerContainer, Func<string, FrameworkElement> buildChildVisual)
    {
        try
        {
            var watcher = new FileSystemWatcher(item.Path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };

            var debounceTimer = new DispatcherTimer { Interval = DrawerWatcherDebounceInterval };
            debounceTimer.Tick += (_, _) =>
            {
                debounceTimer.Stop();
                RefreshDrawerContents(item, innerContainer, buildChildVisual);
            };

            void ScheduleRefresh() => Dispatcher.BeginInvoke(() =>
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            });

            watcher.Created += (_, _) => ScheduleRefresh();
            watcher.Deleted += (_, _) => ScheduleRefresh();
            watcher.Renamed += (_, _) => ScheduleRefresh();
            watcher.Error += (_, _) => Dispatcher.BeginInvoke(() => CollapseDrawerDueToError(item));

            watcher.EnableRaisingEvents = true;

            _drawerWatchers[item] = watcher;
            _drawerRefreshTimers[item] = debounceTimer;
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(StartWatchingDrawer), ex);
            CollapseDrawerDueToError(item);
        }
    }

    private void StopWatchingDrawer(LadaItem item)
    {
        if (_drawerRefreshTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _drawerRefreshTimers.Remove(item);
        }

        if (_drawerWatchers.TryGetValue(item, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _drawerWatchers.Remove(item);
        }
    }

    private void DisposeAllDrawerWatchers()
    {
        foreach (var item in _drawerWatchers.Keys.ToList())
        {
            StopWatchingDrawer(item);
        }
    }

    private void CollapseDrawerDueToError(LadaItem item)
    {
        if (!item.IsDrawer)
            return;

        item.IsDrawer = false;
        StopWatchingDrawer(item);
        ReflowGrid();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DrawerDropTarget_DragOver(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(List<LadaItem>)) || e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DrawerDropTarget_Drop(LadaItem drawerItem, DragEventArgs e)
    {
        e.Handled = true;

        if (e.Data.GetDataPresent(typeof(List<LadaItem>)))
        {
            var draggedItems = (List<LadaItem>)e.Data.GetData(typeof(List<LadaItem>))!;

            foreach (var draggedItem in draggedItems)
            {
                MoveIntoDrawer(draggedItem.Path, drawerItem);
                _items.Remove(draggedItem);
            }

            ReflowGrid();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            foreach (var path in paths)
            {
                MoveIntoDrawer(path, drawerItem);
            }
        }
    }

    private void MoveIntoDrawer(string sourcePath, LadaItem drawerItem)
    {
        var destinationPath = Path.Combine(drawerItem.Path, Path.GetFileName(sourcePath));

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            return;

        var isDirectory = Directory.Exists(sourcePath);

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            Logger.LogError(nameof(MoveIntoDrawer), new IOException($"Destination already exists: {destinationPath}"));
            DrawerOperationFailed?.Invoke(Strings.DrawerAlreadyExists(Path.GetFileName(sourcePath)));
            return;
        }

        try
        {
            if (isDirectory)
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }
        catch (IOException ex)
        {
            Logger.LogError(nameof(MoveIntoDrawer), ex);
            DrawerOperationFailed?.Invoke(isDirectory
                ? Strings.DrawerMoveFailedCrossDrive(Path.GetFileName(sourcePath))
                : Strings.DrawerMoveFailedGeneric(Path.GetFileName(sourcePath)));
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(MoveIntoDrawer), ex);
            DrawerOperationFailed?.Invoke(Strings.DrawerMoveFailedGeneric(Path.GetFileName(sourcePath)));
        }
    }

    private void RenderDrawerItem(LadaItem item)
    {
        var stack = new StackPanel { Width = DrawerItemWidth, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Top, Tag = item };

        var header = new TextBlock
        {
            Text = item.DisplayName,
            Style = (Style)FindResource("IconLabelStyle"),
            TextAlignment = TextAlignment.Left,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 4)
        };
        header.ContextMenu = BuildFolderItemContextMenu(item, stack);
        AttachItemDragSource(header, stack, item);
        stack.Children.Add(header);

        var innerGrid = new WrapPanel { Width = DrawerContentWidth };
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = DrawerMaxVisibleHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            AllowDrop = true,
            Content = innerGrid
        };
        scrollViewer.DragOver += (_, e) => DrawerDropTarget_DragOver(e);
        scrollViewer.Drop += (_, e) => DrawerDropTarget_Drop(item, e);
        stack.Children.Add(scrollViewer);

        IconGrid.Children.Add(stack);

        RefreshDrawerContents(item, innerGrid, BuildDrawerChildVisual);
        StartWatchingDrawer(item, innerGrid, BuildDrawerChildVisual);
    }

    private void RefreshDrawerContents(LadaItem item, Panel innerContainer, Func<string, FrameworkElement> buildChildVisual)
    {
        try
        {
            var entries = Directory.EnumerateFileSystemEntries(item.Path)
                .OrderBy(FileTypeCategorizer.Categorize)
                .ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            innerContainer.Children.Clear();

            foreach (var entryPath in entries)
            {
                innerContainer.Children.Add(buildChildVisual(entryPath));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(RefreshDrawerContents), ex);
            CollapseDrawerDueToError(item);
        }
    }

    // List-mode counterpart to RenderDrawerItem's inner ScrollViewer above --
    // inserted as the next sibling right after the drawer's own row in
    // ItemListPanel (RenderListPanel, LadaWindow.ListView.cs) instead of
    // nested inside a single grid-mode-style tile. Reuses the same
    // RefreshDrawerContents/StartWatchingDrawer live-sync machinery, just
    // targeting a plain StackPanel and a simpler row visual (BuildDrawerChildListRow)
    // instead of a WrapPanel of icon tiles.
    private FrameworkElement BuildDrawerChildrenPanel(LadaItem item)
    {
        var childrenPanel = new StackPanel();
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = DrawerMaxVisibleHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(30, 0, 0, 4),
            AllowDrop = true,
            Content = childrenPanel
        };
        scrollViewer.DragOver += (_, e) => DrawerDropTarget_DragOver(e);
        scrollViewer.Drop += (_, e) => DrawerDropTarget_Drop(item, e);

        RefreshDrawerContents(item, childrenPanel, BuildDrawerChildListRow);
        StartWatchingDrawer(item, childrenPanel, BuildDrawerChildListRow);

        return scrollViewer;
    }

    private FrameworkElement BuildDrawerChildListRow(string path)
    {
        var icon = FileTypeCategorizer.Categorize(path) == FileCategory.Image
            ? ImageThumbnailService.GetThumbnail(path) ?? ShellIconService.GetIcon(path)
            : ShellIconService.GetIcon(path);
        var isDirectory = Directory.Exists(path);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1), Cursor = Cursors.Hand };

        if (icon is BitmapSource bitmap)
        {
            row.Children.Add(new Image { Source = bitmap, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
        }

        var label = new TextBlock
        {
            Text = Path.GetFileName(path),
            FontFamily = (FontFamily)FindResource("LadaFontFamily"),
            FontSize = 11,
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        };
        if (ItemLabelAccentOverride() is { } labelBrush)
        {
            label.Foreground = labelBrush;
        }
        row.Children.Add(label);

        row.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2)
                return;

            if (isDirectory)
                OpenInExplorer(path);
            else
                LaunchPath(path, Path.GetFileName(path));
        };

        var dragStart = default(Point);
        var isDragging = false;

        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            dragStart = e.GetPosition(ActiveItemsPanel);
            isDragging = false;
        };

        row.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || isDragging)
                return;

            var delta = e.GetPosition(ActiveItemsPanel) - dragStart;
            if (Math.Abs(delta.X) <= 4 && Math.Abs(delta.Y) <= 4)
                return;

            isDragging = true;

            if (_hwnd != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(_hwnd);

            DragDrop.DoDragDrop(row, new DataObject(DataFormats.FileDrop, new[] { path }), DragDropEffects.Move);

            isDragging = false;
        };

        return row;
    }

    private FrameworkElement BuildDrawerChildVisual(string path)
    {
        var icon = FileTypeCategorizer.Categorize(path) == FileCategory.Image
            ? ImageThumbnailService.GetThumbnail(path) ?? ShellIconService.GetIcon(path)
            : ShellIconService.GetIcon(path);
        var isDirectory = Directory.Exists(path);

        var child = new StackPanel { Width = DrawerChildCellWidth, Margin = new Thickness(2), Cursor = Cursors.Hand };

        if (icon is BitmapSource bitmap)
        {
            child.Children.Add(new Image { Source = bitmap, Width = 20, Height = 20, Margin = new Thickness(0, 0, 0, 2) });
        }

        var label = new TextBlock
        {
            Text = Path.GetFileName(path),
            Style = (Style)FindResource("IconLabelStyle")
        };
        if (ItemLabelAccentOverride() is { } labelBrush)
        {
            label.Foreground = labelBrush;
        }
        child.Children.Add(label);

        child.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2)
                return;

            if (isDirectory)
                OpenInExplorer(path);
            else
                LaunchPath(path, Path.GetFileName(path));
        };

        var dragStart = default(Point);
        var isDragging = false;

        child.PreviewMouseLeftButtonDown += (_, e) =>
        {
            dragStart = e.GetPosition(IconGrid);
            isDragging = false;
        };

        child.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || isDragging)
                return;

            var delta = e.GetPosition(IconGrid) - dragStart;
            if (Math.Abs(delta.X) <= 4 && Math.Abs(delta.Y) <= 4)
                return;

            isDragging = true;

            if (_hwnd != IntPtr.Zero)
                NativeMethods.SetForegroundWindow(_hwnd);

            DragDrop.DoDragDrop(child, new DataObject(DataFormats.FileDrop, new[] { path }), DragDropEffects.Move);

            isDragging = false;
        };

        return child;
    }

    private void OpenInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\""));
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(OpenInExplorer), ex);
            ItemLaunchFailed?.Invoke(Strings.FolderOpenFailed(Path.GetFileName(path)));
        }
    }
}
