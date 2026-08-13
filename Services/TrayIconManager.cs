using System;
using System.Drawing;
using System.Windows.Forms;
using Lada.Models;
using Lada.Resources;

namespace Lada.Services;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _midnightMenuItem;
    private readonly ToolStripMenuItem _modernismMenuItem;
    private readonly ToolStripMenuItem _andersonMenuItem;
    private readonly ToolStripMenuItem _frenchMenuItem;
    private readonly ToolStripMenuItem _englishMenuItem;
    private readonly ToolStripMenuItem _hoverFadeMenuItem;
    private readonly ToolStripMenuItem _magnetismMenuItem;
    private readonly ToolStripMenuItem _perspectiveTiltMenuItem;
    private readonly ToolStripMenuItem _hudGlowMenuItem;
    private readonly ToolStripMenuItem _newLadaItem;
    private readonly ToolStripMenuItem _newWidgetItem;
    private readonly ToolStripMenuItem _widgetChromeMenuItem;
    private readonly ToolStripMenuItem _showAllItem;
    private readonly ToolStripMenuItem _arrangeItem;
    private readonly ToolStripMenuItem _themeMenu;
    private readonly ToolStripMenuItem _languageMenu;
    private readonly ToolStripMenuItem _aboutItem;
    private readonly ToolStripMenuItem _quitItem;

    public event Action? NewLadaRequested;
    public event Action? ShowAllRequested;
    public event Action? ArrangeRequested;
    public event Action? ExitRequested;
    public event Action<AppTheme>? ThemeChangeRequested;
    public event Action<AppLanguage>? LanguageChangeRequested;
    public event Action<bool>? HoverFadeToggleRequested;
    public event Action<bool>? MagnetismToggleRequested;
    public event Action<bool>? PerspectiveTiltToggleRequested;
    public event Action<bool>? HudGlowToggleRequested;
    public event Action<WidgetComponentType>? NewWidgetRequested;
    public event Action<bool>? WidgetChromeToggleRequested;
    public event Action? AboutRequested;

    public TrayIconManager()
    {
        _midnightMenuItem = new ToolStripMenuItem("Midnight", null, (_, _) => ThemeChangeRequested?.Invoke(AppTheme.Midnight));
        _modernismMenuItem = new ToolStripMenuItem("Modernism", null, (_, _) => ThemeChangeRequested?.Invoke(AppTheme.Modernism));
        _andersonMenuItem = new ToolStripMenuItem("Anderson", null, (_, _) => ThemeChangeRequested?.Invoke(AppTheme.Anderson));
        _themeMenu = new ToolStripMenuItem();
        _themeMenu.DropDownItems.Add(_midnightMenuItem);
        _themeMenu.DropDownItems.Add(_modernismMenuItem);
        _themeMenu.DropDownItems.Add(_andersonMenuItem);

        // Language names are always shown in their own language, regardless
        // of which one is currently active, so these two labels never change.
        _frenchMenuItem = new ToolStripMenuItem("Français", null, (_, _) => LanguageChangeRequested?.Invoke(AppLanguage.French));
        _englishMenuItem = new ToolStripMenuItem("English", null, (_, _) => LanguageChangeRequested?.Invoke(AppLanguage.English));
        _languageMenu = new ToolStripMenuItem();
        _languageMenu.DropDownItems.Add(_frenchMenuItem);
        _languageMenu.DropDownItems.Add(_englishMenuItem);

        _hoverFadeMenuItem = new ToolStripMenuItem("", null, (_, _) => { });
        _hoverFadeMenuItem.CheckOnClick = true;
        _hoverFadeMenuItem.Click += (_, _) => HoverFadeToggleRequested?.Invoke(_hoverFadeMenuItem.Checked);

        _magnetismMenuItem = new ToolStripMenuItem("", null, (_, _) => { });
        _magnetismMenuItem.CheckOnClick = true;
        _magnetismMenuItem.Click += (_, _) => MagnetismToggleRequested?.Invoke(_magnetismMenuItem.Checked);

        _perspectiveTiltMenuItem = new ToolStripMenuItem("", null, (_, _) => { });
        _perspectiveTiltMenuItem.CheckOnClick = true;
        _perspectiveTiltMenuItem.Click += (_, _) => PerspectiveTiltToggleRequested?.Invoke(_perspectiveTiltMenuItem.Checked);

        _hudGlowMenuItem = new ToolStripMenuItem("", null, (_, _) => { });
        _hudGlowMenuItem.CheckOnClick = true;
        _hudGlowMenuItem.Click += (_, _) => HudGlowToggleRequested?.Invoke(_hudGlowMenuItem.Checked);

        _newLadaItem = new ToolStripMenuItem("", null, (_, _) => NewLadaRequested?.Invoke());

        _newWidgetItem = new ToolStripMenuItem();
        foreach (WidgetComponentType type in Enum.GetValues<WidgetComponentType>())
        {
            var widgetTypeItem = new ToolStripMenuItem(Strings.WidgetComponentLabel(type), null, (_, _) => NewWidgetRequested?.Invoke(type)) { Tag = type };
            _newWidgetItem.DropDownItems.Add(widgetTypeItem);
        }

        _widgetChromeMenuItem = new ToolStripMenuItem("", null, (_, _) => { });
        _widgetChromeMenuItem.CheckOnClick = true;
        _widgetChromeMenuItem.Checked = true;
        _widgetChromeMenuItem.Click += (_, _) => WidgetChromeToggleRequested?.Invoke(_widgetChromeMenuItem.Checked);

        _showAllItem = new ToolStripMenuItem("", null, (_, _) => ShowAllRequested?.Invoke());
        _arrangeItem = new ToolStripMenuItem("", null, (_, _) => ArrangeRequested?.Invoke());
        _aboutItem = new ToolStripMenuItem("", null, (_, _) => AboutRequested?.Invoke());
        _quitItem = new ToolStripMenuItem("", null, (_, _) => ExitRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_newLadaItem);
        menu.Items.Add(_newWidgetItem);
        menu.Items.Add(_showAllItem);
        menu.Items.Add(_arrangeItem);
        menu.Items.Add(_themeMenu);
        menu.Items.Add(_languageMenu);
        menu.Items.Add(_hoverFadeMenuItem);
        menu.Items.Add(_magnetismMenuItem);
        menu.Items.Add(_perspectiveTiltMenuItem);
        menu.Items.Add(_hudGlowMenuItem);
        menu.Items.Add(_widgetChromeMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_aboutItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_quitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = BuildTrayIcon(),
            Visible = true,
            Text = "Lada",
            ContextMenuStrip = menu
        };

        RefreshTexts();
    }

    public void SetActiveTheme(AppTheme theme)
    {
        _midnightMenuItem.Checked = theme == AppTheme.Midnight;
        _modernismMenuItem.Checked = theme == AppTheme.Modernism;
        _andersonMenuItem.Checked = theme == AppTheme.Anderson;

        ApplyThemeToTrayMenu(theme);
    }

    private void ApplyThemeToTrayMenu(AppTheme theme)
    {
        var colors = ThemeSurfaceColors.ForTheme(theme);
        if (_notifyIcon.ContextMenuStrip is { } menu)
        {
            menu.Renderer = new ThemedToolStripRenderer(colors);
            // Submenus (Theme, Language) are separate ToolStripDropDownMenu
            // instances, each with their own Renderer -- they don't inherit
            // the root menu's renderer automatically, so each needs its own
            // instance to pick up rounded corners, colors, and the check glyph.
            _themeMenu.DropDown.Renderer = new ThemedToolStripRenderer(colors);
            _languageMenu.DropDown.Renderer = new ThemedToolStripRenderer(colors);
            ApplyForeColorRecursive(menu.Items, colors.Text);
        }
    }

    private static void ApplyForeColorRecursive(ToolStripItemCollection items, Color textColor)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = textColor;
            if (item is ToolStripMenuItem { HasDropDownItems: true } menuItem)
            {
                ApplyForeColorRecursive(menuItem.DropDownItems, textColor);
            }
        }
    }

    public void SetActiveLanguage(AppLanguage language)
    {
        _frenchMenuItem.Checked = language == AppLanguage.French;
        _englishMenuItem.Checked = language == AppLanguage.English;
    }

    public void SetHoverFadeEnabled(bool enabled)
    {
        _hoverFadeMenuItem.Checked = enabled;
    }

    public void SetMagnetismEnabled(bool enabled)
    {
        _magnetismMenuItem.Checked = enabled;
    }

    public void SetPerspectiveTiltEnabled(bool enabled)
    {
        _perspectiveTiltMenuItem.Checked = enabled;
    }

    public void SetHudGlowEnabled(bool enabled)
    {
        _hudGlowMenuItem.Checked = enabled;
    }

    public void SetWidgetChromeEnabled(bool enabled)
    {
        _widgetChromeMenuItem.Checked = enabled;
    }

    public void RefreshTexts()
    {
        _newLadaItem.Text = Strings.NewLada;
        _newWidgetItem.Text = Strings.NewWidgetTrayMenuItem;
        foreach (ToolStripMenuItem item in _newWidgetItem.DropDownItems)
        {
            item.Text = Strings.WidgetComponentLabel((WidgetComponentType)item.Tag!);
        }
        _showAllItem.Text = Strings.ShowAllLadas;
        _arrangeItem.Text = Strings.ArrangeLadasMenuItem;
        _themeMenu.Text = Strings.ThemeMenu;
        _languageMenu.Text = Strings.LanguageMenu;
        _hoverFadeMenuItem.Text = Strings.HoverFadeMenuItem;
        _magnetismMenuItem.Text = Strings.MagnetismMenuItem;
        _perspectiveTiltMenuItem.Text = Strings.PerspectiveTiltMenuItem;
        _hudGlowMenuItem.Text = Strings.HudGlowMenuItem;
        _widgetChromeMenuItem.Text = Strings.WidgetChromeMenuItem;
        _aboutItem.Text = Strings.AboutMenuItem;
        _quitItem.Text = Strings.Quit;
    }

    private static Icon BuildTrayIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(ColorTranslator.FromHtml("#5B8DEF"));
            g.FillEllipse(brush, 1, 1, 14, 14);
        }

        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
