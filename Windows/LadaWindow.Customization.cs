using System;
using System.Windows.Media;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private AppearanceCustomizationManager? _appearanceCustomizationManager;

    private void InitializeAppearanceCustomization(AppearanceCustomizationManager manager)
    {
        _appearanceCustomizationManager = manager;
        _appearanceCustomizationManager.Changed += UpdateAppearanceCustomization;
        Closed += (_, _) =>
            _appearanceCustomizationManager.Changed -= UpdateAppearanceCustomization;
        UpdateAppearanceCustomization();
    }

    private void UpdateAppearanceCustomization()
    {
        if (_appearanceCustomizationManager is null)
            return;

        if (_appearanceCustomizationManager.BackgroundColorHex is string customHex
            && System.Windows.Application.Current.TryFindResource("LadaBackgroundBrush") is SolidColorBrush themeBackground)
        {
            var customColor = (Color)ColorConverter.ConvertFromString(customHex)!;
            customColor.A = themeBackground.Color.A;
            Resources["LadaBackgroundBrush"] = new SolidColorBrush(customColor);
        }
        else
        {
            Resources.Remove("LadaBackgroundBrush");
        }

        var offset = _appearanceCustomizationManager.BrightnessPercent
            - AppearanceCustomizationManager.DefaultBrightnessPercent;
        if (Math.Abs(offset) < 0.1)
        {
            AppearanceBrightnessOverlay.Background = Brushes.Transparent;
            AppearanceBrightnessOverlay.Opacity = 0;
            UpdateBackgroundBlur(preserveZOrder: true);
            return;
        }

        AppearanceBrightnessOverlay.Background = offset > 0 ? Brushes.White : Brushes.Black;
        AppearanceBrightnessOverlay.Opacity = Math.Min(Math.Abs(offset) / 100.0 * 0.72, 0.72);
        UpdateBackgroundBlur(preserveZOrder: true);
    }
}
