using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private ThemeManager? _themeManager;
    private WeatherService? _weatherService;
    private Action? _weatherUpdatedHandler;
    private Path? _resizeChevron;

    private void InitializeForecastWeather(WeatherService weatherService)
    {
        _weatherService = weatherService;
        ForecastRainLayer.SetWeather(weatherService.Current);
        _weatherUpdatedHandler = () => Dispatcher.BeginInvoke(() =>
            ForecastRainLayer.SetWeather(_weatherService?.Current));
        weatherService.Updated += _weatherUpdatedHandler;
        Closed += (_, _) =>
        {
            if (_weatherService is not null && _weatherUpdatedHandler is not null)
                _weatherService.Updated -= _weatherUpdatedHandler;
        };
    }

    private void InitializeTheme(ThemeManager themeManager)
    {
        _themeManager = themeManager;
        _themeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => _themeManager.ThemeChanged -= OnThemeChanged;

        ResizeThumb.ApplyTemplate();
        _resizeChevron = ResizeThumb.Template.FindName("ResizeChevronPath", ResizeThumb) as Path;

        ApplyThemeColors();
    }

    private void OnThemeChanged()
    {
        if (_themeManager?.Current == AppTheme.Anderson && _andersonColorSyncManager?.Enabled == true)
        {
            _iconColor = _andersonColorSyncManager.Color;
        }

        // Viewport2DVisual3D hosts its Visual outside the normal 2D resource
        // inheritance path. While Perspective Tilt is active, a swapped
        // application ResourceDictionary therefore does not invalidate all
        // DynamicResource expressions inside TiltRootContent: the manager's
        // Current theme changes, but parts of the card can retain the old
        // background/font/geometry. Briefly reattaching the complete panel
        // to RootHost makes WPF resolve every resource against the new theme
        // in one synchronous layout tree, then it can safely return to the
        // same 3D host before the next rendered frame.
        var restorePerspectiveHosting = _tiltHostedIn3D;
        if (restorePerspectiveHosting)
        {
            SetPerspectiveTiltHosting(false);
        }

        try
        {
            ApplyThemeColors();
            UpdateAppearanceCustomization();
            UpdateIconButtonVisual();
            UpdateMainBorderClip();
            RefreshDynamicContent();
        }
        finally
        {
            if (restorePerspectiveHosting)
            {
                SetPerspectiveTiltHosting(true);
                UpdateTiltGeometry();
                UpdatePerspectiveTilt();
            }
        }
    }

    // Item icons, tab headers, and selection tint are all built in code
    // (not bound via DynamicResource), so the simplest way to guarantee none
    // of them holds a stale pre-swap brush or string is to just redraw them.
    // Shared by theme changes and language changes (LadaWindow.Localization.cs).
    //
    // The icon-grid re-render loop below and UpdateTabContentModeVisuals's
    // own to-do/memo render calls are both always safe to run regardless of
    // the active tab's actual mode: whichever one doesn't apply just acts on
    // an empty/hidden surface. Without the latter call, switching theme or
    // picking a new accent color while already viewing a to-do list or memo
    // left its text on the stale pre-change color until the next tab switch.
    private void RefreshDynamicContent()
    {
        DisposeAllDrawerWatchers();
        DisposeAllClockTimers();
        DisposeAllDiskTimers();
        DisposeAllBatteryTimers();
        DisposeAllMemoryTimers();
        DisposeAllCpuUpdates();
        DisposeAllGpuUpdates();
        DisposeAllNetworkUpdates();
        DisposeAllTimerWidgetTimers();
        RenderAllItems();
        ApplySelectionVisuals();
        UpdateTabContentModeVisuals();
        RenderTabStrip();
        EnsureContentFits();
    }

    // The lada's own title bar and resize chevron aren't themed via the
    // swappable resource dictionary: in Modernism they instead pick up this
    // specific lada's own icon color (per the design), so they need explicit
    // per-instance handling rather than a DynamicResource lookup.
    private void ApplyThemeColors()
    {
        // Anderson's whole chrome is driven by the color of this specific
        // lada. Keep those overrides at Window scope so detached popups such
        // as ContextMenu can resolve the same color through their placement
        // target as the in-window picker does. Other themes must explicitly
        // release the overrides when the user switches away from Anderson.
        if (_themeManager?.Current == AppTheme.Anderson)
        {
            var activeColor = (Color)ColorConverter.ConvertFromString(_iconColor)!;
            var activeBrush = new SolidColorBrush(activeColor);
            Resources["TitleTextBrush"] = activeBrush;
            Resources["SecondaryTextBrush"] = activeBrush;
            Resources["LadaBorderBrush"] = activeBrush;
            Resources["ColorPickerThumbBackgroundBrush"] = Brushes.Black;
            ColorHexBox.Background = Brushes.Black;
            SaveColorButton.Background = Brushes.Black;
        }
        else
        {
            Resources.Remove("TitleTextBrush");
            Resources.Remove("SecondaryTextBrush");
            Resources.Remove("LadaBorderBrush");
            Resources["ColorPickerThumbBackgroundBrush"] =
                System.Windows.Application.Current.TryFindResource("TitleTextBrush") as Brush ?? Brushes.White;
            ColorHexBox.Background = Brushes.Transparent;
            SaveColorButton.Background = Brushes.Transparent;
        }

        ForecastRainLayer.Visibility = _themeManager?.Current == AppTheme.Forecast
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        HowardHudLayer.Visibility = _themeManager?.Current == AppTheme.Howard
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

        if (_themeManager?.Current == AppTheme.Modernism)
        {
            var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!);
            var readable = ColorContrast.ForegroundBrush(_iconColor);
            TitleBar.Background = accent;
            TitleTextBlock.Foreground = readable;
            TitleTextBox.Foreground = readable;
            // These four aren't part of Modernism's own per-lada look (it
            // only recolors the title bar itself), but Anderson does set
            // them as local values -- without an explicit reset here they'd
            // keep showing Anderson's last accent color forever after
            // switching a lada from Anderson to Modernism, since nothing
            // else would ever touch them again.
            MainBorder.SetResourceReference(Border.BorderBrushProperty, "LadaBorderBrush");
            TitleTabSeparator.SetResourceReference(Border.BackgroundProperty, "LadaBorderBrush");
            IconPickerBorder.SetResourceReference(Border.BorderBrushProperty, "LadaBorderBrush");
            IconPickerDivider.SetResourceReference(Border.BackgroundProperty, "LadaBorderBrush");
            CustomColorDivider.SetResourceReference(Border.BackgroundProperty, "LadaBorderBrush");
            if (_resizeChevron is not null)
            {
                _resizeChevron.Stroke = accent;
            }
        }
        else if (_themeManager?.Current == AppTheme.Anderson)
        {
            var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!);
            TitleBar.Background = Brushes.Transparent;
            MainBorder.BorderBrush = accent;
            TitleTabSeparator.Background = accent;
            TitleTextBlock.Foreground = accent;
            TitleTextBox.Foreground = accent;
            IconPickerBorder.BorderBrush = accent;
            IconPickerDivider.Background = accent;
            CustomColorDivider.Background = accent;
            if (_resizeChevron is not null)
            {
                _resizeChevron.Stroke = accent;
            }
        }
        else if (_themeManager?.Current == AppTheme.Howard)
        {
            var accentColor = (Color)ColorConverter.ConvertFromString(_iconColor)!;
            var accent = new SolidColorBrush(accentColor);
            HowardHudLayer.AccentColor = accentColor;
            TitleBar.Background = Brushes.Transparent;
            MainBorder.BorderBrush = accent;
            TitleTabSeparator.Background = accent;
            TitleTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TitleTextBrush");
            TitleTextBox.SetResourceReference(TextBox.ForegroundProperty, "TitleTextBrush");
            IconPickerBorder.BorderBrush = accent;
            IconPickerDivider.Background = accent;
            CustomColorDivider.Background = accent;
            if (_resizeChevron is not null)
            {
                _resizeChevron.Stroke = accent;
            }
        }
        else
        {
            TitleBar.Background = Brushes.Transparent;
            MainBorder.SetResourceReference(Border.BorderBrushProperty, "LadaBorderBrush");
            TitleTabSeparator.SetResourceReference(Border.BackgroundProperty, "LadaBorderBrush");
            TitleTextBlock.ClearValue(TextBlock.ForegroundProperty);
            TitleTextBox.SetResourceReference(TextBox.ForegroundProperty, "TitleTextBrush");
            IconPickerBorder.SetResourceReference(Border.BorderBrushProperty, "LadaBorderBrush");
            IconPickerDivider.SetResourceReference(Border.BackgroundProperty, "LadaBorderBrush");
            CustomColorDivider.SetResourceReference(Border.BackgroundProperty, "LadaBorderBrush");
            _resizeChevron?.SetResourceReference(Shape.StrokeProperty, "SecondaryTextBrush");
        }

        ApplyMenuAccentColors();
        UpdateAndersonColorSyncButton();
        UpdateHudGlow();
        // A theme/accent refresh changes only the tint. Reordering native
        // windows here would put the color picker's Popup HWND behind the
        // Lada while a slider is being dragged.
        UpdateBackgroundBlur(preserveZOrder: true);
    }

    // Every ContextMenu in this window (base sort/new-widget menu, tab
    // headers, widget menus, ...) is styled via MenuStyles.xaml, which reads
    // AccentBrush/SelectedBackgroundBrush as DynamicResource -- normally the
    // theme's own fixed color (blue in Midnight, green in Anderson) shared
    // app-wide. A ContextMenu's logical parent becomes its PlacementTarget
    // once opened, so a DynamicResource lookup still bubbles up through this
    // window's own Resources first: overriding the two keys here locally
    // makes every menu's selection highlight and radio glyph follow this
    // specific lada's own chosen color instead, in every theme -- otherwise
    // a lada whose accent was changed away from the default blue would still
    // show the old fixed color the moment its context menu opened.
    private void ApplyMenuAccentColors()
    {
        var accent = (Color)ColorConverter.ConvertFromString(_iconColor)!;
        Resources["AccentBrush"] = new SolidColorBrush(accent);
        Resources["SelectedBackgroundBrush"] = new SolidColorBrush(accent) { Opacity = 0.3 };
        Resources["SelectionMarqueeFillBrush"] = new SolidColorBrush(accent) { Opacity = 0.18 };
    }

    // Item labels use IconLabelStyle's DynamicResource-bound SecondaryTextBrush
    // in Midnight/Modernism/Forecast. In Anderson and Howard they follow
    // this specific lada's own accent instead, same as the surrounding HUD
    // chrome -- called from RenderItem (DragDrop.cs)
    // and BuildDrawerChildVisual (Drawer.cs) to override the Style's default
    // per label, since a Style is shared app-wide and can't vary per lada by
    // itself. Returns null outside those themes so callers can leave the Style's
    // own DynamicResource value in place instead of resolving it themselves.
    private Brush? ItemLabelAccentOverride() =>
        _themeManager?.Current is AppTheme.Anderson or AppTheme.Howard
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!)
            : null;

}
