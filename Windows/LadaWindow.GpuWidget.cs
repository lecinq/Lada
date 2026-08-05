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
    private const double GpuWidgetWidth = 90;
    private const double GpuBarWidth = 70;

    private readonly Dictionary<LadaItem, Action> _gpuRefreshHandlers = new();

    // Shared by the "Nouveau widget > Carte graphique" creation submenu
    // (Sort.cs) and "Changer de GPU" on an existing widget below.
    private IEnumerable<MenuItem> BuildGpuMenuItems(Action<string> onSelected)
    {
        _hardwareMonitorService.EnsureStarted();

        foreach (var (id, name) in _hardwareMonitorService.GetGpus())
        {
            var menuItem = new MenuItem { Header = name };
            menuItem.Click += (_, _) => onSelected(id);
            yield return menuItem;
        }
    }

    private void AddGpuWidget(string gpuId)
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsGpuWidget = true,
            GpuIdentifier = gpuId,
            DisplayName = Strings.GpuWidgetMenuItem,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChangeGpu(LadaItem item, string newGpuId)
    {
        item.GpuIdentifier = newGpuId;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderGpuWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = GpuWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var accent = ItemLabelAccentOverride();

        var titleLabel = new TextBlock
        {
            Text = Strings.GpuWidgetMenuItem,
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
                Width = GpuBarWidth,
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
            Width = GpuBarWidth,
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
        stack.ContextMenu = BuildGpuWidgetContextMenu(item, stack);

        IconGrid.Children.Add(stack);

        StartGpuUpdates(item, usageLabel, barFill, temperatureLabel, sparkline, frequencyLabel);
    }

    private ContextMenu BuildGpuWidgetContextMenu(LadaItem item, StackPanel stack)
    {
        var menu = new ContextMenu();

        var changeGpuSubmenu = new MenuItem { Header = Strings.ChangeGpu };
        foreach (var gpuItem in BuildGpuMenuItems(id => ChangeGpu(item, id)))
        {
            changeGpuSubmenu.Items.Add(gpuItem);
        }
        menu.Items.Add(changeGpuSubmenu);

        var detailedViewItem = new MenuItem
        {
            Header = Strings.DetailedViewMenuItem,
            IsCheckable = true,
            IsChecked = item.ShowDetailedView
        };
        detailedViewItem.Click += (_, _) =>
        {
            StopGpuUpdates(item);
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

    private void StartGpuUpdates(LadaItem item, TextBlock usageLabel, Border barFill, TextBlock temperatureLabel, Polyline? sparkline, TextBlock? frequencyLabel)
    {
        var samples = new List<float>();

        void Tick()
        {
            try
            {
                var load = _hardwareMonitorService.GetGpuLoad(item.GpuIdentifier);
                if (load is not { } loadValue)
                {
                    usageLabel.Text = Strings.WidgetUnavailable;
                    barFill.Width = 0;
                    temperatureLabel.Text = "";
                    return;
                }

                usageLabel.Text = Strings.UsagePercent(loadValue.ToString("0"));
                barFill.Width = Math.Clamp(loadValue / 100.0, 0, 1) * GpuBarWidth;

                var temperature = _hardwareMonitorService.GetGpuTemperature(item.GpuIdentifier);
                temperatureLabel.Text = temperature is { } tempValue
                    ? Strings.TemperatureCelsius(tempValue.ToString("0"))
                    : Strings.WidgetUnavailable;

                if (sparkline is not null)
                {
                    samples.Add(loadValue);
                    if (samples.Count > SparklineSampleCount)
                        samples.RemoveAt(0);
                    UpdateSparkline(sparkline, samples, GpuBarWidth, SparklineHeight, 100);
                }

                if (frequencyLabel is not null)
                {
                    var frequency = _hardwareMonitorService.GetGpuFrequency(item.GpuIdentifier);
                    frequencyLabel.Text = frequency is { } freqValue
                        ? Strings.FrequencyGhz((freqValue / 1000).ToString("0.0"))
                        : Strings.WidgetUnavailable;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(nameof(StartGpuUpdates), ex);
                usageLabel.Text = Strings.WidgetUnavailable;
                barFill.Width = 0;
                temperatureLabel.Text = "";
            }
        }

        _hardwareMonitorService.EnsureStarted();
        Tick();

        void Handler() => Tick();
        _hardwareMonitorService.Updated += Handler;
        _gpuRefreshHandlers[item] = Handler;
    }

    private void StopGpuUpdates(LadaItem item)
    {
        if (_gpuRefreshHandlers.TryGetValue(item, out var handler))
        {
            _hardwareMonitorService.Updated -= handler;
            _gpuRefreshHandlers.Remove(item);
        }
    }

    private void DisposeAllGpuUpdates()
    {
        foreach (var item in new List<LadaItem>(_gpuRefreshHandlers.Keys))
        {
            StopGpuUpdates(item);
        }
    }
}
