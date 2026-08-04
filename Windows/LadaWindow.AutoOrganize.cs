using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private readonly Dictionary<FileCategory, MenuItem> _autoOrganizeMenuItems = new();

    private static readonly (FileCategory Category, Func<string> Label)[] AutoOrganizeCategoryOptions =
    {
        (FileCategory.Folder, () => Strings.AutoOrganizeCategoryFolder),
        (FileCategory.Document, () => Strings.AutoOrganizeCategoryDocument),
        (FileCategory.Image, () => Strings.AutoOrganizeCategoryImage),
        (FileCategory.Video, () => Strings.AutoOrganizeCategoryVideo),
        (FileCategory.Audio, () => Strings.AutoOrganizeCategoryAudio),
        (FileCategory.Executable, () => Strings.AutoOrganizeCategoryExecutable),
        (FileCategory.Other, () => Strings.AutoOrganizeCategoryOther)
    };

    // Appelé une fois depuis InitializeSortMenu (LadaWindow.Sort.cs), même
    // menu clic-droit vide que "Trier par type"/tailles prédéfinies.
    private void InitializeAutoOrganizeMenu(ContextMenu contextMenu)
    {
        var submenu = new MenuItem { Header = Strings.AutoOrganizeSubmenu };

        foreach (var (category, label) in AutoOrganizeCategoryOptions)
        {
            var categoryItem = new MenuItem
            {
                Header = label(),
                IsCheckable = true,
                IsChecked = _tabs[_activeTabIndex].AutoOrganizeCategories.Contains(category)
            };
            categoryItem.Click += (_, _) =>
            {
                var categories = _tabs[_activeTabIndex].AutoOrganizeCategories;
                if (categoryItem.IsChecked)
                {
                    if (!categories.Contains(category))
                    {
                        categories.Add(category);
                    }
                }
                else
                {
                    categories.Remove(category);
                }

                LayoutChanged?.Invoke(this, EventArgs.Empty);
                AutoOrganizeCategoriesChanged?.Invoke();
            };

            _autoOrganizeMenuItems[category] = categoryItem;
            submenu.Items.Add(categoryItem);
        }

        contextMenu.Items.Add(submenu);
    }

    // Vue en lecture seule des onglets, consommée par DesktopAutoOrganizeWatcher
    // (Services/) pour trouver quel onglet, sur quelle fenêtre, doit recevoir
    // un fichier du bureau qui correspond à une catégorie active.
    public IReadOnlyList<LadaTab> Tabs => _tabs;

    // Levé quand une catégorie d'auto-organisation est cochée/décochée sur
    // un onglet ; App.xaml.cs s'y abonne pour redéclencher un balayage
    // immédiat du bureau (voir spec : "cocher déclenche le balayage
    // immédiat").
    public event Action? AutoOrganizeCategoriesChanged;

    // Appelé par DesktopAutoOrganizeWatcher quand un fichier du bureau
    // correspond à une catégorie active sur tabIndex. Ne suppose jamais que
    // tabIndex est l'onglet actuellement affiché : le rendu ne se déclenche
    // que si c'est le cas, sinon l'item attend d'être affiché au prochain
    // changement d'onglet (même logique que MoveItemsToTab pour une cible
    // non active).
    public void AbsorbDesktopItem(int tabIndex, LadaItem item)
    {
        var targetTab = _tabs[tabIndex];
        var (column, row) = FindNextFreeCell(targetTab.Items);
        item.Column = column;
        item.Row = row;
        targetTab.Items.Add(item);
        targetTab.LastActivityUtc = DateTime.UtcNow;

        if (tabIndex == _activeTabIndex)
        {
            RenderSingleItem(item);
            EnsureContentFits();
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    // Restaure la position réelle sur le bureau d'un item absorbé automatiquement.
    // Ne fait rien pour un item glissé manuellement (IsDesktopAbsorbed == false) :
    // celui-ci n'a jamais eu d'icône masquée à restaurer.
    private void RestoreDesktopIconIfAbsorbed(LadaItem item)
    {
        if (item.IsDesktopAbsorbed && item.OriginalDesktopX is { } x && item.OriginalDesktopY is { } y)
        {
            DesktopIconVisibilityService.Restore(item.Path, x, y);
        }
    }

    // Appelé par App.xaml.cs juste avant de fermer une fenêtre supprimée
    // (DeleteRequested) : sans ça, supprimer tout un lada laisserait les
    // icônes qu'il avait absorbées invisibles pour toujours sur le bureau.
    public void RestoreAllAbsorbedDesktopIcons()
    {
        foreach (var tab in _tabs)
        {
            foreach (var item in tab.Items)
            {
                RestoreDesktopIconIfAbsorbed(item);
            }
        }
    }

    // Rafraîchit l'état coché des 7 cases de catégorie pour refléter l'onglet
    // qui vient de devenir actif — même principe que la synchronisation de
    // _autoSortMenuItem.IsChecked dans ActivateTab (LadaWindow.Tabs.cs).
    private void RefreshAutoOrganizeMenuChecks()
    {
        if (_autoOrganizeMenuItems.Count == 0)
            return;

        var activeCategories = _tabs[_activeTabIndex].AutoOrganizeCategories;
        foreach (var (category, menuItem) in _autoOrganizeMenuItems)
        {
            menuItem.IsChecked = activeCategories.Contains(category);
        }
    }
}
