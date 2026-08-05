using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Lada.Models;
using Lada.Resources;
using Lada.Services;
using Lada.Windows;
using Microsoft.Win32;

namespace Lada;

public partial class App : Application
{
    private LayoutManager _layoutManager = null!;
    private TrayIconManager _trayIconManager = null!;
    private DesktopToggleService _desktopToggleService = null!;
    private GlobalHotkeyService _hotkeyService = null!;
    private ThemeManager _themeManager = null!;
    private LocalizationManager _localizationManager = null!;
    private HoverFadeManager _hoverFadeManager = null!;
    private MagnetismManager _magnetismManager = null!;
    private HardwareMonitorService _hardwareMonitorService = null!;
    private DesktopAutoOrganizeWatcher _desktopAutoOrganizeWatcher = null!;
    private readonly List<LadaWindow> _ladaWindows = new();
    private bool _overlayActive;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _layoutManager = new LayoutManager(LayoutManager.GetDefaultPath());
        var savedLayout = _layoutManager.Load();

        if (savedLayout.Ladas.Count == 0)
        {
            savedLayout.Ladas.Add(new LadaLayout { Title = "Lada", X = 100, Y = 100 });
        }

        // Applied before any LadaWindow is created so every window's
        // DynamicResource lookups resolve against the right theme from its
        // very first frame, instead of flashing Midnight then switching.
        _themeManager = new ThemeManager();
        _themeManager.Apply(savedLayout.Theme);

        _localizationManager = new LocalizationManager();
        _localizationManager.Apply(savedLayout.Language ?? LocalizationManager.DetectSystemLanguage());

        _hoverFadeManager = new HoverFadeManager();
        _hoverFadeManager.Apply(savedLayout.HoverFadeEnabled);

        _magnetismManager = new MagnetismManager();
        _magnetismManager.Apply(savedLayout.MagnetismEnabled);

        _hardwareMonitorService = new HardwareMonitorService();

        foreach (var ladaLayout in savedLayout.Ladas)
        {
            CreateLadaWindow(ladaLayout);
        }

        EnsureAllLadasVisible();

        _desktopAutoOrganizeWatcher = new DesktopAutoOrganizeWatcher(() => _ladaWindows);
        _desktopAutoOrganizeWatcher.Start();

        _trayIconManager = new TrayIconManager();
        _trayIconManager.NewLadaRequested += () => CreateNewLada();
        _trayIconManager.ThemeChangeRequested += ChangeTheme;
        _trayIconManager.SetActiveTheme(_themeManager.Current);
        _trayIconManager.LanguageChangeRequested += ChangeLanguage;
        _trayIconManager.SetActiveLanguage(_localizationManager.Current);
        _trayIconManager.HoverFadeToggleRequested += ChangeHoverFade;
        _trayIconManager.SetHoverFadeEnabled(_hoverFadeManager.Enabled);
        _trayIconManager.MagnetismToggleRequested += ChangeMagnetism;
        _trayIconManager.SetMagnetismEnabled(_magnetismManager.Enabled);
        _trayIconManager.ArrangeRequested += ArrangeAllLadas;
        _trayIconManager.AboutRequested += () => new AboutWindow().Show();
        _desktopToggleService = new DesktopToggleService(() => _ladaWindows);
        _desktopToggleService.Start();

        _trayIconManager.ShowAllRequested += () => _desktopToggleService.ShowAll();
        _trayIconManager.ExitRequested += Shutdown;

        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.OverlayRequested += ToggleOverlay;
        _hotkeyService.ToggleAllRequested += () => _desktopToggleService.Toggle();
        _hotkeyService.HotkeyRegistrationFailed += message => _trayIconManager.ShowBalloon("Lada", message);
        _hotkeyService.Start();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void ToggleOverlay()
    {
        _overlayActive = !_overlayActive;

        if (_overlayActive)
        {
            _desktopToggleService.ShowAll();
        }

        foreach (var window in _ladaWindows)
        {
            window.SetOverlayMode(_overlayActive);
        }
    }

    private void ChangeTheme(AppTheme theme)
    {
        _themeManager.Apply(theme);
        _trayIconManager.SetActiveTheme(theme);
        PersistLayout();
    }

    private void ChangeLanguage(AppLanguage language)
    {
        _localizationManager.Apply(language);
        _trayIconManager.SetActiveLanguage(language);
        _trayIconManager.RefreshTexts();
        PersistLayout();
    }

    private void ChangeHoverFade(bool enabled)
    {
        _hoverFadeManager.Apply(enabled);
        PersistLayout();
    }

