using System;
using System.Collections.Generic;
using System.Linq;

namespace Lada.Services;

// Holds colors the user has explicitly chosen to keep, shared app-wide
// across every lada (like ThemeManager/HoverFadeManager) rather than
// per-lada -- "save this color permanently" only makes sense as one
// collection everyone's color picker can add to and see.
public sealed class CustomColorPaletteService
{
    private readonly List<string> _colors = new();

    public IReadOnlyList<string> Colors => _colors;

    public event Action? Changed;

    public void Apply(IEnumerable<string> colors)
    {
        _colors.Clear();
        _colors.AddRange(colors);
        Changed?.Invoke();
    }

    public void Add(string hex)
    {
        if (_colors.Contains(hex, StringComparer.OrdinalIgnoreCase))
            return;

        _colors.Add(hex);
        Changed?.Invoke();
    }

    public bool Remove(string hex)
    {
        var index = _colors.FindIndex(color =>
            string.Equals(color, hex, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        _colors.RemoveAt(index);
        Changed?.Invoke();
        return true;
    }
}
