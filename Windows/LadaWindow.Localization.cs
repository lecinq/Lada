using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private LocalizationManager? _localizationManager;

    private void InitializeLocalization(LocalizationManager localizationManager)
    {
        _localizationManager = localizationManager;
        _localizationManager.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => _localizationManager.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        UpdateIconButtonVisual();
        UpdateAndersonColorSyncButton();
        InitializeSortMenu();
        RefreshDynamicContent();
    }
}
