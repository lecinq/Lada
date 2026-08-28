using System.Drawing;
using Lada.Models;

namespace Lada.Resources;

// A second, deliberately duplicated source of truth for each theme's
// surface colors/geometry (background/border/text/hover/accent, corner
// radius, border thickness), transcribed from the same values in
// Styles/Theme.xaml / ThemeModernism.xaml / ThemeAnderson.xaml /
// ThemeForecast.xaml / ThemeHoward.xaml. WinForms
// can't read a WPF ResourceDictionary directly, so the tray icon's
// ContextMenuStrip (System.Drawing types) needs its own lookup -- matching
// how Resources/ColorPalette.cs's icon accent hex strings are already a
// separate source of truth from the same theme XAML files today.
public static class ThemeSurfaceColors
{
    public static (Color Background, Color Border, Color Text, Color Hover, Color Accent, int CornerRadius, int BorderThickness) ForTheme(AppTheme theme) => theme switch
    {
        AppTheme.Modernism => (
            Background: ColorTranslator.FromHtml("#FFFFFF"),
            Border: ColorTranslator.FromHtml("#000000"),
            Text: ColorTranslator.FromHtml("#111111"),
            Hover: ColorTranslator.FromHtml("#38B6FF"),
            Accent: ColorTranslator.FromHtml("#38B6FF"),
            CornerRadius: 0,
            BorderThickness: 2),
        AppTheme.Anderson => (
            Background: ColorTranslator.FromHtml("#000000"),
            Border: ColorTranslator.FromHtml("#33FF33"),
            Text: ColorTranslator.FromHtml("#33FF33"),
            Hover: ColorTranslator.FromHtml("#1F8F1F"),
            Accent: ColorTranslator.FromHtml("#33FF33"),
            CornerRadius: 0,
            BorderThickness: 1),
        AppTheme.Forecast => (
            Background: ColorTranslator.FromHtml("#09131F"),
            Border: ColorTranslator.FromHtml("#294B66"),
            Text: ColorTranslator.FromHtml("#EAF5FF"),
            Hover: ColorTranslator.FromHtml("#62B7FF"),
            Accent: ColorTranslator.FromHtml("#62B7FF"),
            CornerRadius: 12,
            BorderThickness: 1),
        // WinForms tray menus cannot use the HUD's translucent WPF surface;
        // keep the same near-black hue as an opaque menu for legibility.
        AppTheme.Howard => (
            Background: ColorTranslator.FromHtml("#061018"),
            Border: ColorTranslator.FromHtml("#00E5FF"),
            Text: ColorTranslator.FromHtml("#F4FCFF"),
            Hover: ColorTranslator.FromHtml("#123F4A"),
            Accent: ColorTranslator.FromHtml("#00E5FF"),
            CornerRadius: 8,
            BorderThickness: 1),
        // Midnight uses the CSS/X11 navy value requested for its surface.
        _ => (
            Background: ColorTranslator.FromHtml("#000080"),
            Border: ColorTranslator.FromHtml("#2A2F3A"),
            Text: ColorTranslator.FromHtml("#E8EAED"),
            Hover: ColorTranslator.FromHtml("#5B8DEF"),
            Accent: ColorTranslator.FromHtml("#5B8DEF"),
            CornerRadius: 12,
            BorderThickness: 1)
    };
}
