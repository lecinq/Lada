using System.Collections.Generic;

namespace Lada.Models;

public sealed class LadaLayoutCollection
{
    public AppTheme Theme { get; set; } = AppTheme.Midnight;

    // Null means "never explicitly chosen" — the app then derives the
    // language from the OS display language every launch instead of a
    // saved value. Once the user picks one from the tray menu, this is set
    // and takes over from then on.
    public AppLanguage? Language { get; set; }

    public bool HoverFadeEnabled { get; set; } = false;
    public bool MagnetismEnabled { get; set; } = false;
    public bool PerspectiveTiltEnabled { get; set; } = false;
    public bool HudGlowEnabled { get; set; } = false;
    public bool WidgetChromeVisible { get; set; } = true;

    // Colors saved permanently from the custom color picker (see
    // CustomColorPaletteService), shared across every lada rather than
    // stored per-lada like IconColor.
    public List<string> CustomColors { get; set; } = new();

    public List<LadaLayout> Ladas { get; set; } = new();
}
