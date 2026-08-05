using System;
using System.Collections.Generic;
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
    private const double BatteryWidgetWidth = 90;
    private const double BatteryBarWidth = 70;

    private readonly Dictionary<LadaItem, DispatcherTimer> _batteryTimers = new();

    private void AddBatteryWidget()
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsBatteryWidget = true,
            DisplayName = Strings.BatteryWidgetMenuItem,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderBatteryWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = BatteryWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var accent = ItemLabelAccentOverride();

        var titleLabel = new TextBlock
        {
            Text = Strings.BatteryWidgetMenuItem,
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        if (accent is not null)
            titleLabel.Foreground = accent;

        var percentLabel = new TextBlock
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
            Width = BatteryBarWidth,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = WidgetTrackBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true,
            Child = barFill
        };

        stack.Children.Add(titleLabel);
        stack.Children.Add(percentLabel);
        stack.Children.Add(barTrack);

        AttachItemDragSource(stack, stack, item);
        stack.ContextMenu = BuildBatteryWidgetContextMenu(item, stack);

        IconGrid.Children.Add(stack);

        StartBatteryTimer(item, percentLabel, barFill);
    }

    private ContextMenu BuildBatteryWidgetContextMenu(LadaItem item, StackPanel stack)
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

    private void StartBatteryTimer(LadaItem item, TextBlock percentLabel, Border barFill)
    {
        void Tick()
        {
            try
            {
                if (!NativeMethods.GetSystemPowerStatus(out var status)
                    || (status.BatteryFlag & NativeMethods.BATTERY_FLAG_NO_SYSTEM_BATTERY) != 0
                    || status.BatteryLifePercent == NativeMethods.BATTERY_PERCENT_UNKNOWN)
                {
                    percentLabel.Text = Strings.WidgetUnavailable;
                    barFill.Width = 0;
                    return;
                }

                var percent = status.BatteryLifePercent;
                var charging = (status.BatteryFlag & NativeMethods.BATTERY_FLAG_CHARGING) != 0;

                percentLabel.Text = Strings.BatteryPercent(percent.ToString())
                    + (charging ? Strings.BatteryChargingSuffix : "");
                barFill.Width = Math.Clamp(percent / 100.0, 0, 1) * BatteryBarWidth;
            }
            catch (Exception ex)
            {
                Logger.LogError(nameof(StartBatteryTimer), ex);
                percentLabel.Text = Strings.WidgetUnavailable;
                barFill.Width = 0;
            }
        }

        Tick();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        _batteryTimers[item] = timer;
    }

    private void StopBatteryTimer(LadaItem item)
    {
        if (_batteryTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _batteryTimers.Remove(item);
        }
    }

    private void DisposeAllBatteryTimers()
    {
        foreach (var item in new List<LadaItem>(_batteryTimers.Keys))
        {
            StopBatteryTimer(item);
        }
    }
}
