using System;
using System.Collections.Generic;
using System.Windows;
using Lada.Native;
using Lada.Windows;

namespace Lada.Services;

public sealed class DesktopToggleService : IDisposable
{
    private readonly MouseHook _hook = new();
    private readonly Func<IEnumerable<LadaWindow>> _getLadas;
    private bool _ladasHidden;

    public DesktopToggleService(Func<IEnumerable<LadaWindow>> getLadas)
    {
        _getLadas = getLadas;
        _hook.DesktopDoubleClicked += (_, _) => Toggle();
    }

    public void Start() => _hook.Install();

    public void Toggle()
    {
        _ladasHidden = !_ladasHidden;
        ApplyVisibility();
    }

    public void ShowAll()
    {
        _ladasHidden = false;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        var visibility = _ladasHidden ? Visibility.Hidden : Visibility.Visible;
        foreach (var lada in _getLadas())
        {
            lada.Visibility = visibility;
        }
    }

    public void Dispose() => _hook.Dispose();
}
