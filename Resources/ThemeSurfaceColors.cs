using System.Drawing;
using Lada.Models;

namespace Lada.Resources;

// A second, deliberately duplicated source of truth for each theme's
// surface colors/geometry (background/border/text/hover/accent, corner
// radius, border thickness), transcribed from the same values in
// Styles/Theme.xaml / ThemeModernism.xaml / ThemeAnderson.xaml. WinForms
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
        // ColorTranslator.FromHtml doesn't accept 8-digit ARGB hex (Midnight's
        // LadaBackgroundBrush is #E00D0F14 for its ~88%-opacity background)
        // -- a WinForms ToolStripDropDown can't be alpha-blended like a WPF
        // AllowsTransparency window anyway, so this uses the opaque 6-digit
        // RGB portion instead of attempting a translucent tray menu.
        _ => (
            Background: ColorTranslator.FromHtml("#0D0F14"),
            Border: ColorTranslator.FromHtml("#2A2F3A"),
            Text: ColorTranslator.FromHtml("#E8EAED"),
            Hover: ColorTranslator.FromHtml("#5B8DEF"),
            Accent: ColorTranslator.FromHtml("#5B8DEF"),
            CornerRadius: 12,
            BorderThickness: 1)
    };
}
