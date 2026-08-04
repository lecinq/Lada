using System;
using System.Globalization;
using Lada.Models;
using Lada.Resources;

namespace Lada.Services;

public sealed class LocalizationManager
{
    public AppLanguage Current { get; private set; } = AppLanguage.French;

    public event Action? LanguageChanged;

    // Windows' own display language, used only when the user has never
    // explicitly picked one from the tray menu (LadaLayoutCollection.Language
    // is null in that case).
    public static AppLanguage DetectSystemLanguage() =>
        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.French
            : AppLanguage.English;

    public void Apply(AppLanguage language)
    {
        Current = language;
        Strings.Language = language;
        LanguageChanged?.Invoke();
    }
}
