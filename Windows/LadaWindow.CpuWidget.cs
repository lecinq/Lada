using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private const double CpuWidgetWidth = 90;
    private const double CpuBarWidth = 70;
    private const int SparklineSampleCount = 30; // 2s tick x 30 = 1 minute of history
    private const double SparklineHeight = 24;

    private readonly Dictionary<LadaItem, Action> _cpuRefreshHandlers = new();

    private void AddCpuWidget()
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsCpuWidget = true,
            DisplayName = Strings.CpuWidgetMenuItem,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderCpuWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = CpuWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var accent = ItemLabelAccentOverride();

        var titleLabel = new TextBlock
        {
            Text = Strings.CpuWidgetMenuItem,
            Style = (Style)FindResource("IconLabelStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        if (accent is not null)
            titleLabel.Foreground = accent;
        stack.Children.Add(titleLabel);

        Polyline? sparkline = null;
        if (item.ShowDetailedView)
        {
            sparkline = new Polyline
            {
                Width = CpuBarWidth,
                Height = SparklineHeight,
                Stroke = accent ?? (Brush)FindResource("AccentBrush"),
                StrokeThickness = 1.5,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(sparkline);
        }

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
            Width = CpuBarWidth,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = WidgetTrackBrush(accent),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true,
            Child = barFill
        };

        stack.Children.Add(usageLabel);
        stack.Children.Add(barTrack);

        TextBlock? frequencyLabel = null;
        if (item.ShowDetailedView)
        {
            frequencyLabel = new TextBlock
            {
                Text = "…",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = WidgetDimmedAccentBrush(accent),
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(frequencyLabel);
        }

        var temperatureLabel = new TextBlock
        {
            Text = "…",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = WidgetDimmedAccentBrush(accent),
            Margin = new Thickness(0, 4, 0, 0)
        };
        stack.Children.Add(temperatureLabel);

        AttachItemDragSource(stack, stack, item);
        stack.ContextMenu = BuildCpuWidgetContextMenu(item, stack);

        IconGrid.Children.Add(stack);

        StartCpuUpdates(item, usageLabel, barFill, temperatureLabel, sparkline, frequencyLabel);
    }

    private ContextMenu BuildCpuWidgetContextMenu(LadaItem item, StackPanel stack)
    {
        var menu = new ContextMenu();

        var detailedViewItem = new MenuItem
        {
            Header = Strings.DetailedViewMenuItem,
            IsCheckable = true,
            IsChecked = item.ShowDetailedView
        };
        detailedViewItem.Click += (_, _) =>
        {
            StopCpuUpdates(item);
            IconGrid.Children.Remove(stack);
            item.ShowDetailedView = detailedViewItem.IsChecked;
            RenderItem(item);
            EnsureContentFits();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        };
        menu.Items.Add(detailedViewItem);

        if (BuildMoveToTabSubmenu(item) is { } moveToSubmenu)
        {
            menu.Items.Add(moveToSubmenu);
        }

        var removeItem = new MenuItem { Header = BuildRemoveMenuLabel(item) };
        removeItem.Click += (_, _) => RemoveItemOrSelection(item, stack);
        menu.Items.Add(removeItem);

        return menu;
    }

    // CPU/GPU widgets don't own a DispatcherTimer each -- they subscribe to
    // HardwareMonitorService.Updated, which polls LibreHardwareMonitor once
    // for every CPU/GPU widget across every lada, not once per widget.
    private void StartCpuUpdates(LadaItem item, TextBlock usageLabel, Border barFill, TextBlock temperatureLabel, Polyline? sparkline, TextBlock? frequencyLabel)
    {
        var samples = new List<float>();

        void Tick()
        {
            try
            {
                var load = _hardwareMonitorService.GetCpuLoad();
                if (load is not { } loadValue)
                {
                    usageLabel.Text = Strings.WidgetUnavailable;
                    barFill.Width = 0;
                    temperatureLabel.Text = "";
                    return;
                }

                usageLabel.Text = Strings.UsagePercent(loadValue.ToString("0"));
                barFill.Width = Math.Clamp(loadValue / 100.0, 0, 1) * CpuBarWidth;

                var temperature = _hardwareMonitorService.GetCpuTemperature();
                temperatureLabel.Text = temperature is { } tempValue
                    ? Strings.TemperatureCelsius(tempValue.ToString("0"))
                    : Strings.WidgetUnavailable;

                if (sparkline is not null)
                {
                    samples.Add(loadValue);
                    if (samples.Count > SparklineSampleCount)
                        samples.RemoveAt(0);
                    UpdateSparkline(sparkline, samples, CpuBarWidth, SparklineHeight, 100);
                }

                if (frequencyLabel is not null)
                {
                    var frequency = _hardwareMonitorService.GetCpuFrequency();
                    frequencyLabel.Text = frequency is { } freqValue
                        ? Strings.FrequencyGhz((freqValue / 1000).ToString("0.0"))
                        : Strings.WidgetUnavailable;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(nameof(StartCpuUpdates), ex);
                usageLabel.Text = Strings.WidgetUnavailable;
                barFill.Width = 0;
                temperatureLabel.Text = "";
            }
        }

        _hardwareMonitorService.EnsureStarted();
        Tick();

        void Handler() => Tick();
        _hardwareMonitorService.Updated += Handler;
        _cpuRefreshHandlers[item] = Handler;
    }

    private void StopCpuUpdates(LadaItem item)
    {
        if (_cpuRefreshHandlers.TryGetValue(item, out var handler))
        {
            _hardwareMonitorService.Updated -= handler;
            _cpuRefreshHandlers.Remove(item);
        }
    }

    private void DisposeAllCpuUpdates()
    {
        foreach (var item in new List<LadaItem>(_cpuRefreshHandlers.Keys))
        {
            StopCpuUpdates(item);
        }
    }

    // Shared by every widget with a progress bar (Battery/Memory/Timer/Disk/
    // CPU/GPU). At a low fill percentage, most of what's visible is the
    // TRACK, not the fill -- so leaving the track on the theme's fixed,
    // un-accented IconHoverBackgroundBrush (green in Anderson) made a
    // blue-accented lada's mostly-empty CPU bar read as "green" at a
    // glance, even though the small filled sliver was correctly blue. A
    // faint tint of the same accent keeps the whole bar visually part of
    // the same lada regardless of fill level.
    private Brush WidgetTrackBrush(Brush? accent) =>
        accent is SolidColorBrush solid
            ? new SolidColorBrush(solid.Color) { Opacity = 0.15 }
            : (Brush)FindResource("IconHoverBackgroundBrush");

    // Same idea as WidgetTrackBrush but for text that needs to stay legible
    // (an inactive tab, a secondary stat) rather than just hint at a filled
    // region -- dimmed enough to read as "less prominent than the active/
    // primary one" while still being this lada's own hue, not the theme's.
    private Brush WidgetDimmedAccentBrush(Brush? accent) =>
        accent is SolidColorBrush solid
            ? new SolidColorBrush(solid.Color) { Opacity = 0.55 }
            : (Brush)FindResource("SecondaryTextBrush");

    // Shared with the GPU and Network widgets' sparklines. CPU/GPU pass a
    // fixed maxValue of 100 (a usage percentage); Network has no natural
    // ceiling, so it passes the largest sample currently in the window
    // instead, auto-scaling the graph to whatever's actually happening.
    private static void UpdateSparkline(Polyline sparkline, IReadOnlyList<float> samples, double width, double height, double maxValue)
    {
        var points = new PointCollection();
        for (var i = 0; i < samples.Count; i++)
        {
            var x = samples.Count > 1 ? i / (double)(samples.Count - 1) * width : width / 2;
            var y = height - (maxValue > 0 ? Math.Clamp(samples[i] / maxValue, 0, 1) : 0) * height;
            points.Add(new Point(x, y));
        }
        sparkline.Points = points;
    }
}
