using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private const double HoverFadeRestOpacity = 0.4;
    private static readonly TimeSpan HoverFadeDuration = TimeSpan.FromMilliseconds(200);

    private HoverFadeManager? _hoverFadeManager;
    private bool _isMouseOverWindow;
    private bool _isContextMenuOpen;
    private bool _isIconPickerOpen;
    private bool _isResizing;
    private bool _isOverlayActive;

    // Wires mouse-over tracking, every source of "the user is actively
    // using this lada right now" (context menus, the icon/color picker,
    // dragging, resizing, renaming), and the global on/off toggle, into a
    // single recomputed target opacity animated via BeginAnimation.
    //
    // Window-level ContextMenuOpening/ContextMenuClosing are used instead
    // of hooking each individual ContextMenu's own Opened/Closed: both are
    // bubbling routed events (RoutingStrategy.Bubble), so subscribing once
    // on the root Window catches every context menu anywhere in this
    // lada's visual tree (items, tabs, widgets, empty-space menu, resize
    // chevron menu) without needing to touch every menu-creation call site
    // individually, and automatically covers any future one too.
    private void InitializeHoverFade(HoverFadeManager hoverFadeManager)
    {
        _hoverFadeManager = hoverFadeManager;
        _hoverFadeManager.Changed += UpdateHoverFadeOpacity;
        Closed += (_, _) => _hoverFadeManager.Changed -= UpdateHoverFadeOpacity;

        MouseEnter += (_, _) =>
        {
            _isMouseOverWindow = true;
            UpdateHoverFadeOpacity();
        };
        MouseLeave += (_, _) =>
        {
            _isMouseOverWindow = false;
            UpdateHoverFadeOpacity();
        };

        ContextMenuOpening += (_, _) =>
        {
            _isContextMenuOpen = true;
            UpdateHoverFadeOpacity();
        };
        ContextMenuClosing += (_, _) =>
        {
            _isContextMenuOpen = false;
            UpdateHoverFadeOpacity();
        };

        PickerPopup.Opened += (_, _) =>
        {
            _isIconPickerOpen = true;
            UpdateHoverFadeOpacity();
        };
        PickerPopup.Closed += (_, _) =>
        {
            _isIconPickerOpen = false;
            UpdateHoverFadeOpacity();
        };

        // ToDoTaskList.IsKeyboardFocusWithin and MemoTextBox.IsKeyboardFocused
        // are read live inside UpdateHoverFadeOpacity itself, but a recompute
        // still has to be triggered at the moment focus is actually lost
        // (e.g. Alt-Tabbing away without touching the mouse) -- otherwise
        // nothing else would re-evaluate opacity at that point.
        ToDoTaskList.LostKeyboardFocus += (_, _) => UpdateHoverFadeOpacity();
        MemoTextBox.LostKeyboardFocus += (_, _) => UpdateHoverFadeOpacity();

        UpdateHoverFadeOpacity();
    }

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isResizing = true;
        UpdateHoverFadeOpacity();
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isResizing = false;
        UpdateHoverFadeOpacity();
    }

    // Called from SetOverlayMode (LadaWindow.Native.cs) whenever overlay
    // mode is toggled on or off.
    private void SetOverlayActiveForHoverFade(bool active)
    {
        _isOverlayActive = active;
        UpdateHoverFadeOpacity();
    }

    // Called from BeginTitleEdit/CommitTitleEdit/the Escape-cancel path
    // (LadaWindow.xaml.cs): starting or ending a rename doesn't always
    // coincide with a MouseEnter/MouseLeave (e.g. the mouse can drift off
    // the lada while the user is still typing), so those transitions need
    // their own explicit recompute to catch that case correctly.
    private void UpdateHoverFadeOpacity()
    {
        if (_hoverFadeManager is not { Enabled: true } || _isOverlayActive)
        {
            AnimateOpacityTo(1.0);
            return;
        }

        var stayOpaque = _isMouseOverWindow
            || _isContextMenuOpen
            || _isIconPickerOpen
            || _isResizing
            || _isDraggingTitleBar
            || _isDraggingItem
            || _isDraggingToDoTask
            || TitleTextBox.Visibility == Visibility.Visible
            || ToDoTaskList.IsKeyboardFocusWithin
            || MemoTextBox.IsKeyboardFocused;

        AnimateOpacityTo(stayOpaque ? 1.0 : HoverFadeRestOpacity);
    }

    private void AnimateOpacityTo(double target)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(target, HoverFadeDuration));
    }
}
