using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Lada.Models;
using Lada.Windows;

namespace Lada.Services;

// Surveille %USERPROFILE%\Desktop en continu et route chaque fichier
// correspondant vers l'onglet le plus récemment actif parmi ceux ayant coché
// sa catégorie (Services/FileTypeCategorizer.FileCategory), en délégant le
// masquage de l'icône réelle à DesktopIconVisibilityService. Ne surveille
// jamais le dossier Bureau public (hors scope, voir spec).
public sealed class DesktopAutoOrganizeWatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);

    private readonly Func<IEnumerable<LadaWindow>> _getWindows;
    private readonly string _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    private readonly DispatcherTimer _debounceTimer;
    private FileSystemWatcher? _watcher;

    public DesktopAutoOrganizeWatcher(Func<IEnumerable<LadaWindow>> getWindows)
    {
        _getWindows = getWindows;
        _debounceTimer = new DispatcherTimer { Interval = DebounceInterval };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            Sweep();
        };
    }

    // Appelé une fois depuis App.xaml.cs, après que toutes les LadaWindow
    // sauvegardées ont été créées : ré-applique le masquage des items déjà
    // absorbés (au cas où Explorer aurait réinitialisé leur position depuis
    // la dernière session), balaie une première fois pour l'existant, puis
    // démarre la surveillance continue.
    public void Start()
    {
        ReapplyHiddenPositions();
        Sweep();

        try
        {
            _watcher = new FileSystemWatcher(_desktopPath) { NotifyFilter = NotifyFilters.FileName };
            _watcher.Created += (_, _) => RestartDebounce();
            _watcher.Renamed += (_, _) => RestartDebounce();
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(DesktopAutoOrganizeWatcher), ex);
        }
    }

    // Les événements FileSystemWatcher arrivent sur un thread threadpool, et
    // une seule action utilisateur en déclenche typiquement plusieurs en
    // rafale (comportement documenté de l'API, même mécanique que les
    // watchers de Drawer existants) : on revient sur l'UI thread puis on
    // debounce avant de rebalayer.
    private void RestartDebounce()
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        });
    }

    // Public : aussi appelé quand une catégorie vient d'être cochée sur un
    // onglet (voir LadaWindow.AutoOrganizeCategoriesChanged), pour absorber
    // l'existant tout de suite plutôt que d'attendre un nouveau fichier.
    public void Sweep()
    {
        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(_desktopPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(DesktopAutoOrganizeWatcher), ex);
            return;
        }

        var windows = _getWindows().ToList();
        var alreadyTracked = new HashSet<string>(
            windows.SelectMany(w => w.Tabs).SelectMany(t => t.Items).Select(i => i.Path),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in entries)
        {
            if (alreadyTracked.Contains(path))
                continue;

            AbsorbIfMatched(windows, path);
        }
    }

    private static void AbsorbIfMatched(List<LadaWindow> windows, string path)
    {
        var category = FileTypeCategorizer.Categorize(path);

        LadaWindow? winnerWindow = null;
        var winnerTabIndex = -1;
        var winnerActivity = DateTime.MinValue;

        foreach (var window in windows)
        {
            var tabs = window.Tabs;
            for (var i = 0; i < tabs.Count; i++)
            {
                if (!tabs[i].AutoOrganizeCategories.Contains(category))
                    continue;

                if (tabs[i].LastActivityUtc > winnerActivity)
                {
                    winnerActivity = tabs[i].LastActivityUtc;
                    winnerWindow = window;
                    winnerTabIndex = i;
                }
            }
        }

        if (winnerWindow is null)
            return;

        var item = new LadaItem
        {
            Path = path,
            DisplayName = Directory.Exists(path) ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path),
            IsDesktopAbsorbed = true
        };

        if (DesktopIconVisibilityService.TryHide(path, out var originalX, out var originalY))
        {
            item.OriginalDesktopX = originalX;
            item.OriginalDesktopY = originalY;
        }

        winnerWindow.AbsorbDesktopItem(winnerTabIndex, item);
    }

    private void ReapplyHiddenPositions()
    {
        foreach (var window in _getWindows())
        {
            foreach (var tab in window.Tabs)
            {
                foreach (var item in tab.Items)
                {
                    if (item.IsDesktopAbsorbed)
                    {
                        DesktopIconVisibilityService.TryHide(item.Path, out _, out _);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _debounceTimer.Stop();
        _watcher?.Dispose();
    }
}
