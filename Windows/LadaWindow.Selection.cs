using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Models;
using Lada.Native;

namespace Lada.Windows;

public partial class LadaWindow
{
    private readonly HashSet<LadaItem> _selectedItems = new();
    private LadaItem? _selectionAnchor;
    private bool _selectionCollapsePending;

    private void ActivateForSelection()
    {
        // WS_EX_NOACTIVATE means a click never grants real OS keyboard focus
        // on its own — without this, Suppr/Échap (added in Task 3) would
        // silently never reach the window. Same fix already used for title
        // editing (ActivateForTitleEdit) and drag-start.
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(_hwnd);
            ReassertBackdropPairing();
        }
        Focus();
    }

    private void HandleItemSelectionMouseDown(LadaItem item)
    {
        ActivateForSelection();

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (!_selectedItems.Add(item))
            {
                _selectedItems.Remove(item);
            }
            _selectionAnchor = item;
            ApplySelectionVisuals();
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _selectionAnchor is not null)
        {
            SelectRange(_selectionAnchor, item);
            ApplySelectionVisuals();
            return;
        }

        if (_selectedItems.Contains(item) && _selectedItems.Count > 1)
        {
            // Defer collapsing to a single item until mouse-up, so a drag
            // started from here (Task 4) can still carry the whole existing
            // selection instead of shrinking it to just this one item.
            _selectionCollapsePending = true;
            return;
        }

        _selectedItems.Clear();
        _selectedItems.Add(item);
        _selectionAnchor = item;
        ApplySelectionVisuals();
    }

    private void HandleItemSelectionMouseUp(LadaItem item)
    {
        if (!_selectionCollapsePending)
            return;

        _selectionCollapsePending = false;
        _selectedItems.Clear();
        _selectedItems.Add(item);
        _selectionAnchor = item;
        ApplySelectionVisuals();
    }

    private void SelectRange(LadaItem anchor, LadaItem target)
    {
        var anchorIndex = _items.IndexOf(anchor);
        var targetIndex = _items.IndexOf(target);

        if (anchorIndex < 0 || targetIndex < 0)
            return;

        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);

        _selectedItems.Clear();
        for (var i = start; i <= end; i++)
        {
            _selectedItems.Add(_items[i]);
        }
    }

    private void ClearSelection()
    {
        if (_selectedItems.Count == 0)
            return;

        _selectedItems.Clear();
        _selectionAnchor = null;
        ApplySelectionVisuals();
    }

    private void RemoveSelectedItems()
    {
        foreach (var item in _selectedItems.ToList())
        {
            StopWatchingDrawer(item);
            StopClockTimer(item);
            StopDiskTimer(item);
            StopTimerTicking(item);
            RestoreDesktopIconIfAbsorbed(item);
            _items.Remove(item);
        }

        _tabs[_activeTabIndex].LastActivityUtc = DateTime.UtcNow;
        _selectedItems.Clear();
        _selectionAnchor = null;
        ReflowGrid();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LadaWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (_selectedItems.Count == 0)
            return;

        if (e.Key == Key.Delete)
        {
            RemoveSelectedItems();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearSelection();
            e.Handled = true;
        }
    }

    private void ApplySelectionVisuals()
    {
        foreach (var child in ActiveItemsPanel.Children.OfType<Panel>())
        {
            RestoreItemBackground(child);
        }
    }

    private void RestoreItemBackground(Panel stack)
    {
        stack.Background = stack.Tag is LadaItem item && _selectedItems.Contains(item)
            ? (Brush)FindResource("SelectedBackgroundBrush")
            : Brushes.Transparent;
    }

    private Point _marqueeStart;
    private bool _isMarqueeSelecting;

    private void IconGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A widget has exactly one component and nothing else in its grid --
        // there's nothing to marquee-select, and the empty-space drag would
        // otherwise be free to start a selection rectangle right on top of
        // (or around) the one component, which makes no sense here.
        if (_isWidget)
            return;

        // The item's own PreviewMouseLeftButtonDown (tunneling) fires after
        // this one regardless of e.Handled here — MouseButtonDown-family
        // events don't stop routing on Handled the way ordinary routed
        // events do. An explicit hit-test is the only reliable way to tell
        // "empty space" from "landed on an item".
        if (FindStackAtPoint(e.GetPosition(ActiveItemsPanel)) is not null)
            return;

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            ClearSelection();
        }

        _marqueeStart = e.GetPosition(ActiveItemsPanel);
        _isMarqueeSelecting = false;
        ActiveItemsPanel.CaptureMouse();
    }

    private void IconGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !ActiveItemsPanel.IsMouseCaptured)
            return;

        var current = e.GetPosition(ActiveItemsPanel);
        var delta = current - _marqueeStart;

        if (!_isMarqueeSelecting && (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4))
        {
            _isMarqueeSelecting = true;
            ActivateForSelection();
            SelectionMarquee.Visibility = Visibility.Visible;
        }

        if (!_isMarqueeSelecting)
            return;

        var rect = new Rect(_marqueeStart, current);
        Canvas.SetLeft(SelectionMarquee, rect.Left);
        Canvas.SetTop(SelectionMarquee, rect.Top);
        SelectionMarquee.Width = rect.Width;
        SelectionMarquee.Height = rect.Height;

        UpdateMarqueeSelection(rect);
    }

    private void IconGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!ActiveItemsPanel.IsMouseCaptured)
            return;

        ActiveItemsPanel.ReleaseMouseCapture();
        _isMarqueeSelecting = false;
        SelectionMarquee.Visibility = Visibility.Collapsed;
    }

    private void UpdateMarqueeSelection(Rect marqueeRect)
    {
        _selectedItems.Clear();

        foreach (var child in ActiveItemsPanel.Children.OfType<Panel>())
        {
            if (child.Tag is not LadaItem item)
                continue;

            var topLeft = child.TranslatePoint(new Point(0, 0), ActiveItemsPanel);
            var bounds = new Rect(topLeft, child.RenderSize);

            if (marqueeRect.IntersectsWith(bounds))
            {
                _selectedItems.Add(item);
            }
        }

        ApplySelectionVisuals();
    }
}
