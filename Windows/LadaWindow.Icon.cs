using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private string _iconId = "table";
    private string _iconColor = "#5B8DEF";
    private IDisposable? _iconPickerOutsideClickWatch;
    private ContextMenu? _savedColorContextMenu;
    // Shared by every swatch in the currently-open icon picker popup (see
    // PopulatePicker) so that dragging the color sliders can update every
    // swatch live by mutating this one brush's Color, the same way the
    // title bar's own icon glyph already updates live -- rebuilding the
    // whole popup on every slider tick (ApplyIconColor deliberately avoids
    // that) would tear down the controls the user is actively dragging.
    private SolidColorBrush? _iconSwatchFillBrush;

    // See OutsideClickWatcher: WS_EX_NOACTIVATE means a click on a
    // genuinely separate window (another lada, the desktop, another app)
    // never reaches LadaWindow_PreviewMouseLeftButtonDown below, since that
    // handler only ever sees clicks routed to this window. This covers the
    // gap the same way the two ContextMenus do (see LadaWindow.Native.cs).
    private void InitializeIconPickerOutsideClickAutoClose()
    {
        PickerPopup.Opened += (_, _) =>
        {
            _iconPickerOutsideClickWatch = OutsideClickWatcher.Watch(
                IsPointInsideIconPickerSurface,
                () => Dispatcher.BeginInvoke(() => PickerPopup.IsOpen = false));
        };

        PickerPopup.Closed += (_, _) =>
        {
            if (_savedColorContextMenu is not null)
                _savedColorContextMenu.IsOpen = false;
            _savedColorContextMenu = null;
            _iconPickerOutsideClickWatch?.Dispose();
            _iconPickerOutsideClickWatch = null;
        };
    }

    private bool IsPointInsideIconPickerSurface(int x, int y)
    {
        if (PresentationSource.FromVisual(PickerPopup.Child) is HwndSource pickerSource
            && OutsideClickWatcher.IsPointInsideWindow(pickerSource.Handle, x, y))
        {
            return true;
        }

        return _savedColorContextMenu is { IsOpen: true } menu
            && PresentationSource.FromVisual(menu) is HwndSource menuSource
            && OutsideClickWatcher.IsPointInsideWindow(menuSource.Handle, x, y);
    }

    private void UpdateIconButtonVisual()
    {
        var entry = IconLibrary.Icons.FirstOrDefault(i => i.Id == _iconId) ?? IconLibrary.Icons[0];
        IconButtonPath.Data = Geometry.Parse(entry.PathData);
        IconButtonPath.Fill = IconGlyphFillBrush();
        IconButton.ToolTip = Strings.ChooseIconTooltip;

        // Every icon-collection swatch's Fill points at this same brush
        // instance (see PopulatePicker) -- mutating its Color here updates
        // all of them live, immediately, with no visual-tree rebuild.
        if (_iconSwatchFillBrush is not null)
        {
            _iconSwatchFillBrush.Color = (Color)ColorConverter.ConvertFromString(_iconColor)!;
        }
    }

    // In Modernism the title-bar icon glyph sits on top of the per-lada
    // accent color (which paints the title bar itself, see
    // LadaWindow.Theme.cs), so it needs black-or-white contrast rather than
    // a fixed color. In Midnight it tints the glyph itself, as before.
    private Brush IconGlyphFillBrush() =>
        _themeManager?.Current == AppTheme.Modernism
            ? ColorContrast.ForegroundBrush(_iconColor)
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!);

    private void IconButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PickerPopup.IsOpen)
        {
            PopulatePicker();
        }
        PickerPopup.IsOpen = !PickerPopup.IsOpen;
        e.Handled = true;
    }

    private void PopulatePicker()
    {
        // Picker swatches always sit on the popup's own background (never
        // the title bar), so — unlike the title button's own glyph — they
        // always show the actual per-lada color here, in every theme.
        // A fresh brush each time the popup is (re)built, matching the old
        // Path elements' fresh construction below -- UpdateIconButtonVisual
        // then mutates this same instance in place for live slider updates.
        _iconSwatchFillBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_iconColor)!);
        var swatchFill = _iconSwatchFillBrush;

        IconPickerGrid.Children.Clear();
        foreach (var entry in IconLibrary.Icons)
        {
            var swatch = new Border
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(2),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };
            var path = new Path
            {
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                Data = Geometry.Parse(entry.PathData),
                Fill = swatchFill
            };
            swatch.Child = path;
            swatch.MouseLeftButtonDown += (_, e) =>
            {
                _iconId = entry.Id;
                UpdateIconButtonVisual();
                PopulatePicker();
                LayoutChanged?.Invoke(this, System.EventArgs.Empty);
                e.Handled = true;
            };
            IconPickerGrid.Children.Add(swatch);
        }

        ColorPickerRow.Children.Clear();
        foreach (var hex in ColorPalette.ForTheme(_themeManager?.Current ?? AppTheme.Midnight))
        {
            var dot = new Border
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!)
            };
            dot.MouseLeftButtonDown += (_, e) =>
            {
                ApplyIconColor(hex);
                PopulatePicker();
                e.Handled = true;
            };
            ColorPickerRow.Children.Add(dot);
        }

        SavedColorsRow.Children.Clear();
        var savedColors = _customColorPaletteService?.Colors ?? Array.Empty<string>();
        foreach (var hex in savedColors)
        {
            var dot = new Border
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!)
            };
            dot.MouseLeftButtonDown += (_, e) =>
            {
                ApplyIconColor(hex);
                PopulatePicker();
                e.Handled = true;
            };

            var menu = new ContextMenu();
            var deleteItem = new MenuItem { Header = Strings.DeleteCustomColor };
            deleteItem.Click += (_, _) =>
            {
                menu.IsOpen = false;
                if (_customColorPaletteService?.Remove(hex) == true)
                    PopulatePicker();
            };
            menu.Items.Add(deleteItem);
            menu.Opened += (_, _) => _savedColorContextMenu = menu;
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_savedColorContextMenu, menu))
                    _savedColorContextMenu = null;
            };
            AttachOutsideClickAutoClose(menu);
            dot.ContextMenu = menu;
            SavedColorsRow.Children.Add(dot);
        }

        var hasSavedColors = savedColors.Count > 0;
        SavedColorsDivider.Visibility = hasSavedColors ? Visibility.Visible : Visibility.Collapsed;
        SavedColorsRow.Visibility = hasSavedColors ? Visibility.Visible : Visibility.Collapsed;

        RefreshCustomColorPicker();
    }

    // PickerPopup uses StaysOpen="True" plus this manual dismiss instead of
    // StaysOpen="False": a StaysOpen="False" popup opened directly from the
    // icon button's own MouseLeftButtonDown closes itself immediately on
    // that same click's MouseUp (documented WPF behavior — the popup steals
    // PreviewMouseDown as soon as it opens, and the trailing mouse-up of the
    // opening click reads as "outside"). Closing manually here avoids that.
    //
    // Only catches clicks inside this window; a click entirely outside the
    // lada (the desktop, another app) is covered separately by
    // InitializeIconPickerOutsideClickAutoClose above.
    private void LadaWindow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!PickerPopup.IsOpen)
            return;

        if (e.OriginalSource is DependencyObject source &&
            (IsDescendantOf(source, IconButton) || (PickerPopup.Child is DependencyObject popupChild && IsDescendantOf(source, popupChild))))
        {
            return;
        }

        PickerPopup.IsOpen = false;
    }

    private static bool IsDescendantOf(DependencyObject descendant, DependencyObject ancestor)
    {
        var current = descendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
