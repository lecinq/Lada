using System.Windows.Media;

namespace Lada.Resources;

public static class ColorContrast
{
    // Same perceptual luminance rule originally used by Modernism's title
    // bar. Centralizing it keeps every colored control consistent.
    public static Brush ForegroundBrush(Color background)
    {
        var luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
        return luminance > 0.5 ? Brushes.Black : Brushes.White;
    }

    public static Brush ForegroundBrush(string hex) =>
        ForegroundBrush((Color)ColorConverter.ConvertFromString(hex)!);
}
