using System;
using System.Windows;
using Lada.Models;

namespace Lada.Services;

public sealed class ThemeManager
{
    private const string MidnightSource = "Styles/Theme.xaml";
    private const string ModernismSource = "Styles/ThemeModernism.xaml";
    private const string AndersonSource = "Styles/ThemeAnderson.xaml";

    public AppTheme Current { get; private set; } = AppTheme.Midnight;

    public event Action? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        var source = theme switch
        {
            AppTheme.Modernism => ModernismSource,
            AppTheme.Anderson => AndersonSource,
            _ => MidnightSource
        };
        var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        // Swapping the whole merged dictionary (rather than mutating brushes
        // in place) is what lets every DynamicResource consumer across the
        // app pick up the new theme's values in one shot.
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(dictionary);

        Current = theme;
        ThemeChanged?.Invoke();
    }
}
