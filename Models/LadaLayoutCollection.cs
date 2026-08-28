using System.Collections.Generic;

namespace Lada.Models;

public sealed class LadaLayoutCollection
{
    public const int CurrentSettingsVersion = 1;

    // Version 1 changes pure widget windows to content-only by default. The
    // explicit version lets existing layout files migrate once without
    // preventing a user from deliberately turning widget chrome back on
    // afterward through the tray menu.
    public int SettingsVersion { get; set; }

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
    public bool BackgroundBlurEnabled { get; set; } = true;
    public bool WidgetChromeVisible { get; set; } = false;
    public double AppearanceBrightnessPercent { get; set; } = 100;
    public string? AppearanceBackgroundColor { get; set; }
    public bool AndersonColorsSynchronized { get; set; } = true;
    public string? AndersonSynchronizedColor { get; set; }

    // Colors saved permanently from the custom color picker (see
    // CustomColorPaletteService), shared across every lada rather than
    // stored per-lada like IconColor.
    public List<string> CustomColors { get; set; } = new();

    public List<LadaLayout> Ladas { get; set; } = new();

    public void ApplyMigrations()
    {
        if (SettingsVersion < 1)
        {
            WidgetChromeVisible = false;
        }

        SettingsVersion = CurrentSettingsVersion;
    }
}
