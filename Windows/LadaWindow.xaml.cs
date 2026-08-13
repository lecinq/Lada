using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow : Window
{
    private const double TitleBarHeight = 32; // tabs live inside this row now, not a separate one
    private const double IconCellWidth = 72; // item StackPanel Width(64) + Margin(4+4)
    private const double IconCellHeight = 76; // approximate: icon+label+margins; varies slightly if a label wraps to two lines
    private const double IconGridOuterMargin = 32; // IconGrid Margin="16" on all four sides

    private readonly Guid _id;
    private readonly List<LadaTab> _tabs;
    private int _activeTabIndex;
    // Always the same reference as _tabs[_activeTabIndex].Items — reassigned
    // in LadaWindow.Tabs.cs whenever the active tab changes, so every other
    // partial (Sort/DragDrop/Selection/Drawer) can keep mutating it in place
    // without knowing tabs exist.
    private List<LadaItem> _items;
    private HardwareMonitorService _hardwareMonitorService = null!;
    private GmailAuthService? _gmailAuthService;
    private GmailPollingService? _gmailPollingService;
    private Point _titleBarDragStart;
    private bool _isDraggingTitleBar;
    private Vector _pendingDragDelta;
    private bool _isWidget;
    private WidgetChromeManager? _widgetChromeManager;

    public event EventHandler? LayoutChanged;
    public event Action? NewLadaRequested;
    public event Action<WidgetComponentType>? NewWidgetRequested;
    public event Action? DeleteRequested;

    public LadaWindow(LadaLayout layout, ThemeManager themeManager, LocalizationManager localizationManager, HoverFadeManager hoverFadeManager, MagnetismManager magnetismManager, PerspectiveTiltManager perspectiveTiltManager, HudGlowManager hudGlowManager, WidgetChromeManager widgetChromeManager, CustomColorPaletteService customColorPaletteService, HardwareMonitorService hardwareMonitorService, GmailAuthService gmailAuthService, GmailPollingService gmailPollingService, Func<IEnumerable<LadaWindow>> getAllLadaWindows)
    {
        InitializeComponent();

        _id = layout.Id;
        _isWidget = layout.IsWidget;
        _tabs = layout.ResolveTabs();
        _activeTabIndex = Math.Clamp(layout.ActiveTabIndex, 0, _tabs.Count - 1);
        _items = _tabs[_activeTabIndex].Items;

        if (_isWidget)
        {
            ResizeThumb.Visibility = Visibility.Collapsed;
        }

        _magnetismManager = magnetismManager;
        _customColorPaletteService = customColorPaletteService;
        _hardwareMonitorService = hardwareMonitorService;
        _gmailAuthService = gmailAuthService;
        _gmailPollingService = gmailPollingService;
        _getAllLadaWindows = getAllLadaWindows;

        // layout.X/Y/Width/Height are the LOGICAL (visible card) values;
        // this Window's own Left/Top/Width/Height DPs represent the real,
        // HudGlowMargin-padded HWND rect (LadaWindow.HudGlow.cs) instead.
        Left = layout.X - HudGlowMargin;
        Top = layout.Y - HudGlowMargin;
        Width = layout.Width + 2 * HudGlowMargin;
        Height = layout.Height + 2 * HudGlowMargin;
        TitleTextBlock.Text = layout.Title;

        _iconId = layout.IconId;
        _iconColor = layout.IconColor;
        _autoSortEnabled = _tabs[_activeTabIndex].AutoSortEnabled;
        InitializeTheme(themeManager);
        InitializeLocalization(localizationManager);

        _isFolded = layout.IsFolded;
        if (_isFolded)
        {
            // _expandedHeight always holds the padded (Height-DP) value,
            // matching ToggleFold's own bookkeeping (LadaWindow.Fold.cs) --
            // layout.Height is logical, so it needs the same +2*margin
            // conversion as everywhere else layout.Height is loaded.
            _expandedHeight = layout.Height + 2 * HudGlowMargin;
            Height = TitleBarHeight + 2 * HudGlowMargin;
        }

        LocationChanged += (_, _) => LayoutChanged?.Invoke(this, EventArgs.Empty);
        SizeChanged += (_, _) => LayoutChanged?.Invoke(this, EventArgs.Empty);

        InitializeNativeWindowBehavior();
        InitializeHoverFade(hoverFadeManager);
        InitializePerspectiveTilt(perspectiveTiltManager);
        InitializeHudGlow(hudGlowManager);

        RenderAllItems();

        // IconGrid is a WrapPanel: by default it stretches to fill its
        // whole Grid row and always flows children from its own top-left
        // corner, regardless of the child's own alignment -- fine for a
        // real multi-item grid, but for a widget's one and only component
        // it left extra trailing space bottom/right (from the fit-to-content
        // safety margin) with none to match on top/left, reading as visibly
        // off-center. Centering IconGrid itself instead makes it shrink-wrap
        // to its single child and center that within the available card
        // area, splitting the leftover space evenly on every side.
        if (_isWidget)
        {
            IconGrid.HorizontalAlignment = HorizontalAlignment.Center;
            IconGrid.VerticalAlignment = VerticalAlignment.Center;
        }

        UpdateIconButtonVisual();
        InitializeIconPickerOutsideClickAutoClose();
        InitializeCustomColorPicker();

        if (!_isWidget)
        {
            InitializeSortMenu();
            InitializeResizeMenu();
            ResizeThumb.DragStarted += (_, _) =>
            {
                CacheResizeFloors();
                CompositionTarget.Rendering += OnResizeCompositionRendering;
            };
            ResizeThumb.DragCompleted += (_, _) =>
            {
                CompositionTarget.Rendering -= OnResizeCompositionRendering;
                _pendingResizeDelta = default;
            };
            InitializeToDoMemoContextMenus();
            InitializeToDoMemoActivation();
            InitializeToDoListDragTarget();
            RenderTabStrip();
        }

        UpdateTabContentModeVisuals();

        _widgetChromeManager = widgetChromeManager;
        if (_isWidget)
        {
            // Deferred to Loaded rather than run here: this window has no
            // presentation source yet at this point in the constructor
            // (Show() only happens after the constructor returns, back in
            // App.xaml.cs), and ComputeIconGridContentExtent reads each
            // child's real rendered RenderSize/bounds -- measuring before
            // the window has ever actually been laid out against a real
            // surface produced a near-zero content size, which meant every
            // new widget silently fell back to its floor size (~160x40)
            // instead of fitting its actual component.
            Loaded += (_, _) => UpdateWidgetChromeVisibility();
            _widgetChromeManager.Changed += UpdateWidgetChromeVisibility;
            Closed += (_, _) => _widgetChromeManager!.Changed -= UpdateWidgetChromeVisibility;
        }

        Closed += (_, _) =>
        {
            DisposeAllDrawerWatchers();
            DisposeAllClockTimers();
            DisposeAllDiskTimers();
            DisposeAllTimerWidgetTimers();
            DisposeAllTimerAlerts();
            DisposeAllBatteryTimers();
            DisposeAllMemoryTimers();
            DisposeAllCpuUpdates();
            DisposeAllGpuUpdates();
            DisposeAllNetworkUpdates();

            if (_tabs.Any(t => t.ContentMode == TabContentMode.Mail))
            {
                _gmailPollingService?.ReleaseSubscriber();
            }
        };
    }

    public LadaLayout ToLayout()
    {
        _tabs[_activeTabIndex].AutoSortEnabled = _autoSortEnabled;

        return new()
        {
            Id = _id,
            Title = TitleTextBlock.Text,
            X = Left + HudGlowMargin,
            Y = Top + HudGlowMargin,
            Width = Width - 2 * HudGlowMargin,
            Height = (_isFolded ? _expandedHeight : Height) - 2 * HudGlowMargin,
            IsFolded = _isFolded,
            IconId = _iconId,
            IconColor = _iconColor,
            IsWidget = _isWidget,
            ActiveTabIndex = _activeTabIndex,
            Tabs = _tabs.Select(t => new LadaTab
            {
                Id = t.Id,
                Title = t.Title,
                AutoSortEnabled = t.AutoSortEnabled,
                AutoOrganizeCategories = t.AutoOrganizeCategories,
                LastActivityUtc = t.LastActivityUtc,
                Items = t.Items.ToList()
            }).ToList()
        };
    }

    // Toggled globally from the tray menu (App.xaml.cs), applied to every
    // open widget at once. Collapsing TitleBar alone wouldn't remove its
    // reserved space -- TitleBarRow has a fixed pixel Height in XAML, not
    // Auto, so it needs its own Height zeroed out too for the window to
    // actually shrink down to just the component (see EffectiveTitleBarHeight
    // below for why the content-fit math needs the same adjustment).
    private void UpdateWidgetChromeVisibility()
    {
        if (!_isWidget || _widgetChromeManager is null)
            return;

        var visible = _widgetChromeManager.Enabled;
        TitleBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        TitleBarRow.Height = visible ? new GridLength(TitleBarHeight) : new GridLength(0);
        FitWindowToContent();
    }

    // TitleBarHeight is a fixed constant everywhere else (a normal lada
    // always has a title row), but a widget's own reserved title space can
    // be zero when chrome is hidden (UpdateWidgetChromeVisibility) -- both
    // ComputeNeededContentHeight and FitWindowToContent's icon-grid branch
    // need to size against whichever is actually true right now.
    private double EffectiveTitleBarHeight =>
        _isWidget && _widgetChromeManager?.Enabled == false ? 0 : TitleBarHeight;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleFold();
            e.Handled = true;
            return;
        }

        // Screen coordinates (PointToScreen), not e.GetPosition(this) --
        // GetPosition is relative to THIS window, which moves during the
        // drag, so the reference frame itself shifts under every reading.
        // That created a self-referential feedback loop between "where the
        // mouse is relative to me" and "where I just moved myself to" --
        // fact-checked against an isolated repro (a plain window with no
        // transparency/effects/app complexity reproduced the exact same
        // jitter/ghosting using window-relative coordinates, and switching
        // to screen coordinates fixed the great majority of it). Screen
        // coordinates don't move when this window does, so they can't feed
        // back on themselves.
        _titleBarDragStart = PointToScreen(e.GetPosition(this));
        _isDraggingTitleBar = false;
        _dragSnapCandidates = _magnetismManager?.Enabled == true ? GatherSnapCandidates() : null;
        ((UIElement)sender).CaptureMouse();
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = PointToScreen(e.GetPosition(this));
        var delta = current - _titleBarDragStart;

        if (!_isDraggingTitleBar && (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4))
        {
            _isDraggingTitleBar = true;

            if (_magnetismManager?.Enabled != true)
            {
                // Magnétisme off (the default) needs no live snap feedback
                // during the drag, so hand off to WPF's native DragMove()
                // instead of our own manual tracking. DragMove() is tied
                // directly into the OS's own move loop, which tracks a
                // high-refresh display (e.g. 144Hz) smoothly; our manual
                // CompositionTarget.Rendering-based tracking is capped by
                // WPF's own render-thread pacing and can't match that, no
                // matter how well-throttled the position updates are. This
                // is the exact drag call this app used before magnétisme
                // existed -- restored here for the common (magnétisme-off)
                // case; TitleBar_MouseLeftButtonUp already handles the
                // no-op cleanup correctly once DragMove() returns.
                ((UIElement)sender).ReleaseMouseCapture();
                DragMove();
                return;
            }

            CompositionTarget.Rendering += OnDragCompositionRendering;
        }

        if (_isDraggingTitleBar)
        {
            // WPF can raise MouseMove far more often than the screen
            // refreshes (especially with a high-poll-rate mouse). Applying
            // a SetWindowPos-driven move synchronously on every one of those
            // events outran what the screen could actually show.
            // Accumulating the delta here and only applying it once per
            // rendered frame (below, via CompositionTarget.Rendering) keeps
            // window moves in step with the display instead.
            _pendingDragDelta += delta;
            // Re-anchor for the next move so each delta is measured since
            // the last move, not the original mouse-down point -- a snap
            // adjustment moves the window by something other than the raw
            // mouse delta, so re-deriving from a fixed start would drift.
            // PointToScreen returns device (physical) pixels -- see
            // ApplyManualDragMove, which now treats this delta as physical
            // throughout instead of converting it from DIPs.
            _titleBarDragStart = PointToScreen(e.GetPosition(this));
        }
    }

    // Applies at most one accumulated drag move per rendered frame -- see
    // the comment in TitleBar_MouseMove for why this exists.
    private void OnDragCompositionRendering(object? sender, EventArgs e)
    {
        if (_pendingDragDelta == default)
            return;

        ApplyManualDragMove(_pendingDragDelta);
        _pendingDragDelta = default;
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ((UIElement)sender).ReleaseMouseCapture();

        if (_isDraggingTitleBar)
        {
            CompositionTarget.Rendering -= OnDragCompositionRendering;
            _pendingDragDelta = default;
        }

        if (_isDraggingTitleBar)
            return;

        // A click on the title bar background while already editing must
        // commit the in-progress edit, not restart BeginTitleEdit (which
        // would overwrite the just-typed text with the stale committed
        // title).
        if (TitleTextBox.Visibility == Visibility.Visible)
        {
            CommitTitleEdit();
            return;
        }

        // Renaming only starts when the click actually lands on the title
        // text itself, not the icon button, the tab strip, or empty space
        // elsewhere on the title bar -- the bar as a whole stays draggable
        // and double-click-to-fold still works everywhere on it, only
        // rename-on-click is scoped down to the title.
        var position = e.GetPosition(TitleColumn);
        var withinTitle = position.X >= 0 && position.Y >= 0
            && position.X <= TitleColumn.ActualWidth && position.Y <= TitleColumn.ActualHeight;
        if (withinTitle)
        {
            BeginTitleEdit();
        }
    }

    private IDisposable? _titleEditOutsideClickWatch;

    private void BeginTitleEdit()
    {
        ActivateForTitleEdit();

        TitleTextBox.Text = TitleTextBlock.Text;
        TitleTextBlock.Visibility = Visibility.Collapsed;
        TitleTextBox.Visibility = Visibility.Visible;
        TitleTextBox.Focus();
        TitleTextBox.SelectAll();

        // See OutsideClickWatcher: WS_EX_NOACTIVATE means TextBox.LostFocus
        // never fires for a click on a genuinely separate window, so a click
        // outside this lada's own bounds is force-committed manually.
        _titleEditOutsideClickWatch = OutsideClickWatcher.Watch(
            (x, y) => OutsideClickWatcher.IsPointInsideWindow(_hwnd, x, y),
            () => Dispatcher.BeginInvoke(CommitTitleEdit));

        UpdateHoverFadeOpacity();
    }

    private void CommitTitleEdit()
    {
        if (TitleTextBox.Visibility != Visibility.Visible)
            return;

        var newTitle = string.IsNullOrWhiteSpace(TitleTextBox.Text)
            ? TitleTextBlock.Text
            : TitleTextBox.Text.Trim();

        TitleTextBlock.Text = newTitle;
        TitleTextBox.Visibility = Visibility.Collapsed;
        TitleTextBlock.Visibility = Visibility.Visible;
        _titleEditOutsideClickWatch?.Dispose();
        _titleEditOutsideClickWatch = null;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        UpdateHoverFadeOpacity();
    }

    private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e) => CommitTitleEdit();

    private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTitleEdit();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            TitleTextBox.Visibility = Visibility.Collapsed;
            TitleTextBlock.Visibility = Visibility.Visible;
            _titleEditOutsideClickWatch?.Dispose();
            _titleEditOutsideClickWatch = null;
            UpdateHoverFadeOpacity();
            e.Handled = true;
        }
    }

    private double _resizeWidthFloor;
    private double _resizeHeightFloor;

    // The content-size floor (below) can't change mid-drag -- nothing is
    // being added to or removed from the lada while the chevron is held --
    // so it only needs computing once per drag gesture, not on every single
    // DragDelta tick. ComputeNeededContentHeight() forces a full UpdateLayout()
    // pass, which is genuinely expensive; recomputing it per tick (or even
    // batched once per rendered frame, which was tried and made the
    // stutter worse by letting bigger deltas pile up behind each expensive
    // call) outran what a fast mouse drag could keep up with and made the
    // resize appear to "lose" the mouse.
    private void CacheResizeFloors()
    {
        var contentHeightFloor = ComputeNeededContentHeight() + 2 * HudGlowMargin;
        var widthFloor = 160 + 2 * HudGlowMargin;
        if (_tabs[_activeTabIndex].ContentMode == TabContentMode.Icons && _tabs[_activeTabIndex].ViewMode == ItemViewMode.Grid)
        {
            var (contentWidth, _) = ComputeIconGridContentExtent();
            widthFloor = Math.Max(widthFloor, IconGridOuterMargin + contentWidth + GridSizeSafetyMargin + 2 * HudGlowMargin);
        }

        _resizeWidthFloor = widthFloor;
        _resizeHeightFloor = Math.Max(TitleBarHeight + 40 + 2 * HudGlowMargin, contentHeightFloor);
    }

    private Vector _pendingResizeDelta;

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        // Applying Width/Height synchronously on every single DragDelta
        // tick means WPF re-renders the whole lada into a texture on every
        // tick too, while Perspective 3D is actually hosting this window's
        // content (Viewport2DVisual3D, see LadaWindow.PerspectiveTilt.cs) --
        // measured at 8-20ms per application, plenty to make a fast real
        // mouse drag pile up input faster than that can drain, ending in
        // WPF/Windows actually dropping mouse capture outright (confirmed
        // via diagnostic logging: LostMouseCapture fired after 600+ms with
        // zero DragDelta ticks processed). Accumulating and applying at
        // most once per rendered frame caps how often that expensive
        // re-render has to happen -- but that same throttling makes an
        // ordinary, cheap resize (3D off, the default) look choppy for no
        // reason, since there's nothing expensive to protect against there.
        // Only pay for the batching when the window is actually 3D-hosted.
        if (_tiltHostedIn3D)
        {
            _pendingResizeDelta += new Vector(e.HorizontalChange, e.VerticalChange);
            return;
        }

        var newWidth = Math.Max(_resizeWidthFloor, Width + e.HorizontalChange);
        var newHeight = Math.Max(_resizeHeightFloor, Height + e.VerticalChange);
        Width = newWidth;
        Height = newHeight;
    }

    // Applies at most one accumulated resize delta per rendered frame --
    // see the comment in ResizeThumb_DragDelta for why this exists. The
    // floors come from CacheResizeFloors (DragStarted), not recomputed
    // here -- ComputeNeededContentHeight() forces its own full layout
    // pass, and doing that on top of this would defeat the point.
    private void OnResizeCompositionRendering(object? sender, EventArgs e)
    {
        if (_pendingResizeDelta == default)
            return;

        var newWidth = Math.Max(_resizeWidthFloor, Width + _pendingResizeDelta.X);
        var newHeight = Math.Max(_resizeHeightFloor, Height + _pendingResizeDelta.Y);
        Width = newWidth;
        Height = newHeight;
        _pendingResizeDelta = default;
    }

    private static readonly (int Columns, int Rows)[] SizePresets =
    {
        (3, 1), (3, 3), (5, 1), (5, 3), (10, 1), (10, 3)
    };

    private void InitializeResizeMenu()
    {
        var menu = new ContextMenu();

        foreach (var (columns, rows) in SizePresets)
        {
            var menuItem = new MenuItem { Header = $"{columns} x {rows}" };
            menuItem.Click += (_, _) => ApplyGridSizePreset(columns, rows);
            menu.Items.Add(menuItem);
        }

        menu.Items.Add(new Separator());
        var fitToContentItem = new MenuItem { Header = Strings.FitToContentMenuItem };
        fitToContentItem.Click += (_, _) => FitWindowToContent();
        menu.Items.Add(fitToContentItem);

        AttachOutsideClickAutoClose(menu);
        ResizeThumb.ContextMenu = menu;
    }

    // Empty space in the to-do/memo content area gets just "Nouveau lada"/
    // "Supprimer ce lada" -- not the full sort/auto-organize/widget menu
    // IconGrid's empty space has, none of which applies to these modes.
    // The memo box additionally needs Cut/Copy/Paste: overriding
    // TextBox.ContextMenu replaces its built-in editing menu entirely, so
    // basic text editing would otherwise regress.
    // WS_EX_NOACTIVATE means this window never gets OS keyboard focus from
    // an ordinary click, so typing in either box wouldn't work at all
    // without this -- same fix already used for title/tab renaming
    // (BeginTitleEdit/BeginRename). Wired once here since ToDoNewTaskBox
    // and MemoTextBox are static XAML elements, not recreated per render
    // like a to-do task's own inline-edit TextBox (which activates in its
    // own click handler in LadaWindow.ToDoList.cs instead).
    private void InitializeToDoMemoActivation()
    {
        ToDoNewTaskBox.PreviewMouseLeftButtonDown += (_, _) => ActivateForTitleEdit();
        MemoTextBox.PreviewMouseLeftButtonDown += (_, _) => ActivateForTitleEdit();
    }

    private void InitializeToDoMemoContextMenus()
    {
        // Only reachable while the active tab is already in ToDoList mode
        // (ToDoListPanel is only visible then), so the only other modes
        // worth offering are Memo and back-to-Icons -- same live-capture
        // of _activeTabIndex as the equivalent entries on IconGrid's own
        // empty-space menu (LadaWindow.Sort.cs).
        var toDoMenu = new ContextMenu();
        var toDoConvertToMemoItem = new MenuItem { Header = Strings.ConvertTabToMemo };
        toDoConvertToMemoItem.Click += (_, _) => TryConvertTabContentMode(_activeTabIndex, TabContentMode.Memo);
        toDoMenu.Items.Add(toDoConvertToMemoItem);
        var toDoConvertToIconsItem = new MenuItem { Header = Strings.ConvertTabToIcons };
        toDoConvertToIconsItem.Click += (_, _) => TryConvertTabContentMode(_activeTabIndex, TabContentMode.Icons);
        toDoMenu.Items.Add(toDoConvertToIconsItem);
        toDoMenu.Items.Add(new Separator());
        var toDoNewLadaItem = new MenuItem { Header = Strings.NewLada };
        toDoNewLadaItem.Click += (_, _) => NewLadaRequested?.Invoke();
        toDoMenu.Items.Add(toDoNewLadaItem);
        toDoMenu.Items.Add(new Separator());
        var toDoDeleteLadaItem = new MenuItem { Header = Strings.DeleteLadaMenuItem };
        toDoDeleteLadaItem.Click += (_, _) => ConfirmAndRequestDelete();
        toDoMenu.Items.Add(toDoDeleteLadaItem);
        AttachOutsideClickAutoClose(toDoMenu);
        ToDoListPanel.ContextMenu = toDoMenu;

        var memoMenu = new ContextMenu();
        var memoConvertToToDoItem = new MenuItem { Header = Strings.ConvertTabToToDoList };
        memoConvertToToDoItem.Click += (_, _) => TryConvertTabContentMode(_activeTabIndex, TabContentMode.ToDoList);
        memoMenu.Items.Add(memoConvertToToDoItem);
        var memoConvertToIconsItem = new MenuItem { Header = Strings.ConvertTabToIcons };
        memoConvertToIconsItem.Click += (_, _) => TryConvertTabContentMode(_activeTabIndex, TabContentMode.Icons);
        memoMenu.Items.Add(memoConvertToIconsItem);
        memoMenu.Items.Add(new Separator());
        memoMenu.Items.Add(new MenuItem { Header = Strings.CutMenuItem, Command = ApplicationCommands.Cut });
        memoMenu.Items.Add(new MenuItem { Header = Strings.CopyMenuItem, Command = ApplicationCommands.Copy });
        memoMenu.Items.Add(new MenuItem { Header = Strings.PasteMenuItem, Command = ApplicationCommands.Paste });
        memoMenu.Items.Add(new Separator());
        var memoNewLadaItem = new MenuItem { Header = Strings.NewLada };
        memoNewLadaItem.Click += (_, _) => NewLadaRequested?.Invoke();
        memoMenu.Items.Add(memoNewLadaItem);
        memoMenu.Items.Add(new Separator());
        var memoDeleteLadaItem = new MenuItem { Header = Strings.DeleteLadaMenuItem };
        memoDeleteLadaItem.Click += (_, _) => ConfirmAndRequestDelete();
        memoMenu.Items.Add(memoDeleteLadaItem);
        AttachOutsideClickAutoClose(memoMenu);
        MemoTextBox.ContextMenu = memoMenu;
    }

    private void ConfirmAndRequestDelete()
    {
        var confirmation = new ConfirmationWindow(Strings.DeleteLadaMenuItem, Strings.DeleteLadaConfirmationBody) { Owner = this };
        confirmation.ShowDialog();

        if (confirmation.Confirmed)
        {
            DeleteRequested?.Invoke();
        }
    }

    private void MainBorder_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateMainBorderClip();

    // Border.CornerRadius only affects how its own Background/BorderBrush are
    // painted — it does not clip descendant content by default, so a selected
    // item's tint (or anything else near an edge) can visibly poke past the
    // rounded corners. An explicit Clip on the whole border guarantees every
    // descendant respects the current corner radius.
    //
    // CornerRadius itself is DynamicResource-bound and changes with the
    // active theme, but that alone doesn't raise SizeChanged (the window's
    // actual size isn't changing) — so this also gets called from the
    // theme-change handler (LadaWindow.Theme.cs) to keep the clip in sync.
    private void UpdateMainBorderClip() => UpdateMainBorderClip(MainBorder.ActualWidth, MainBorder.ActualHeight);

    // Takes explicit dimensions (rather than always reading
    // MainBorder.ActualWidth/Height) so UpdateTiltGeometry
    // (LadaWindow.PerspectiveTilt.cs) can apply the clip synchronously,
    // using the size it just assigned, instead of waiting for
    // MainBorder's own SizeChanged/layout pass to catch up -- during a
    // fast, continuous chevron drag that pass can lag a frame or more
    // behind, which otherwise left content (e.g. a timer widget) rendering
    // past MainBorder's shrinking edge, uncropped, until the drag ended.
    private void UpdateMainBorderClip(double width, double height)
    {
        var radius = MainBorder.CornerRadius.TopLeft;
        MainBorder.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
    }

    // A handful of extra pixels so the computed size isn't an exact edge-to-edge
    // fit — sub-pixel/border-thickness rounding in WPF's layout pass was enough
    // to wrap one fewer column than intended without this slack (confirmed: a
    // "3 x 3" preset landed at exactly 248px, the bare minimum for 3 columns,
    // and rendered as 2).
    private const double GridSizeSafetyMargin = 16;

    private void ApplyGridSizePreset(int columns, int rows)
    {
        Width = Math.Max(160, IconGridOuterMargin + columns * IconCellWidth + GridSizeSafetyMargin) + 2 * HudGlowMargin;
        Height = Math.Max(TitleBarHeight + 40, TitleBarHeight + IconGridOuterMargin + rows * IconCellHeight + GridSizeSafetyMargin) + 2 * HudGlowMargin;
    }

    // IconGrid.ActualHeight isn't reliable here: when content doesn't fill
    // the whole row (an empty or lightly-populated tab), the WrapPanel just
    // stretches to fill whatever height its Grid row currently has —
    // circularly reflecting the window's OWN current size — instead of its
    // content's real size. Confirmed by measuring: growing the window from
    // that inflated reading made the next read grow again by the same
    // amount, forever (reproduced as "adding/removing a tab keeps growing
    // the lada a little every time"). An explicit unconstrained Measure
    // gives the content's true desired height regardless of how much room
    // it was actually given, whether that's more (content overflows) or
    // less (content is sparse) than the row's current allotment.
    private Panel ActiveItemsPanel =>
        _tabs[_activeTabIndex].ContentMode == TabContentMode.Icons && _tabs[_activeTabIndex].ViewMode == ItemViewMode.List
            ? ItemListPanel
            : IconGrid;

    private void EnsureContentFits()
    {
        if (_isFolded)
            return;

        var neededHeight = ComputeNeededContentHeight() + 2 * HudGlowMargin;
        if (neededHeight > Height)
        {
            Height = neededHeight;
        }
    }

    // Used by EnsureContentFits (auto grow-only, all content modes) and by
    // FitWindowToContent outside of icon grid mode. Measured at the CURRENT
    // width (not an unconstrained one): a WrapPanel's needed height depends
    // on the width it's already been given -- measuring at infinite width
    // would collapse everything onto a single unwrapped row instead of
    // reporting the wrapped height at today's width. Icon grid mode's
    // "Fit to content" uses ComputeIconGridContentExtent below instead,
    // which fits width and height together from one rendered snapshot.
    private double ComputeNeededContentHeight()
    {
        UpdateLayout();
        ActiveItemsPanel.Measure(new Size(ActiveItemsPanel.ActualWidth, double.PositiveInfinity));
        return EffectiveTitleBarHeight + ActiveItemsPanel.DesiredSize.Height + IconGridOuterMargin + GridSizeSafetyMargin;
    }

    // Not a Measure() -- WrapPanel has no independent "desired width" to ask
    // for the way it does a desired height at a given width, and re-Measuring
    // it a second time (at a new width, after already resizing once this
    // same call) turned out to be exactly that kind of two-step: fitting
    // width first, then asking the panel to re-measure height at the new
    // width, produced a stale/inflated number in practice. Reading back
    // where the CURRENTLY rendered children actually ended up -- both their
    // rightmost and bottommost edges, in one pass, before anything is
    // resized -- sidesteps a second layout pass entirely. Since every item
    // already fits within that span at the current width, shrinking to
    // exactly that span can't force any new wrapping, so the row/column
    // layout the user is looking at right now is preserved, just with the
    // trailing empty space trimmed off on both edges.
    private (double Width, double Height) ComputeIconGridContentExtent()
    {
        double maxRight = 0, maxBottom = 0;
        foreach (UIElement child in IconGrid.Children)
        {
            var bounds = child.TransformToAncestor(IconGrid).TransformBounds(new Rect(child.RenderSize));
            maxRight = Math.Max(maxRight, bounds.Right);
            maxBottom = Math.Max(maxBottom, bounds.Bottom);
        }
        return (maxRight, maxBottom);
    }

    // Unlike EnsureContentFits (called automatically after every content
    // change, grow-only so an in-progress drag or a momentarily-short tab
    // never gets yanked smaller on its own), this is the user-requested
    // "Fit to content" action: shrinks just as readily as it grows, and
    // fits width too, not just height -- but only in icon grid mode; list
    // rows and the to-do/memo surfaces already span the full width by
    // design, so there's no trailing space to trim there.
    private void FitWindowToContent()
    {
        if (_isFolded)
            return;

        UpdateLayout();

        if (_tabs[_activeTabIndex].ContentMode == TabContentMode.Icons && _tabs[_activeTabIndex].ViewMode == ItemViewMode.Grid)
        {
            var (contentWidth, contentHeight) = ComputeIconGridContentExtent();
            Width = Math.Max(160, IconGridOuterMargin + contentWidth + GridSizeSafetyMargin) + 2 * HudGlowMargin;
            Height = Math.Max(EffectiveTitleBarHeight + 40, EffectiveTitleBarHeight + contentHeight + IconGridOuterMargin + GridSizeSafetyMargin) + 2 * HudGlowMargin;
        }
        else
        {
            Height = Math.Max(TitleBarHeight + 40, ComputeNeededContentHeight()) + 2 * HudGlowMargin;
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }
}
