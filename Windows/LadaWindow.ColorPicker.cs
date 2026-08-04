using System;
using System.Windows.Controls;
using System.Windows.Media;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private bool _isSyncingColorPicker;

    // Wired once (these are static XAML elements, not rebuilt per popup
    // open like the preset swatches in PopulatePicker).
    private void InitializeCustomColorPicker()
    {
        ColorHexBox.ToolTip = Strings.CustomColorHexTooltip;

        HueSlider.ValueChanged += (_, _) => ApplyColorFromSliders();
        SaturationSlider.ValueChanged += (_, _) => ApplyColorFromSliders();
        LightnessSlider.ValueChanged += (_, _) => ApplyColorFromSliders();
        ColorHexBox.TextChanged += (_, _) => ApplyColorFromHexBox();
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
        _iconColor = hex;
        UpdateIconButtonVisual();
        ApplyThemeColors();
        RefreshDynamicContent();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
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
