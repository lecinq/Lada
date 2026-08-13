using System;

namespace Lada.Services;

public sealed class WidgetChromeManager
{
    public bool Enabled { get; private set; } = true;

    public event Action? Changed;

    public void Apply(bool enabled)
    {
        Enabled = enabled;
        Changed?.Invoke();
    }
}
