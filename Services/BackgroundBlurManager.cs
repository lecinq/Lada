using System;

namespace Lada.Services;

public sealed class BackgroundBlurManager
{
    public bool Enabled { get; private set; }

    public event Action? Changed;

    public void Apply(bool enabled)
    {
        Enabled = enabled;
        Changed?.Invoke();
    }
}
