using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private const double DiskWidgetWidth = 90;
    private const double DiskBarWidth = 70;

    private readonly Dictionary<LadaItem, DispatcherTimer> _diskTimers = new();

    // Shared by the "Nouveau widget > Espace disque" creation submenu (Sort.cs)
    // and "Changer de lecteur" on an existing widget below.
    private IEnumerable<MenuItem> BuildDriveMenuItems(Action<string> onSelected)
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var path = drive.Name;
            var menuItem = new MenuItem { Header = path };
            menuItem.Click += (_, _) => onSelected(path);
            yield return menuItem;
        }
    }

    private void AddDiskWidget(string drivePath)
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsDiskWidget = true,
            DrivePath = drivePath,
            DisplayName = drivePath,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChangeDisk(LadaItem item, TextBlock driveLabel, string newDrivePath)
    {
        item.DrivePath = newDrivePath;
        item.DisplayName = newDrivePath;
        driveLabel.Text = newDrivePath;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderDiskWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = DiskWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var driveLabel = new TextBlock
        {
            Text = item.DrivePath ?? "?",
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var freeSpaceLabel = new TextBlock
        {
            Text = "…",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)FindResource("TitleTextBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var barFill = new Border
        {
            Width = 0,
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = (Brush)FindResource("AccentBrush")
        };
        var barTrack = new Border
        {
            Width = DiskBarWidth,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = (Brush)FindResource("IconHoverBackgroundBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true,
            Child = barFill
        };

        stack.Children.Add(driveLabel);
        stack.Children.Add(freeSpaceLabel);
        stack.Children.Add(barTrack);

        AttachItemDragSource(stack, stack, item);
        stack.ContextMenu = BuildDiskWidgetContextMenu(item, stack, driveLabel);

        IconGrid.Children.Add(stack);

        StartDiskTimer(item, freeSpaceLabel, barFill);
    }

    private ContextMenu BuildDiskWidgetContextMenu(LadaItem item, StackPanel stack, TextBlock driveLabel)
    {
        var menu = new ContextMenu();

        var changeDriveSubmenu = new MenuItem { Header = Strings.ChangeDrive };
        foreach (var driveItem in BuildDriveMenuItems(path => ChangeDisk(item, driveLabel, path)))
        {
            changeDriveSubmenu.Items.Add(driveItem);
        }
        menu.Items.Add(changeDriveSubmenu);

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            menu.Items.Add(moveToSubmenu);
        }

        var removeItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        menu.Items.Add(removeItem);

        return menu;
    }

    private void StartDiskTimer(LadaItem item, TextBlock freeSpaceLabel, Border barFill)
    {
        void Tick()
        {
            try
            {
                var drive = new DriveInfo(item.DrivePath ?? "C:\\");
                if (!drive.IsReady)
                {
                    freeSpaceLabel.Text = Strings.DiskUnavailable;
                    barFill.Width = 0;
                    return;
                }

                var freeBytes = (double)drive.AvailableFreeSpace;
                var totalBytes = (double)drive.TotalSize;
                var freeGb = freeBytes / 1024 / 1024 / 1024;

                freeSpaceLabel.Text = Strings.DiskFreeSpace(freeGb.ToString("0.#"));

                var usedFraction = totalBytes > 0 ? 1 - (freeBytes / totalBytes) : 0;
                barFill.Width = Math.Clamp(usedFraction, 0, 1) * DiskBarWidth;
            }
            catch (Exception ex)
            {
                Logger.LogError(nameof(StartDiskTimer), ex);
                freeSpaceLabel.Text = Strings.DiskUnavailable;
                barFill.Width = 0;
            }
        }

        Tick();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        _diskTimers[item] = timer;
    }

    private void StopDiskTimer(LadaItem item)
    {
        if (_diskTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _diskTimers.Remove(item);
        }
    }

    private void DisposeAllDiskTimers()
    {
        foreach (var item in new List<LadaItem>(_diskTimers.Keys))
        {
            StopDiskTimer(item);
        }
    }
}