    private void ChangeMagnetism(bool enabled)
    {
        _magnetismManager.Apply(enabled);
        PersistLayout();
    }

    // Arranges every currently visible lada into a flow layout on the
    // primary screen -- a one-shot action, not a persistent mode. A
    // currently hidden lada (desktop double-click toggle) is skipped
    // entirely, keeping its position untouched.
    private void ArrangeAllLadas()
    {
        var visibleWindows = _ladaWindows.Where(w => w.Visibility == Visibility.Visible).ToList();
        if (visibleWindows.Count == 0)
            return;

        var currentBounds = visibleWindows.Select(w => w.GetPhysicalBounds()).ToList();
        var screenBounds = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;

        var arranged = LadaArrangeCalculator.Arrange(currentBounds, screenBounds);

        for (var i = 0; i < visibleWindows.Count; i++)
        {
            visibleWindows[i].SetPhysicalPosition(arranged[i].X, arranged[i].Y);
        }

        PersistLayout();
    }

    private void CreateLadaWindow(LadaLayout layout)
    {
        var window = new LadaWindow(layout, _themeManager, _localizationManager, _hoverFadeManager, _magnetismManager, _hardwareMonitorService, () => _ladaWindows);
        window.LayoutChanged += (_, _) => PersistLayout();
        window.ItemLaunchFailed += message => _trayIconManager.ShowBalloon("Lada", message);
        window.DrawerOperationFailed += message => _trayIconManager.ShowBalloon("Lada", message);
        window.TimerFinished += message => _trayIconManager.ShowBalloon("Lada", message);
        window.NewLadaRequested += () => CreateNewLada(window);
        window.DeleteRequested += () => DeleteLadaWindow(window);
        window.AutoOrganizeCategoriesChanged += () => _desktopAutoOrganizeWatcher.Sweep();
        _ladaWindows.Add(window);
        window.Show();
        window.EnsureVisible(_ladaWindows.Count);

        if (_overlayActive)
        {
            window.SetOverlayMode(true);
        }
    }

    // Requested from a specific lada's own menu (its "Nouveau lada" entry):
    // the new one spawns just offset from that lada, so it's obviously
    // related rather than appearing at a fixed, possibly far-away spot.
    // Requested from the tray icon instead (no source lada): falls back to
    // the original fixed-point cascade, since there's nothing to spawn near.
    private void CreateNewLada(LadaWindow? sourceWindow = null)
    {
        double x, y;
        if (sourceWindow is not null)
        {
            x = sourceWindow.Left + 24;
            y = sourceWindow.Top + 24;
        }
        else
        {
            var offset = _ladaWindows.Count * 24;
            x = 100 + offset;
            y = 100 + offset;
        }

        var iconColor = ColorPalette.ForTheme(_themeManager.Current)[0];
        CreateLadaWindow(new LadaLayout { Title = "Lada", X = x, Y = y, IconColor = iconColor });
        PersistLayout();
    }

    private void DeleteLadaWindow(LadaWindow window)
    {
        window.RestoreAllAbsorbedDesktopIcons();
        _ladaWindows.Remove(window);
        window.Close();
        PersistLayout();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        EnsureAllLadasVisible();
    }

    private void EnsureAllLadasVisible()
    {
        var cascadeIndex = 0;
        var anyRepositioned = false;

        foreach (var window in _ladaWindows)
        {
            if (window.EnsureVisible(cascadeIndex))
            {
                cascadeIndex++;
                anyRepositioned = true;
            }
        }

        if (anyRepositioned)
        {
            PersistLayout();
        }
    }

    private void PersistLayout()
    {
        var collection = new LadaLayoutCollection
        {
            Theme = _themeManager.Current,
            Language = _localizationManager.Current,
            HoverFadeEnabled = _hoverFadeManager.Enabled,
            MagnetismEnabled = _magnetismManager.Enabled,
            Ladas = _ladaWindows.Select(w => w.ToLayout()).ToList()
        };
        _layoutManager.RequestSave(collection);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var collection = new LadaLayoutCollection
        {
            Theme = _themeManager.Current,
            Language = _localizationManager.Current,
            HoverFadeEnabled = _hoverFadeManager.Enabled,
            MagnetismEnabled = _magnetismManager.Enabled,
            Ladas = _ladaWindows.Select(w => w.ToLayout()).ToList()
        };
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        _layoutManager.SaveImmediate(collection);
        _layoutManager.Dispose();
        _trayIconManager.Dispose();
        _desktopToggleService.Dispose();
        _hotkeyService.Dispose();
        _desktopAutoOrganizeWatcher.Dispose();
        _hardwareMonitorService.Dispose();

        base.OnExit(e);
    }
}
