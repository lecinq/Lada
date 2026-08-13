using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Lada.Models;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private ThemeManager? _themeManager;
    private Path? _resizeChevron;

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
        ApplyThemeColors();
        UpdateIconButtonVisual();
        UpdateMainBorderClip();
        RefreshDynamicContent();
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
        if (_themeManager?.Current == AppTheme.Modernism)
        {
            var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!);
            var readable = ContrastBrush(_iconColor);
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
        UpdateHudGlow();
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
    }

    // Item labels use IconLabelStyle's DynamicResource-bound SecondaryTextBrush
    // in Midnight/Modernism (unchanged, theme-wide, not per-lada). In
    // Anderson they follow this specific lada's own accent instead, same as
    // the border/title/icon glyph -- called from RenderItem (DragDrop.cs)
    // and BuildDrawerChildVisual (Drawer.cs) to override the Style's default
    // per label, since a Style is shared app-wide and can't vary per lada by
    // itself. Returns null outside Anderson so callers can leave the Style's
    // own DynamicResource value in place instead of resolving it themselves.
    private Brush? ItemLabelAccentOverride() =>
        _themeManager?.Current == AppTheme.Anderson
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!)
            : null;

    // A title bar's background is an arbitrary per-lada color in Modernism
    // (whatever accent the user picked), so fixed black or white text/icon
    // would go illegible against roughly half the palette. Picking
    // black-or-white from the color's own perceptual luminance keeps title
    // text and the title icon glyph readable against any accent, including
    // ones added to the palette later.
    private static Brush ContrastBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex)!;
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luminance > 0.5 ? Brushes.Black : Brushes.White;
    }
}
