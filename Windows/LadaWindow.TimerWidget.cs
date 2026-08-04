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
    private const double TimerWidgetWidth = 90;
    private const double TimerBarWidth = 70;

    private readonly Dictionary<LadaItem, DispatcherTimer> _timerWidgetTimers = new();

    public event Action<string>? TimerFinished;

    private void AddTimerWidget()
    {
        var picker = new TimerDurationPickerWindow { Owner = this };
        if (picker.ShowDialog() != true)
            return;

        var (column, row) = FindNextFreeCell(_items);
        var totalSeconds = (int)picker.SelectedDuration.TotalSeconds;
        var item = new LadaItem
        {
            IsTimerWidget = true,
            DisplayName = Strings.TimerWidgetMenuItem,
            TimerDurationSeconds = totalSeconds,
            TimerRemainingSeconds = totalSeconds,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderTimerWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = TimerWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var label = new TextBlock
        {
            Text = item.DisplayName,
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var timeLabel = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.Medium,
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
            Width = TimerBarWidth,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = (Brush)FindResource("IconHoverBackgroundBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true,
            Child = barFill
        };

        stack.Children.Add(label);
        stack.Children.Add(timeLabel);
        stack.Children.Add(barTrack);

        AttachItemDragSource(stack, stack, item);
        stack.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 1)
                ToggleTimerRunning(item, timeLabel, barFill);
        };
        stack.ContextMenu = BuildTimerWidgetContextMenu(item, stack, timeLabel, barFill);

        IconGrid.Children.Add(stack);

        UpdateTimerDisplay(item, timeLabel, barFill);
        if (item.TimerEndUtc is not null)
        {
            StartTimerTicking(item, timeLabel, barFill);
        }
    }

    private ContextMenu BuildTimerWidgetContextMenu(LadaItem item, StackPanel stack, TextBlock timeLabel, Border barFill)
    {
        var menu = new ContextMenu();

        var toggleItem = new MenuItem { Header = item.TimerEndUtc is not null ? Strings.PauseTimer : Strings.StartTimer };
        toggleItem.Click += (_, _) => ToggleTimerRunning(item, timeLabel, barFill);
        // Built once and reused across opens (matching Clock/Disk's menus), so
        // the Start/Pause label needs a live refresh on every open, not just
        // at construction time -- otherwise it goes stale after the first toggle.
        menu.Opened += (_, _) => toggleItem.Header = item.TimerEndUtc is not null ? Strings.PauseTimer : Strings.StartTimer;
        menu.Items.Add(toggleItem);

        var resetItem = new MenuItem { Header = Strings.ResetTimer };
        resetItem.Click += (_, _) => ResetTimer(item, timeLabel, barFill);
        menu.Items.Add(resetItem);

        var changeDurationItem = new MenuItem { Header = Strings.ChangeTimerDuration };
        changeDurationItem.Click += (_, _) => ChangeTimerDuration(item, timeLabel, barFill);
        menu.Items.Add(changeDurationItem);

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            menu.Items.Add(moveToSubmenu);
        }

        var removeItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        menu.Items.Add(removeItem);

        return menu;
    }

    private void ToggleTimerRunning(LadaItem item, TextBlock timeLabel, Border barFill)
    {
        if (item.TimerEndUtc is not null)
        {
            item.TimerRemainingSeconds = GetCurrentRemaining(item).TotalSeconds;
            item.TimerEndUtc = null;
            StopTimerTicking(item);
        }
        else
        {
            var remaining = GetCurrentRemaining(item);
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.FromSeconds(item.TimerDurationSeconds);
            }

            item.TimerEndUtc = DateTime.UtcNow + remaining;
            StartTimerTicking(item, timeLabel, barFill);
        }

        UpdateTimerDisplay(item, timeLabel, barFill);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetTimer(LadaItem item, TextBlock timeLabel, Border barFill)
    {
        item.TimerEndUtc = null;
        item.TimerRemainingSeconds = item.TimerDurationSeconds;
        StopTimerTicking(item);
        UpdateTimerDisplay(item, timeLabel, barFill);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChangeTimerDuration(LadaItem item, TextBlock timeLabel, Border barFill)
    {
        var picker = new TimerDurationPickerWindow { Owner = this };
        if (picker.ShowDialog() != true)
            return;

        item.TimerDurationSeconds = (int)picker.SelectedDuration.TotalSeconds;
        item.TimerRemainingSeconds = item.TimerDurationSeconds;
        item.TimerEndUtc = null;
        StopTimerTicking(item);
        UpdateTimerDisplay(item, timeLabel, barFill);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private TimeSpan GetCurrentRemaining(LadaItem item) =>
        item.TimerEndUtc is { } endUtc
            ? TimerCountdownCalculator.RemainingFrom(endUtc, DateTime.UtcNow)
            : TimeSpan.FromSeconds(item.TimerRemainingSeconds);

    private void UpdateTimerDisplay(LadaItem item, TextBlock timeLabel, Border barFill)
    {
        var remaining = GetCurrentRemaining(item);

        if (remaining <= TimeSpan.Zero && item.TimerEndUtc is not null)
        {
            item.TimerEndUtc = null;
            item.TimerRemainingSeconds = 0;
            StopTimerTicking(item);
            TimerFinished?.Invoke(Strings.TimerFinishedMessage(item.DisplayName));
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            remaining = TimeSpan.Zero;
        }

        timeLabel.Text = FormatTimerRemaining(remaining);

        var total = item.TimerDurationSeconds > 0 ? item.TimerDurationSeconds : 1;
        var fraction = Math.Clamp(remaining.TotalSeconds / total, 0, 1);
        barFill.Width = fraction * TimerBarWidth;
    }

    private static string FormatTimerRemaining(TimeSpan remaining) =>
        remaining.TotalHours >= 1
            ? remaining.ToString(@"hh\:mm\:ss")
            : remaining.ToString(@"mm\:ss");

    private void StartTimerTicking(LadaItem item, TextBlock timeLabel, Border barFill)
    {
        StopTimerTicking(item);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateTimerDisplay(item, timeLabel, barFill);
        timer.Start();

        _timerWidgetTimers[item] = timer;
    }

    private void StopTimerTicking(LadaItem item)
    {
        if (_timerWidgetTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _timerWidgetTimers.Remove(item);
        }
    }

    private void DisposeAllTimerWidgetTimers()
    {
        foreach (var item in new List<LadaItem>(_timerWidgetTimers.Keys))
        {
            StopTimerTicking(item);
        }
    }
}
