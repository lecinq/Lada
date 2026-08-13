using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly Dictionary<LadaItem, CancellationTokenSource> _timerAlertTokens = new();

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

        var accent = ItemLabelAccentOverride();

        var label = new TextBlock
        {
            Text = item.DisplayName,
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        if (accent is not null)
            label.Foreground = accent;

        var timeLabel = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeights.Medium,
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
            Width = TimerBarWidth,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = WidgetTrackBrush(accent),
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
        // A click while the finished-alert is still beeping only silences
        // it (StopTimerTicking cancels the alert token) -- it does NOT
        // fall through to starting a fresh countdown in the same click.
        // Without this check, TimerEndUtc being null (the finished state)
        // was indistinguishable from "never started", so the very click
        // meant to dismiss the alarm also immediately restarted it.
        if (_timerAlertTokens.ContainsKey(item))
        {
            StopTimerTicking(item);
            UpdateTimerDisplay(item, timeLabel, barFill);
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

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
            PlayTimerFinishedSound(item);
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

    // A synthesized double-beep (Console.Beep, no audio asset needed) rather
    // than SystemSounds -- that shares its sound with the tray balloon
    // notification fired right after this, so both firing together just
    // played the same alert twice. Console.Beep blocks the calling thread
    // for its duration, so this runs on a background thread rather than the
    // UI thread. Each double-beep spans ~200ms, then 800ms of silence
    // before the next one -- a 1-second loop -- and keeps repeating until
    // StopTimerTicking cancels this item's token, which already happens
    // from every path that touches this timer again (clicking the widget
    // to restart it, Reset, Change duration, removing the item, or closing
    // the lada), so no separate "dismiss" gesture was needed.
    private void PlayTimerFinishedSound(LadaItem item)
    {
        var cts = new CancellationTokenSource();
        _timerAlertTokens[item] = cts;
        var token = cts.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                Console.Beep(1500, 90);
                if (token.WaitHandle.WaitOne(20)) return;
                Console.Beep(1500, 90);
                if (token.WaitHandle.WaitOne(800)) return;
            }
        });
    }

    private void StartTimerTicking(LadaItem item, TextBlock timeLabel, Border barFill)
    {
        StopTimerTicking(item);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => UpdateTimerDisplay(item, timeLabel, barFill);
        timer.Start();

        _timerWidgetTimers[item] = timer;
    }

    // Also cancels this item's finished-alert loop, if one is currently
    // running -- StopTimerTicking is already called from every path that
    // should silence it (see PlayTimerFinishedSound), so hooking it in
    // here covers all of them at once instead of repeating the same
    // cancellation at each call site.
    private void StopTimerTicking(LadaItem item)
    {
        if (_timerWidgetTimers.TryGetValue(item, out var timer))
        {
            timer.Stop();
            _timerWidgetTimers.Remove(item);
        }

        if (_timerAlertTokens.TryGetValue(item, out var cts))
        {
            cts.Cancel();
            _timerAlertTokens.Remove(item);
        }
    }

    private void DisposeAllTimerWidgetTimers()
    {
        foreach (var item in new List<LadaItem>(_timerWidgetTimers.Keys))
        {
            StopTimerTicking(item);
        }
    }

    // A timer that already finished (and is mid-alert-loop) has no entry
    // left in _timerWidgetTimers -- its ticking timer stopped the moment it
    // hit zero -- so DisposeAllTimerWidgetTimers' own loop above wouldn't
    // reach it. Without this, closing a lada while one of its timers is
    // still beeping would leave that background loop running forever.
    private void DisposeAllTimerAlerts()
    {
        foreach (var item in new List<LadaItem>(_timerAlertTokens.Keys))
        {
            StopTimerTicking(item);
        }
    }
}
