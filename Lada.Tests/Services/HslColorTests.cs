using System;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class HslColorTests
{
    [Theory]
    [InlineData(0x5B, 0x8D, 0xEF)] // Midnight blue #5B8DEF
    [InlineData(0x33, 0xFF, 0x33)] // Anderson green #33FF33
    [InlineData(0xE8, 0x72, 0x0C)] // Modernism orange #E8720C
    [InlineData(0xFF, 0xFF, 0xFF)] // White
    [InlineData(0x00, 0x00, 0x00)] // Black
    [InlineData(0x11, 0x11, 0x11)] // Modernism black #111111
    public void RgbToHslToRgb_RoundTrips(byte r, byte g, byte b)
    {
        var (h, s, l) = HslColor.RgbToHsl(r, g, b);
        var (r2, g2, b2) = HslColor.HslToRgb(h, s, l);

        Assert.True(Math.Abs(r - r2) <= 1, $"R: {r} vs {r2}");
        Assert.True(Math.Abs(g - g2) <= 1, $"G: {g} vs {g2}");
        Assert.True(Math.Abs(b - b2) <= 1, $"B: {b} vs {b2}");
    }
}
