using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private bool _isSyncingColorPicker;
    private bool _isPublishingSynchronizedColor;
    private CustomColorPaletteService? _customColorPaletteService;
    private AndersonColorSyncManager? _andersonColorSyncManager;

    private void InitializeAndersonColorSync(AndersonColorSyncManager manager)
    {
        _andersonColorSyncManager = manager;
        manager.ColorChanged += OnSynchronizedAndersonColorChanged;
        manager.Changed += UpdateAndersonColorSyncButton;
        Closed += (_, _) =>
        {
            manager.ColorChanged -= OnSynchronizedAndersonColorChanged;
            manager.Changed -= UpdateAndersonColorSyncButton;
        };
    }

    // Wired once (these are static XAML elements, not rebuilt per popup
    // open like the preset swatches in PopulatePicker).
    private void InitializeCustomColorPicker()
    {
        ColorHexBox.ToolTip = Strings.CustomColorHexTooltip;
        SaveColorButton.ToolTip = Strings.SaveColorTooltip;
        ColorSyncButton.MouseLeftButtonDown += (_, e) =>
        {
            if (_themeManager?.Current == AppTheme.Anderson)
            {
                _andersonColorSyncManager?.Toggle(_iconColor);
            }
            else if (_getAllLadaWindows is not null)
            {
                // Outside Anderson this remains the original one-shot
                // action: copy the current lada's color to every active one.
                foreach (var lada in _getAllLadaWindows())
                    lada.ApplyIconColorCore(_iconColor);
            }
            e.Handled = true;
        };
        UpdateAndersonColorSyncButton();

        HueSlider.ValueChanged += (_, _) => ApplyColorFromSliders();
        SaturationSlider.ValueChanged += (_, _) => ApplyColorFromSliders();
        LightnessSlider.ValueChanged += (_, _) => ApplyColorFromSliders();
        ColorHexBox.TextChanged += (_, _) => ApplyColorFromHexBox();
        SaveColorButton.MouseLeftButtonDown += (_, e) =>
        {
            _customColorPaletteService?.Add(_iconColor);
            PopulatePicker();
            e.Handled = true;
        };

        AttachClickAnywhereToDrag(HueSlider);
        AttachClickAnywhereToDrag(SaturationSlider);
        AttachClickAnywhereToDrag(LightnessSlider);
    }

    // The custom ColorPickerSliderStyle (App.xaml) replaces the Track's
    // default RepeatButtons/Thumb chrome, which breaks WPF's built-in
    // IsMoveToPointEnabled "click then keep dragging in the same gesture"
    // handoff to the Thumb. Handling the mouse directly here sidesteps that
    // entirely: the slider always tracks the cursor for as long as the
    // button stays down, whether the drag started on the thumb or anywhere
    // else on the track.
    private static void AttachClickAnywhereToDrag(Slider slider)
    {
        void SetValueFromMouse(MouseEventArgs e)
        {
            var x = e.GetPosition(slider).X;
            var fraction = slider.ActualWidth > 0 ? Math.Clamp(x / slider.ActualWidth, 0, 1) : 0;
            slider.Value = slider.Minimum + fraction * (slider.Maximum - slider.Minimum);
        }

        slider.PreviewMouseLeftButtonDown += (_, e) =>
        {
            slider.CaptureMouse();
            SetValueFromMouse(e);
            e.Handled = true;
        };
        slider.PreviewMouseMove += (_, e) =>
        {
            if (slider.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                SetValueFromMouse(e);
            }
        };
        slider.PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (slider.IsMouseCaptured)
            {
                slider.ReleaseMouseCapture();
            }
        };
    }

    // Called from PopulatePicker (Icon.cs) whenever the popup opens or a
    // preset swatch is clicked: reflects the lada's current _iconColor into
    // the sliders/hex box without re-triggering their own change handlers
    // (the reentrancy guard below), and refreshes the Saturation/Lightness
    // gradient previews to match the current hue.
    private void RefreshCustomColorPicker()
    {
        _isSyncingColorPicker = true;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(_iconColor)!;
            var (h, s, l) = HslColor.RgbToHsl(color.R, color.G, color.B);
            HueSlider.Value = h;
            SaturationSlider.Value = s * 100;
            LightnessSlider.Value = l * 100;
            ColorHexBox.Text = _iconColor;
            UpdateColorSliderGradients(h, s);
        }
        finally
        {
            _isSyncingColorPicker = false;
        }
    }

    private void ApplyColorFromSliders()
    {
        if (_isSyncingColorPicker)
            return;

        var (r, g, b) = HslColor.HslToRgb(HueSlider.Value, SaturationSlider.Value / 100.0, LightnessSlider.Value / 100.0);
        var hex = $"#{r:X2}{g:X2}{b:X2}";

        _isSyncingColorPicker = true;
        try
        {
            ColorHexBox.Text = hex;
            UpdateColorSliderGradients(HueSlider.Value, SaturationSlider.Value / 100.0);
        }
        finally
        {
            _isSyncingColorPicker = false;
        }

        ApplyIconColor(hex);
    }

    private void ApplyColorFromHexBox()
    {
        if (_isSyncingColorPicker)
            return;

        Color color;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(ColorHexBox.Text)!;
        }
        catch
        {
            // Incomplete/invalid hex while typing (e.g. "#5B8" mid-edit) --
            // wait for it to become parseable again, no error shown.
            return;
        }

        var (h, s, l) = HslColor.RgbToHsl(color.R, color.G, color.B);

        _isSyncingColorPicker = true;
        try
        {
            HueSlider.Value = h;
            SaturationSlider.Value = s * 100;
            LightnessSlider.Value = l * 100;
            UpdateColorSliderGradients(h, s);
        }
        finally
        {
            _isSyncingColorPicker = false;
        }

        ApplyIconColor(ColorHexBox.Text);
    }

    // Same chain the 8 preset swatches use in PopulatePicker (Icon.cs),
    // minus PopulatePicker() itself -- rebuilding the whole popup mid-drag
    // or mid-keystroke would tear down the very controls the user is
    // actively interacting with.
    private void ApplyIconColor(string hex)
    {
        if (_themeManager?.Current == AppTheme.Anderson && _andersonColorSyncManager?.Enabled == true)
        {
            // Hue 360° is intentionally the right-hand endpoint even though
            // it represents the same RGB red as 0°. The manager publishes
            // synchronously to every lada, including this source window. If
            // the source then round-trips that RGB value back through HSL,
            // 360° is canonically decoded as 0° and the thumb appears to jump
            // from the far right to the far left. Mark the publisher so only
            // the other windows refresh their picker coordinates.
            _isPublishingSynchronizedColor = true;
            try
            {
                _andersonColorSyncManager.SetColor(hex);
            }
            finally
            {
                _isPublishingSynchronizedColor = false;
            }
            return;
        }

        ApplyIconColorCore(hex);
    }

    private void ApplyIconColorCore(string hex)
    {
        _iconColor = hex;
        UpdateIconButtonVisual();
        ApplyThemeColors();
        RefreshDynamicContent();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSynchronizedAndersonColorChanged(string hex)
    {
        if (_themeManager?.Current != AppTheme.Anderson)
            return;

        ApplyIconColorCore(hex);
        if (PickerPopup.IsOpen && !_isPublishingSynchronizedColor)
            RefreshCustomColorPicker();
    }

    private void UpdateAndersonColorSyncButton()
    {
        if (ColorSyncButton is null)
            return;

        ColorSyncButton.Visibility = System.Windows.Visibility.Visible;

        var isAnderson = _themeManager?.Current == AppTheme.Anderson;
        var synchronized = _andersonColorSyncManager?.Enabled != false;
        var icon = !isAnderson || synchronized
            ? IconLibrary.ShareColorAction
            : IconLibrary.IndependentColorsAction;
        ColorSyncIconPath.Data = Geometry.Parse(icon.PathData);
        ColorSyncButton.ToolTip = !isAnderson
            ? Strings.ShareColorWithAllLadasTooltip
            : synchronized
                ? Strings.AndersonColorsSynchronizedTooltip
                : Strings.AndersonColorsIndependentTooltip;
    }

    // Saturation's own gradient (grey -> current hue at full saturation)
    // and Lightness's own gradient (black -> current hue+saturation ->
    // white) both depend on the OTHER sliders' current values, so they're
    // recomputed here rather than being fixed in XAML -- a static gradient
    // would otherwise mislead (e.g. still showing a red preview while the
    // actual hue is blue).
    private void UpdateColorSliderGradients(double hue, double saturation)
    {
        var (satR, satG, satB) = HslColor.HslToRgb(hue, 1.0, 0.5);
        SaturationGradientStop.Color = Color.FromRgb(satR, satG, satB);

        var (lightR, lightG, lightB) = HslColor.HslToRgb(hue, saturation, 0.5);
        LightnessGradientMidStop.Color = Color.FromRgb(lightR, lightG, lightB);
    }
}
