using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Lada.Models;
using Lada.Native;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private const double MemoryWidgetWidth = 90;
    private const double MemoryBarWidth = 70;

    private readonly Dictionary<LadaItem, DispatcherTimer> _memoryTimers = new();

    private void AddMemoryWidget()
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsMemoryWidget = true,
            DisplayName = Strings.MemoryWidgetMenuItem,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderMemoryWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = MemoryWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var accent = ItemLabelAccentOverride();

        var titleLabel = new TextBlock
        {
            Text = Strings.MemoryWidgetMenuItem,
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        if (accent is not null)
            titleLabel.Foreground = accent;

        var usageLabel = new TextBlock
        {
            Text = "…",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = accent ?? (Brush)FindResource("TitleTextBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var barFill = new Border
        {
            Width = 0,
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = accent ?? (Brush)FindResource("AccentBrush")
        };
        var barTrack = new Border
        {
            Width = MemoryBarWidth,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = WidgetTrackBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true,
            Child = barFill
        };

        stack.Children.Add(titleLabel);
        stack.Children.Add(usageLabel);
        stack.Children.Add(barTrack);

        AttachItemDragSource(stack, stack, item);
        stack.ContextMenu = BuildMemoryWidgetContextMenu(item, stack);

        IconGrid.Children.Add(stack);

        StartMemoryTimer(item, usageLabel, barFill);
    }

    private ContextMenu BuildMemoryWidgetContextMenu(LadaItem item, StackPanel stack)
    {
        var menu = new ContextMenu();

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            menu.Items.Add(moveToSubmenu);
        }

        var removeItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        menu.Items.Add(removeItem);

        return menu;
    }

    private void StartMemoryTimer(LadaItem item, TextBlock usageLabel, Border barFill)
    {
        void Tick()
        {
            try
            {
                var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (!NativeMethods.GlobalMemoryStatusEx(ref status))
                {
                    usageLabel.Text = Strings.WidgetUnavailable;
                    barFill.Width = 0;
                    return;
                }

                var usedBytes = (double)(status.ullTotalPhys - status.ullAvailPhys);
                var totalBytes = (double)status.ullTotalPhys;
                var usedGb = usedBytes / 1024 / 1024 / 1024;
                var totalGb = totalBytes / 1024 / 1024 / 1024;

                usageLabel.Text = Strings.MemoryUsage(usedGb.ToString("0.#"), totalGb.ToString("0.#"));

                var usedFraction = totalBytes > 0 ? usedBytes / totalBytes : 0;
                barFill.Width = Math.Clamp(usedFraction, 0, 1) * MemoryBarWidth;
            }
            catch (Exception ex)
            {
                Logger.LogError(nameof(StartMemoryTimer), ex);
                usageLabel.Text = Strings.WidgetUnavailable;
                barFill.Width = 0;
            }
        }

        Tick();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        _memoryTimers[item] = timer;
    }

    private void StopMemoryTimer(LadaItem item)
    {
        if (_memoryTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _memoryTimers.Remove(item);
        }
    }

    private void DisposeAllMemoryTimers()
    {
        foreach (var item in new List<LadaItem>(_memoryTimers.Keys))
        {
            StopMemoryTimer(item);
        }
    }
}
