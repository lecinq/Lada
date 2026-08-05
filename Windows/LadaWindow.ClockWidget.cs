using System;
using System.Collections.Generic;
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
    private const double ClockWidgetWidth = 90;

    private readonly Dictionary<LadaItem, DispatcherTimer> _clockTimers = new();

    private void AddClockWidget()
    {
        var picker = new TimeZonePickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedTimeZone is null)
            return;

        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsClockWidget = true,
            TimeZoneId = picker.SelectedTimeZone.Id,
            DisplayName = picker.SelectedTimeZone.DisplayName,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChangeClockTimeZone(LadaItem item, TextBlock cityLabel)
    {
        var picker = new TimeZonePickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedTimeZone is null)
            return;

        item.TimeZoneId = picker.SelectedTimeZone.Id;
        item.DisplayName = picker.SelectedTimeZone.DisplayName;
        cityLabel.Text = item.DisplayName;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderClockWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = ClockWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var cityLabel = new TextBlock
        {
            Text = item.DisplayName,
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var timeLabel = new TextBlock
        {
            Text = "--:--:--",
            FontSize = 18,
            FontWeight = FontWeights.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)FindResource("TitleTextBrush")
        };

        if (ItemLabelAccentOverride() is { } accent)
        {
            cityLabel.Foreground = accent;
            timeLabel.Foreground = accent;
        }

        stack.Children.Add(cityLabel);
        stack.Children.Add(timeLabel);

        AttachItemDragSource(stack, stack, item);
        stack.ContextMenu = BuildClockWidgetContextMenu(item, stack, cityLabel);

        IconGrid.Children.Add(stack);

        StartClockTimer(item, timeLabel);
    }

    private ContextMenu BuildClockWidgetContextMenu(LadaItem item, StackPanel stack, TextBlock cityLabel)
    {
        var menu = new ContextMenu();

        var changeZoneItem = new MenuItem { Header = Strings.ChangeTimeZone };
        changeZoneItem.Click += (_, _) => ChangeClockTimeZone(item, cityLabel);
        menu.Items.Add(changeZoneItem);

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            menu.Items.Add(moveToSubmenu);
        }

        var removeItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        menu.Items.Add(removeItem);

        return menu;
    }

    private void StartClockTimer(LadaItem item, TextBlock timeLabel)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId ?? "UTC");
        }
        catch (TimeZoneNotFoundException ex)
        {
            Logger.LogError(nameof(StartClockTimer), ex);
            timeLabel.Text = "?";
            return;
        }

        void Tick() => timeLabel.Text = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).ToString("HH:mm:ss");

        Tick();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        _clockTimers[item] = timer;
    }

    private void StopClockTimer(LadaItem item)
    {
        if (_clockTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _clockTimers.Remove(item);
        }
    }

    private void DisposeAllClockTimers()
    {
        foreach (var item in new List<LadaItem>(_clockTimers.Keys))
        {
            StopClockTimer(item);
        }
    }
}
