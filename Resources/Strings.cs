using System;
using Lada.Models;

namespace Lada.Resources;

// Every user-facing string in the app, hand-picked per language rather than
// via .resx/ResourceManager — consistent with the rest of the project's
// "explicit C#, no framework" style. LocalizationManager is the only thing
// that sets Language; everything else just reads through these properties.
public static class Strings
{
    public static AppLanguage Language { get; set; } = AppLanguage.French;

    private static string Pick(string fr, string en) => Language == AppLanguage.French ? fr : en;

    // Tray menu
    public static string NewLada => Pick("Nouveau lada", "New lada");
    public static string ShowAllLadas => Pick("Réafficher tous les lada", "Show all ladas");
    public static string ThemeMenu => Pick("Thème", "Theme");
    public static string LanguageMenu => Pick("Langue", "Language");
    public static string HoverFadeMenuItem => Pick("Opacité au survol", "Opacity on hover");
    public static string MagnetismMenuItem => Pick("Magnétisme", "Magnetism");
    public static string PerspectiveTiltMenuItem => Pick("Perspective 3D", "3D perspective");
    public static string HudGlowMenuItem => Pick("Lueur HUD", "HUD glow");
    public static string BackgroundBlurMenuItem => Pick("Flou d’arrière-plan", "Background blur");
    public static string NewWidgetTrayMenuItem => Pick("Nouveau widget", "New widget");
    public static string WidgetChromeMenuItem => Pick("Titre et icône des widgets", "Widget title and icon");
    public static string ArrangeLadasMenuItem => Pick("Ranger les ladas", "Arrange ladas");
    public static string AboutMenuItem => Pick("À propos", "About");
    public static string CustomizeMenuItem => Pick("Personnaliser", "Customize");
    public static string CustomizeBrightness => Pick("Luminosité", "Brightness");
    public static string CustomizeBrightnessDescription => Pick(
        "Modifie en temps réel la luminosité des surfaces de tous les Ladas.",
        "Adjusts the surface brightness of every Lada in real time.");
    public static string CustomizeBackgroundColor => Pick("Couleur de fond", "Background color");
    public static string CustomizeBackgroundColorDescription => Pick(
        "Remplace la couleur de fond tout en conservant la transparence du thème.",
        "Replaces the background color while preserving the theme transparency.");
    public static string CustomizeResetBackgroundColor => Pick("Réinitialiser", "Reset");
    public static string NewGmailLadaMenuItem => Pick("Nouveau Lada Gmail", "New Gmail Lada");
    public static string Quit => Pick("Quitter", "Quit");

    public static string AboutMessage => Pick(
        "Lada, un organiseur de bureau Windows personnel avec des conteneurs semi-transparents pour tes raccourcis. Créé par Quentin Ecrepont.",
        "Lada, a personal Windows desktop organizer with semi-transparent containers for your shortcuts. Made by Quentin Ecrepont.");

    public static string AboutOpenSource => Pick("Gratuit et open source :", "Free and open source:");
    public static string WeatherDataAttribution => Pick("Données météo par Open-Meteo", "Weather data by Open-Meteo");

    // Lada empty-space context menu
    public static string SortByType => Pick("Trier par type", "Sort by type");
    public static string AutoSortToggle => Pick("Tri automatique des nouveaux fichiers", "Auto-sort new files");
    public static string SizePresetSubmenu => Pick("Taille prédéfinie", "Preset size");
    public static string FitToContentMenuItem => Pick("Ajuster au contenu", "Fit to content");
    public static string NewComponentSubmenu => Pick("Nouveau composant", "New component");
    public static string ClockWidgetMenuItem => Pick("Horloge", "Clock");
    public static string DiskWidgetMenuItem => Pick("Espace disque", "Disk space");
    public static string TimerWidgetMenuItem => Pick("Minuteur", "Timer");
    public static string BatteryWidgetMenuItem => Pick("Batterie", "Battery");
    public static string MemoryWidgetMenuItem => Pick("Mémoire", "Memory");
    public static string CpuWidgetMenuItem => Pick("Processeur", "CPU");
    public static string GpuWidgetMenuItem => Pick("Carte graphique", "GPU");
    public static string NetworkWidgetMenuItem => Pick("Réseau", "Network");
    public static string DeleteLadaMenuItem => Pick("Supprimer ce lada", "Delete this lada");

