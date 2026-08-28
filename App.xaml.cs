using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private PerspectiveTiltManager _perspectiveTiltManager = null!;
    private HudGlowManager _hudGlowManager = null!;
    private BackgroundBlurManager _backgroundBlurManager = null!;
    private AppearanceCustomizationManager _appearanceCustomizationManager = null!;
    private WidgetChromeManager _widgetChromeManager = null!;
    private CustomColorPaletteService _customColorPaletteService = null!;
    private AndersonColorSyncManager _andersonColorSyncManager = null!;
    private HardwareMonitorService _hardwareMonitorService = null!;
    private GmailAuthService _gmailAuthService = null!;
    private GmailPollingService _gmailPollingService = null!;
    private WeatherService _weatherService = null!;
    private DesktopAutoOrganizeWatcher _desktopAutoOrganizeWatcher = null!;
    private readonly List<LadaWindow> _ladaWindows = new();
    private bool _overlayActive;
    private bool _weatherTransferAuthorizedForSession;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Best effort: Lada remains usable with its normal transparent
        // backgrounds when the optional Windows Acrylic runtime is absent.
        WindowsAppRuntimeInitializer.TryInitialize();

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

        _perspectiveTiltManager = new PerspectiveTiltManager();
        _perspectiveTiltManager.Apply(savedLayout.PerspectiveTiltEnabled);

        _hudGlowManager = new HudGlowManager();
        _hudGlowManager.Apply(savedLayout.HudGlowEnabled);

        _backgroundBlurManager = new BackgroundBlurManager();
        _backgroundBlurManager.Apply(savedLayout.BackgroundBlurEnabled);

        _appearanceCustomizationManager = new AppearanceCustomizationManager();
        _appearanceCustomizationManager.ApplyBrightness(savedLayout.AppearanceBrightnessPercent);
        _appearanceCustomizationManager.ApplyBackgroundColor(savedLayout.AppearanceBackgroundColor);
        _appearanceCustomizationManager.Changed += PersistLayout;

        _widgetChromeManager = new WidgetChromeManager();
        _widgetChromeManager.Apply(savedLayout.WidgetChromeVisible);

        _customColorPaletteService = new CustomColorPaletteService();
        _customColorPaletteService.Apply(savedLayout.CustomColors);
        _customColorPaletteService.Changed += PersistLayout;

        _andersonColorSyncManager = new AndersonColorSyncManager();
        var restoredAndersonColor = savedLayout.AndersonSynchronizedColor
            ?? savedLayout.Ladas.FirstOrDefault()?.IconColor
            ?? ColorPalette.ForTheme(AppTheme.Anderson)[0];
        _andersonColorSyncManager.Apply(savedLayout.AndersonColorsSynchronized, restoredAndersonColor);
        _andersonColorSyncManager.Changed += PersistLayout;

        _hardwareMonitorService = new HardwareMonitorService();
        _weatherService = new WeatherService(WeatherService.GetDefaultPath());

        _gmailAuthService = new GmailAuthService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lada"));
        _gmailPollingService = new GmailPollingService();
        _gmailPollingService.Configure(_gmailAuthService);

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
        _trayIconManager.ForecastDebugWeatherRequested += ChangeForecastDebugWeather;
        _trayIconManager.SetActiveForecastDebugWeather(_weatherService.DebugWeather);
        _trayIconManager.LanguageChangeRequested += ChangeLanguage;
        _trayIconManager.SetActiveLanguage(_localizationManager.Current);
        _trayIconManager.HoverFadeToggleRequested += ChangeHoverFade;
        _trayIconManager.SetHoverFadeEnabled(_hoverFadeManager.Enabled);
        _trayIconManager.MagnetismToggleRequested += ChangeMagnetism;
        _trayIconManager.SetMagnetismEnabled(_magnetismManager.Enabled);
        _trayIconManager.PerspectiveTiltToggleRequested += ChangePerspectiveTilt;
        _trayIconManager.SetPerspectiveTiltEnabled(_perspectiveTiltManager.Enabled);
        _trayIconManager.HudGlowToggleRequested += ChangeHudGlow;
        _trayIconManager.SetHudGlowEnabled(_hudGlowManager.Enabled);
        _trayIconManager.BackgroundBlurToggleRequested += ChangeBackgroundBlur;
        _trayIconManager.SetBackgroundBlurEnabled(_backgroundBlurManager.Enabled);
        _trayIconManager.NewWidgetRequested += CreateNewWidget;
        _trayIconManager.WidgetChromeToggleRequested += ChangeWidgetChromeVisible;
        _trayIconManager.SetWidgetChromeEnabled(_widgetChromeManager.Enabled);
        _trayIconManager.ArrangeRequested += ArrangeAllLadas;
        _trayIconManager.NewGmailLadaRequested += CreateNewGmailLada;
        _trayIconManager.CustomizeRequested += () =>
            new CustomizeWindow(_appearanceCustomizationManager).ShowDialog();
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

        // A saved Forecast session can render its last locally cached state
        // immediately, but it never contacts the weather provider silently.
        // Defer the consent dialog until all startup UI is fully available.
        if (_themeManager.Current == AppTheme.Forecast)
            _ = Dispatcher.InvokeAsync(EnableForecastWeatherAsync);

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

    private async void ChangeTheme(AppTheme theme)
    {
        _themeManager.Apply(theme);
        _trayIconManager.SetActiveTheme(theme);
        PersistLayout();

        if (theme != AppTheme.Forecast || _weatherService.DebugWeather != ForecastDebugWeather.Real)
            return;

        await EnableForecastWeatherAsync();
    }

    private async void ChangeForecastDebugWeather(ForecastDebugWeather weather)
    {
        _weatherService.SetDebugWeather(weather);
        _trayIconManager.SetActiveForecastDebugWeather(weather);

        if (weather == ForecastDebugWeather.Real)
            await EnableForecastWeatherAsync();
    }

    private async Task EnableForecastWeatherAsync()
    {
        var firstActivation = !_weatherService.HasCachedLocation;
        if (!_weatherTransferAuthorizedForSession)
        {
            var confirmation = new ConfirmationWindow(
                Strings.ForecastLocationTitle,
                Strings.ForecastLocationConsent);
            var owner = _ladaWindows.FirstOrDefault(window => window.IsVisible);
            if (owner is not null)
                confirmation.Owner = owner;
            confirmation.ShowDialog();
            if (!confirmation.Confirmed)
                return;

            _weatherTransferAuthorizedForSession = true;
        }

        // Re-read the position on every explicit activation. Once Windows
        // has granted access this is silent, and it lets Forecast follow a
        // laptop that has moved since the last session.
        var result = await _weatherService.ActivateAsync(requestLocation: true);
        if (result == WeatherActivationResult.Updated)
        {
            if (firstActivation)
                _trayIconManager.ShowBalloon("Forecast", Strings.ForecastWeatherReady);
        }
        else if (result == WeatherActivationResult.PermissionDenied)
        {
            _trayIconManager.ShowBalloon("Forecast", Strings.ForecastLocationDenied);
        }
        else
        {
            _trayIconManager.ShowBalloon("Forecast", Strings.ForecastWeatherUnavailable);
        }
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

    private void ChangePerspectiveTilt(bool enabled)
    {
        _perspectiveTiltManager.Apply(enabled);
        PersistLayout();
    }

    private void ChangeHudGlow(bool enabled)
    {
        _hudGlowManager.Apply(enabled);
        PersistLayout();
    }

    private void ChangeBackgroundBlur(bool enabled)
    {
        _backgroundBlurManager.Apply(enabled);
        PersistLayout();
    }

    private void ChangeWidgetChromeVisible(bool enabled)
    {
        _widgetChromeManager.Apply(enabled);
        PersistLayout();
    }

    private void CreateNewWidget(WidgetComponentType type)
    {
        var item = BuildWidgetComponentItem(type);
        if (item is null)
            return;

        var iconColor = ColorPalette.ForTheme(_themeManager.Current)[0];
        var layout = new LadaLayout
        {
            Title = "Lada",
            IsWidget = true,
            IconColor = iconColor,
            Items = { item }
        };

        CreateLadaWindow(layout);
        PersistLayout();
    }

    // Disk/GPU/Network normally offer a cascading submenu of live options
    // (BuildDriveMenuItems/BuildGpuMenuItems/BuildNetworkAdapterMenuItems in
    // LadaWindow.*Widget.cs) built from WPF MenuItems inside a WPF
    // ContextMenu -- the tray menu is a separate WinForms ContextMenuStrip
    // (TrayIconManager), so those can't be reused here, and rebuilding the
    // same cascading list in WinForms for a one-time creation shortcut isn't
    // worth the duplication. Picking one of these three from the tray
    // creates the widget immediately with a sensible default instead; the
    // widget's own existing "Change drive"/"Change GPU"/"Change adapter"
    // submenu (unchanged, still WPF, already cascading) is one right-click
    // away if the default wasn't the right one.
    private LadaItem? BuildWidgetComponentItem(WidgetComponentType type)
    {
        switch (type)
        {
            case WidgetComponentType.Clock:
            {
                var picker = new TimeZonePickerWindow();
                if (picker.ShowDialog() != true || picker.SelectedTimeZone is null)
                    return null;
                return new LadaItem { IsClockWidget = true, TimeZoneId = picker.SelectedTimeZone.Id, DisplayName = picker.SelectedTimeZone.DisplayName };
            }
            case WidgetComponentType.Timer:
            {
                var picker = new TimerDurationPickerWindow();
                if (picker.ShowDialog() != true)
                    return null;
                var totalSeconds = (int)picker.SelectedDuration.TotalSeconds;
                return new LadaItem { IsTimerWidget = true, DisplayName = Strings.TimerWidgetMenuItem, TimerDurationSeconds = totalSeconds, TimerRemainingSeconds = totalSeconds };
            }
            case WidgetComponentType.Disk:
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
                if (drive is null)
                    return null;
                return new LadaItem { IsDiskWidget = true, DrivePath = drive.Name, DisplayName = drive.Name };
            }
            case WidgetComponentType.Battery:
                return new LadaItem { IsBatteryWidget = true, DisplayName = Strings.BatteryWidgetMenuItem };
            case WidgetComponentType.Memory:
                return new LadaItem { IsMemoryWidget = true, DisplayName = Strings.MemoryWidgetMenuItem };
            case WidgetComponentType.Cpu:
                return new LadaItem { IsCpuWidget = true, DisplayName = Strings.CpuWidgetMenuItem };
            case WidgetComponentType.Gpu:
            {
                _hardwareMonitorService.EnsureStarted();
                var gpus = _hardwareMonitorService.GetGpus();
                if (gpus.Count == 0)
                    return null;
                var gpu = gpus[0];
                return new LadaItem { IsGpuWidget = true, GpuIdentifier = gpu.Id, DisplayName = gpu.Name };
            }
            case WidgetComponentType.Network:
            {
                _hardwareMonitorService.EnsureStarted();
                var adapters = _hardwareMonitorService.GetNetworkAdapters();
                if (adapters.Count == 0)
                    return null;
                var adapter = adapters[0];
                return new LadaItem { IsNetworkWidget = true, NetworkAdapterIdentifier = adapter.Id, DisplayName = adapter.Name };
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
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
        var window = new LadaWindow(layout, _themeManager, _localizationManager, _hoverFadeManager, _magnetismManager, _perspectiveTiltManager, _hudGlowManager, _backgroundBlurManager, _appearanceCustomizationManager, _widgetChromeManager, _customColorPaletteService, _andersonColorSyncManager, _hardwareMonitorService, _gmailAuthService, _gmailPollingService, _weatherService, () => _ladaWindows);
        window.LayoutChanged += (_, _) => PersistLayout();
        window.ItemLaunchFailed += message => _trayIconManager.ShowBalloon("Lada", message);
        window.DrawerOperationFailed += message => _trayIconManager.ShowBalloon("Lada", message);
        window.TimerFinished += message => _trayIconManager.ShowBalloon("Lada", message);
        window.NewLadaRequested += () => CreateNewLada(window);
        window.NewWidgetRequested += CreateNewWidget;
        window.DeleteRequested += () => DeleteLadaWindow(window);
        window.AutoOrganizeCategoriesChanged += () => _desktopAutoOrganizeWatcher.Sweep();
        _ladaWindows.Add(window);
        window.Show();
        window.EnsureVisible(_ladaWindows.Count);
        window.EnsureBackgroundBlurAfterShow();

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

    private void CreateNewGmailLada()
    {
        var offset = _ladaWindows.Count * 24;
        var iconColor = ColorPalette.ForTheme(_themeManager.Current)[0];
        CreateLadaWindow(new LadaLayout
        {
            Title = "Gmail",
            X = 100 + offset,
            Y = 100 + offset,
            IconId = "case",
            IconColor = iconColor,
            Tabs = new List<LadaTab>
            {
                new() { Title = "Gmail", ContentMode = TabContentMode.Mail }
            }
        });
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
            PerspectiveTiltEnabled = _perspectiveTiltManager.Enabled,
            HudGlowEnabled = _hudGlowManager.Enabled,
            BackgroundBlurEnabled = _backgroundBlurManager.Enabled,
            AppearanceBrightnessPercent = _appearanceCustomizationManager.BrightnessPercent,
            AppearanceBackgroundColor = _appearanceCustomizationManager.BackgroundColorHex,
            AndersonColorsSynchronized = _andersonColorSyncManager.Enabled,
            AndersonSynchronizedColor = _andersonColorSyncManager.Color,
            WidgetChromeVisible = _widgetChromeManager.Enabled,
            CustomColors = _customColorPaletteService.Colors.ToList(),
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
            PerspectiveTiltEnabled = _perspectiveTiltManager.Enabled,
            HudGlowEnabled = _hudGlowManager.Enabled,
            BackgroundBlurEnabled = _backgroundBlurManager.Enabled,
            AppearanceBrightnessPercent = _appearanceCustomizationManager.BrightnessPercent,
            AppearanceBackgroundColor = _appearanceCustomizationManager.BackgroundColorHex,
            AndersonColorsSynchronized = _andersonColorSyncManager.Enabled,
            AndersonSynchronizedColor = _andersonColorSyncManager.Color,
            WidgetChromeVisible = _widgetChromeManager.Enabled,
            CustomColors = _customColorPaletteService.Colors.ToList(),
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
        _gmailPollingService.Dispose();
        _weatherService.Dispose();

        base.OnExit(e);
    }
}
