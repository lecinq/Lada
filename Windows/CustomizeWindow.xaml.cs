using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Lada.Resources;
using Lada.Services;
using Forms = System.Windows.Forms;

namespace Lada.Windows;

public partial class CustomizeWindow : Window
{
    private readonly AppearanceCustomizationManager _manager;

    public CustomizeWindow(AppearanceCustomizationManager manager)
    {
        InitializeComponent();
        _manager = manager;

        Title = Strings.CustomizeMenuItem;
        TitleLabel.Text = Strings.CustomizeMenuItem;
        BrightnessLabel.Text = Strings.CustomizeBrightness;
        BrightnessDescription.Text = Strings.CustomizeBrightnessDescription;
        BackgroundColorLabel.Text = Strings.CustomizeBackgroundColor;
        BackgroundColorDescription.Text = Strings.CustomizeBackgroundColorDescription;
        ResetBackgroundColorText.Text = Strings.CustomizeResetBackgroundColor;
        BrightnessSlider.Value = _manager.BrightnessPercent;
        UpdateBrightnessValue();
        UpdateBackgroundColorPreview();
        BrightnessSlider.ValueChanged += (_, _) =>
        {
            _manager.ApplyBrightness(BrightnessSlider.Value);
            UpdateBrightnessValue();
        };
    }

    private void UpdateBrightnessValue() =>
        BrightnessValueLabel.Text = $"{Math.Round(BrightnessSlider.Value):0} %";

    private void UpdateBackgroundColorPreview()
    {
        var hex = _manager.BackgroundColorHex;
        var color = hex is not null
            ? (Color)ColorConverter.ConvertFromString(hex)!
            : (Application.Current.TryFindResource("LadaBackgroundBrush") as SolidColorBrush)?.Color
                ?? Colors.Transparent;
        color.A = 255;
        BackgroundColorSwatch.Background = new SolidColorBrush(color);
        BackgroundColorValue.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        ResetBackgroundColorButton.Opacity = hex is null ? 0.5 : 1;
    }

    private void BackgroundColorButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var preview = (BackgroundColorSwatch.Background as SolidColorBrush)?.Color ?? Colors.Black;
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(preview.R, preview.G, preview.B)
        };

        var owner = new NativeDialogOwner(new WindowInteropHelper(this).Handle);
        if (dialog.ShowDialog(owner) == Forms.DialogResult.OK)
        {
            _manager.ApplyBackgroundColor(
                $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
            UpdateBackgroundColorPreview();
        }

        e.Handled = true;
    }

    private void ResetBackgroundColorButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _manager.ResetBackgroundColor();
        UpdateBackgroundColorPreview();
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Close();
        e.Handled = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private sealed class NativeDialogOwner(IntPtr handle) : Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