    public static string WidgetComponentLabel(WidgetComponentType type) => type switch
    {
        WidgetComponentType.Clock => ClockWidgetMenuItem,
        WidgetComponentType.Disk => DiskWidgetMenuItem,
        WidgetComponentType.Timer => TimerWidgetMenuItem,
        WidgetComponentType.Battery => BatteryWidgetMenuItem,
        WidgetComponentType.Memory => MemoryWidgetMenuItem,
        WidgetComponentType.Cpu => CpuWidgetMenuItem,
        WidgetComponentType.Gpu => GpuWidgetMenuItem,
        WidgetComponentType.Network => NetworkWidgetMenuItem,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string AutoOrganizeSubmenu => Pick("Auto-organisation", "Auto-organize");
    public static string AutoOrganizeCategoryFolder => Pick("Dossiers", "Folders");
    public static string AutoOrganizeCategoryDocument => Pick("Documents", "Documents");
    public static string AutoOrganizeCategoryImage => Pick("Images", "Images");
    public static string AutoOrganizeCategoryVideo => Pick("Vidéos", "Videos");
    public static string AutoOrganizeCategoryAudio => Pick("Audio", "Audio");
    public static string AutoOrganizeCategoryExecutable => Pick("Exécutables", "Executables");
    public static string AutoOrganizeCategoryOther => Pick("Autres", "Other");

    public static string DeleteLadaConfirmationBody => Pick(
        "Supprimer définitivement ce lada ? Les fichiers qu'il contient resteront intacts sur le disque.",
        "Permanently delete this lada? The files inside it will stay untouched on disk.");

    // Themed confirmation dialog (ConfirmationWindow)
    public static string ConfirmButton => Pick("Confirmer", "Confirm");
    public static string CancelButton => Pick("Annuler", "Cancel");

    // Forecast weather
    public static string ForecastLocationTitle => Pick("Météo locale Forecast", "Forecast local weather");
    public static string ForecastLocationConsent => Pick(
        "Autoriser Forecast à adapter son apparence à la météo locale ? Windows demandera l’accès à votre position. Seules des coordonnées arrondies (environ à l’échelle de la ville) seront conservées sur ce PC et envoyées à Open-Meteo pour obtenir la météo. Vous pourrez refuser : le thème conservera alors son animation décorative.",
        "Allow Forecast to adapt its appearance to local weather? Windows will request access to your location. Only rounded coordinates (roughly city-level) will be kept on this PC and sent to Open-Meteo to retrieve weather data. You can decline: the theme will keep its decorative animation.");
    public static string ForecastWeatherReady => Pick(
        "Forecast est maintenant synchronisé avec la météo locale.",
        "Forecast is now synchronized with local weather.");
    public static string ForecastLocationDenied => Pick(
        "La localisation n’a pas été autorisée. Forecast conserve son animation décorative.",
        "Location access wasn't allowed. Forecast will keep its decorative animation.");
    public static string ForecastWeatherUnavailable => Pick(
        "La météo locale est momentanément indisponible. Forecast utilise la dernière météo connue ou son animation décorative.",
        "Local weather is temporarily unavailable. Forecast is using the last known weather or its decorative animation.");
    public static string ForecastDebugMenu => Pick("Debug Forecast", "Forecast debug");
    public static string ForecastDebugWeatherLabel(ForecastDebugWeather weather) => weather switch
    {
        ForecastDebugWeather.Real => Pick("Météo réelle", "Live weather"),
        ForecastDebugWeather.ClearDay => Pick("Dégagé — jour", "Clear — day"),
        ForecastDebugWeather.ClearNight => Pick("Dégagé — nuit", "Clear — night"),
        ForecastDebugWeather.Cloudy => Pick("Nuageux", "Cloudy"),
        ForecastDebugWeather.Fog => Pick("Brouillard", "Fog"),
        ForecastDebugWeather.LightRain => Pick("Pluie légère", "Light rain"),
        ForecastDebugWeather.HeavyRain => Pick("Forte pluie", "Heavy rain"),
        ForecastDebugWeather.Snow => Pick("Neige", "Snow"),
        ForecastDebugWeather.Storm => Pick("Orage", "Storm"),
        _ => throw new ArgumentOutOfRangeException(nameof(weather))
    };

    // Tabs
    public static string RenameTab => Pick("Renommer", "Rename");
    public static string DeleteTabMenuItem => Pick("Supprimer ce tab", "Delete this tab");
    public static string MoveToSubmenu => Pick("Déplacer vers", "Move to");
    public static string DefaultTabTitle(int number) => Pick($"Tab {number}", $"Tab {number}");

    public static string DeleteTabConfirmationBody(string tabTitle, int itemCount) => Pick(
        $"Supprimer le tab \"{tabTitle}\" ? Il contient {itemCount} élément(s) qui seront retirés de ce lada (les fichiers resteront intacts sur le disque).",
        $"Delete the tab \"{tabTitle}\"? It holds {itemCount} item(s) that will be removed from this lada (the files will stay untouched on disk).");

    // Tab content mode
    public static string ConvertTabToToDoList => Pick("Convertir en liste de tâches", "Convert to to-do list");
    public static string ConvertTabToMemo => Pick("Convertir en mémo", "Convert to memo");
    public static string ConvertTabToIcons => Pick("Revenir aux icônes", "Back to icons");
    public static string ConvertTabToMail => Pick("Convertir en mail", "Convert to mail");
    public static string ConvertTabBlockedTitle => Pick("Conversion impossible", "Can't convert");

    public static string ConvertTabBlockedBody => Pick(
        "Ce tab contient encore du contenu. Videz-le d'abord avant de changer de mode.",
        "This tab still has content. Clear it first before changing modes.");

    public static string CutMenuItem => Pick("Couper", "Cut");
    public static string CopyMenuItem => Pick("Copier", "Copy");
    public static string PasteMenuItem => Pick("Coller", "Paste");

    // Items
    public static string RemoveFromLada => Pick("Retirer de ce lada", "Remove from this lada");
    public static string RemoveFromLadaCount(int count) => Pick($"Retirer de ce lada ({count})", $"Remove from this lada ({count})");
    public static string ItemLaunchFailed(string displayName) => Pick($"Impossible d'ouvrir \"{displayName}\".", $"Couldn't open \"{displayName}\".");
    public static string FolderOpenFailed(string folderName) => Pick($"Impossible d'ouvrir le dossier \"{folderName}\".", $"Couldn't open the folder \"{folderName}\".");

    // Drawers
    public static string ShowDrawerContent => Pick("Afficher le contenu", "Show contents");
    public static string CollapseDrawer => Pick("Replier", "Collapse");
    public static string DrawerAlreadyExists(string fileName) => Pick($"\"{fileName}\" existe déjà dans ce dossier.", $"\"{fileName}\" already exists in this folder.");

    public static string DrawerMoveFailedCrossDrive(string fileName) => Pick(
        $"Impossible de déplacer le dossier \"{fileName}\" : un déplacement entre deux disques différents n'est pas pris en charge pour un dossier.",
        $"Couldn't move the folder \"{fileName}\": moving a folder across two different drives isn't supported.");

    public static string DrawerMoveFailedGeneric(string fileName) => Pick($"Impossible de déplacer \"{fileName}\".", $"Couldn't move \"{fileName}\".");

    // Clock widget
    public static string ChangeTimeZone => Pick("Changer de fuseau horaire", "Change timezone");
    public static string TimeZonePickerTitle => Pick("Choisir un fuseau horaire", "Choose a timezone");

    // Disk widget
    public static string ChangeDrive => Pick("Changer de lecteur", "Change drive");
    public static string ChangeGpu => Pick("Changer de GPU", "Change GPU");
    public static string ChangeNetworkAdapter => Pick("Changer d'interface", "Change adapter");
    public static string NetworkSpeed(string formattedBytes) => $"{formattedBytes}/s";

    // Battery / Memory / CPU / GPU widgets
    public static string BatteryPercent(string percent) => $"{percent}%";
    public static string BatteryChargingSuffix => Pick(" (en charge)", " (charging)");
    public static string MemoryUsage(string usedGb, string totalGb) => Pick($"{usedGb} / {totalGb} Go", $"{usedGb} / {totalGb} GB");
    public static string UsagePercent(string percent) => Pick($"{percent}% utilisé", $"{percent}% used");
    public static string TemperatureCelsius(string celsius) => $"{celsius} °C";
    public static string FrequencyGhz(string ghz) => $"{ghz} GHz";
    public static string DetailedViewMenuItem => Pick("Vue détaillée", "Detailed view");
    public static string DiskFreeSpace(string formattedGb) => Pick($"{formattedGb} Go libres", $"{formattedGb} GB free");
    public static string WidgetUnavailable => Pick("Indisponible", "Unavailable");

    // Timer widget
    public static string TimerDurationPickerTitle => Pick("Durée du minuteur", "Timer duration");
    public static string TimerDurationPickerHint => Pick("Entrée pour valider, Échap pour annuler", "Enter to confirm, Escape to cancel");
    public static string StartTimer => Pick("Démarrer", "Start");
    public static string PauseTimer => Pick("Mettre en pause", "Pause");
    public static string ResetTimer => Pick("Réinitialiser", "Reset");
    public static string ChangeTimerDuration => Pick("Changer la durée", "Change duration");
    public static string TimerFinishedMessage(string label) => Pick($"{label} terminé", $"{label} finished");

    // Icon picker
    public static string ChooseIconTooltip => Pick("Choisir une icône", "Choose an icon");
    public static string CustomColorHexTooltip => Pick("Code hex (#RRGGBB)", "Hex code (#RRGGBB)");
    public static string SaveColorTooltip => Pick("Enregistrer cette couleur", "Save this color");
    public static string DeleteCustomColor => Pick("Supprimer", "Delete");
    public static string ShareColorWithAllLadasTooltip => Pick(
        "Appliquer cette couleur à tous les Ladas",
        "Apply this color to every Lada");
    public static string AndersonColorsSynchronizedTooltip => Pick(
        "Couleurs Anderson synchronisées — cliquer pour les rendre indépendantes",
        "Anderson colors synchronized — click to make them independent");
    public static string AndersonColorsIndependentTooltip => Pick(
        "Couleurs Anderson indépendantes — cliquer pour les synchroniser",
        "Anderson colors independent — click to synchronize them");

    // List view
    public static string ViewModeSubmenu => Pick("Affichage", "View");
    public static string ViewModeGrid => Pick("Grille", "Grid");
    public static string ViewModeList => Pick("Liste", "List");
    public static string ColumnsSubmenu => Pick("Colonnes", "Columns");
    public static string ColumnName => Pick("Nom", "Name");
    public static string ColumnType => Pick("Type", "Type");
    public static string ColumnSize => Pick("Taille", "Size");
    public static string ColumnModifiedDate => Pick("Date de modification", "Date modified");

    public static string FileSizeBytes => Pick("o", "B");
    public static string FileSizeKilobytes => Pick("Ko", "KB");
    public static string FileSizeMegabytes => Pick("Mo", "MB");
    public static string FileSizeGigabytes => Pick("Go", "GB");

    // Mail widget
    public static string MailConnectButton => Pick("Se connecter à Gmail", "Connect to Gmail");
    public static string MailLoadingIndicator => Pick("Chargement…", "Loading…");
    public static string MailReauthRequired => Pick("Reconnexion nécessaire", "Reconnection needed");
    public static string MailReauthButton => Pick("Se reconnecter", "Reconnect");
    public static string MailLastUpdateFailed => Pick("Dernière mise à jour : échec", "Last update: failed");
    public static string MailNoMessages => Pick("Aucun e-mail dans la boîte de réception", "No mail in the inbox");
    public static string MailNoClientSecret => Pick(
        "Configuration Gmail requise : placez google_client_secret.json dans %AppData%\\Lada. Ce fichier provient d’un client OAuth « Application de bureau » dont l’API Gmail est activée.",
        "Gmail setup required: place google_client_secret.json in %AppData%\\Lada. This file comes from a Desktop OAuth client with the Gmail API enabled.");
    public static string OpenGmailConfigurationFolder => Pick(
        "Ouvrir le dossier de configuration",
        "Open configuration folder");

    // Global hotkeys
    public static string OverlayHotkeyDescription => Pick("Ctrl+Alt+O (Overlay)", "Ctrl+Alt+O (Overlay)");
    public static string ToggleAllHotkeyDescription => Pick("Ctrl+Alt+D (Afficher/Masquer)", "Ctrl+Alt+D (Show/Hide)");

    public static string HotkeyUnavailable(string description, string win32Message, int errorCode) => Pick(
        $"Raccourci {description} indisponible : {win32Message} (code {errorCode}). Probablement déjà utilisé par un autre logiciel.",
        $"Shortcut {description} unavailable: {win32Message} (code {errorCode}). Probably already claimed by another app.");
}
