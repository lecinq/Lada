using System;

namespace Lada.Services;

// Anderson treats the accent as one shared HUD channel by default. Keeping
// that state outside LadaWindow means newly-created windows and restored
// windows join the same channel, while disabling it restores normal
// per-window colors without destroying any window's current value.
public sealed class AndersonColorSyncManager
{
    public bool Enabled { get; private set; } = true;
    public string Color { get; private set; } = "#33FF33";

    public event Action? Changed;
    public event Action<string>? ColorChanged;

    public void Apply(bool enabled, string color)
    {
        Enabled = enabled;
        Color = color;
    }

    public void Toggle(string sourceColor)
    {
        Enabled = !Enabled;
        if (Enabled)
        {
            Color = sourceColor;
            ColorChanged?.Invoke(Color);
        }

        Changed?.Invoke();
    }

    public void SetColor(string color)
    {
        Color = color;
        ColorChanged?.Invoke(Color);
        Changed?.Invoke();
    }
}
