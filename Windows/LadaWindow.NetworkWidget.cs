using System;
using System.Collections.Generic;
using System.Linq;
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
    private const double NetworkWidgetWidth = 90;
    private const double NetworkBarWidth = 70;

    private readonly Dictionary<LadaItem, Action> _networkRefreshHandlers = new();

    // Shared by the "Nouveau widget > Réseau" creation submenu (Sort.cs)
    // and "Changer d'interface" on an existing widget below.
    private IEnumerable<MenuItem> BuildNetworkAdapterMenuItems(Action<string> onSelected)
    {
        _hardwareMonitorService.EnsureStarted();

        foreach (var (id, name) in _hardwareMonitorService.GetNetworkAdapters())
        {
            var menuItem = new MenuItem { Header = name };
            menuItem.Click += (_, _) => onSelected(id);
            yield return menuItem;
        }
    }

    private void AddNetworkWidget(string adapterId)
    {
        var (column, row) = FindNextFreeCell(_items);
        var item = new LadaItem
        {
            IsNetworkWidget = true,
            NetworkAdapterIdentifier = adapterId,
            DisplayName = Strings.NetworkWidgetMenuItem,
            Column = column,
            Row = row
        };

        _items.Add(item);
        RenderSingleItem(item);
        EnsureContentFits();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChangeNetworkAdapter(LadaItem item, string newAdapterId)
    {
        item.NetworkAdapterIdentifier = newAdapterId;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderNetworkWidget(LadaItem item)
    {
        var stack = new StackPanel
        {
            Width = NetworkWidgetWidth,
            Margin = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.Hand,
            Tag = item
        };

        var accent = ItemLabelAccentOverride();

        var titleLabel = new TextBlock
        {
            Text = Strings.NetworkWidgetMenuItem,
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
                Width = NetworkBarWidth,
                Height = SparklineHeight,
                Stroke = accent ?? (Brush)FindResource("AccentBrush"),
                StrokeThickness = 1.5,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(sparkline);
        }

        var downloadLabel = new TextBlock
        {
            Text = "…",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = accent ?? (Brush)FindResource("TitleTextBrush"),
            Margin = new Thickness(0, 0, 0, 2)
        };
        stack.Children.Add(downloadLabel);

        var uploadLabel = new TextBlock
        {
            Text = "…",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = WidgetDimmedAccentBrush(accent)
        };
        stack.Children.Add(uploadLabel);

        AttachItemDragSource(stack, stack, item);
        stack.ContextMenu = BuildNetworkWidgetContextMenu(item, stack);

        IconGrid.Children.Add(stack);

        StartNetworkUpdates(item, downloadLabel, uploadLabel, sparkline);
    }

    private ContextMenu BuildNetworkWidgetContextMenu(LadaItem item, StackPanel stack)
    {
        var menu = new ContextMenu();

        var changeAdapterSubmenu = new MenuItem { Header = Strings.ChangeNetworkAdapter };
        foreach (var adapterItem in BuildNetworkAdapterMenuItems(id => ChangeNetworkAdapter(item, id)))
        {
            changeAdapterSubmenu.Items.Add(adapterItem);
        }
        menu.Items.Add(changeAdapterSubmenu);

        var detailedViewItem = new MenuItem
        {
            Header = Strings.DetailedViewMenuItem,
            IsCheckable = true,
            IsChecked = item.ShowDetailedView
        };
        detailedViewItem.Click += (_, _) =>
        {
            StopNetworkUpdates(item);
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

    // The sparkline tracks download speed only (the side people actually
    // watch day to day); auto-scaled to the largest sample currently in the
    // window since, unlike CPU/GPU usage, there's no natural 0-100 ceiling.
    private void StartNetworkUpdates(LadaItem item, TextBlock downloadLabel, TextBlock uploadLabel, Polyline? sparkline)
    {
        var samples = new List<float>();

        void Tick()
        {
            try
            {
                var download = _hardwareMonitorService.GetNetworkDownloadSpeed(item.NetworkAdapterIdentifier);
                var upload = _hardwareMonitorService.GetNetworkUploadSpeed(item.NetworkAdapterIdentifier);

                if (download is not { } downloadValue || upload is not { } uploadValue)
                {
                    downloadLabel.Text = Strings.WidgetUnavailable;
                    uploadLabel.Text = "";
                    return;
                }

                downloadLabel.Text = "↓ " + Strings.NetworkSpeed(FileSizeFormatter.FormatBytes((long)downloadValue));
                uploadLabel.Text = "↑ " + Strings.NetworkSpeed(FileSizeFormatter.FormatBytes((long)uploadValue));

                if (sparkline is not null)
                {
                    samples.Add(downloadValue);
                    if (samples.Count > SparklineSampleCount)
                        samples.RemoveAt(0);
                    UpdateSparkline(sparkline, samples, NetworkBarWidth, SparklineHeight, samples.Count > 0 ? samples.Max() : 0);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(nameof(StartNetworkUpdates), ex);
                downloadLabel.Text = Strings.WidgetUnavailable;
                uploadLabel.Text = "";
            }
        }

        _hardwareMonitorService.EnsureStarted();
        Tick();

        void Handler() => Tick();
        _hardwareMonitorService.Updated += Handler;
        _networkRefreshHandlers[item] = Handler;
    }

    private void StopNetworkUpdates(LadaItem item)
    {
        if (_networkRefreshHandlers.TryGetValue(item, out var handler))
        {
            _hardwareMonitorService.Updated -= handler;
            _networkRefreshHandlers.Remove(item);
        }
    }

    private void DisposeAllNetworkUpdates()
    {
        foreach (var item in new List<LadaItem>(_networkRefreshHandlers.Keys))
        {
            StopNetworkUpdates(item);
        }
    }
}
