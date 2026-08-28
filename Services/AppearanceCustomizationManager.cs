using System;

namespace Lada.Services;

public sealed class AppearanceCustomizationManager
{
    public const double DefaultBrightnessPercent = 100;
    public const double MinimumBrightnessPercent = 0;
    public const double MaximumBrightnessPercent = 200;

    public double BrightnessPercent { get; private set; } = DefaultBrightnessPercent;
    public string? BackgroundColorHex { get; private set; }

    public event Action? Changed;

    public void ApplyBrightness(double percent)
    {
        var normalized = Math.Round(
            Math.Clamp(percent, MinimumBrightnessPercent, MaximumBrightnessPercent));
        if (Math.Abs(BrightnessPercent - normalized) < 0.1)
            return;

        BrightnessPercent = normalized;
        Changed?.Invoke();
    }

    public void ApplyBackgroundColor(string? hex)
    {
        var normalized = NormalizeHexColor(hex);
        if (string.Equals(BackgroundColorHex, normalized, StringComparison.Ordinal))
            return;

        BackgroundColorHex = normalized;
        Changed?.Invoke();
    }

    public void ResetBackgroundColor() => ApplyBackgroundColor(null);

    private static string? NormalizeHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;

        var value = hex.Trim();
        if (!value.StartsWith('#'))
            value = "#" + value;

        if (value.Length != 7)
            throw new ArgumentException("Background color must use #RRGGBB format.", nameof(hex));

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
                throw new ArgumentException("Background color must use #RRGGBB format.", nameof(hex));
        }

        return value.ToUpperInvariant();
    }
}
