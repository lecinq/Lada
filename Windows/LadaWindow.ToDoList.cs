using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Models;
using Lada.Native;

namespace Lada.Windows;

public partial class LadaWindow
{
    private Point _toDoTaskDragStart;
    private bool _isDraggingToDoTask;

    private void RenderToDoList()
    {
        ToDoTaskList.Children.Clear();

        foreach (var task in _tabs[_activeTabIndex].ToDoTasks)
        {
            ToDoTaskList.Children.Add(BuildToDoTaskRow(task));
        }

        // ToDoNewTaskBox is a static XAML element (not rebuilt per task row
        // like BuildToDoTaskRow's own text), so it needs the same Anderson
        // per-lada-accent handling applied directly here.
        if (ItemLabelAccentOverride() is { } accent)
        {
            ToDoNewTaskBox.Foreground = accent;
        }
        else
        {
            ToDoNewTaskBox.SetResourceReference(TextBox.ForegroundProperty, "TitleTextBrush");
        }
    }

    // Wired once (ToDoTaskScrollViewer is a static XAML element, not
    // recreated per render): catches drags released in the empty space
    // below the last row (or into an empty list entirely), which no row's
    // own DragOver/Drop would ever see, and treats it as "append at the
    // end". A row's own DragOver sets e.Handled = true, so this never fires
    // for drags directly over a row -- only the empty space around them.
    private void InitializeToDoListDragTarget()
    {
        ToDoTaskScrollViewer.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(typeof(ToDoTaskEntry)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;

            if (e.Data.GetDataPresent(typeof(ToDoTaskEntry)))
            {
                ShowToDoInsertionIndicator(_tabs[_activeTabIndex].ToDoTasks.Count);
            }
        };
        ToDoTaskScrollViewer.DragLeave += (_, _) => ClearToDoInsertionIndicator();
        ToDoTaskScrollViewer.Drop += (_, e) => HandleToDoTaskReorderDrop(e, _tabs[_activeTabIndex].ToDoTasks.Count);
    }

    private Border BuildToDoTaskRow(ToDoTaskEntry task)
    {
        // Anderson: every text element in the row follows this lada's own
        // accent, same as item labels and the tab '+' button already do
        // (ItemLabelAccentOverride, LadaWindow.Theme.cs). Midnight/Modernism
        // keep the existing static theme-wide brushes, unchanged.
        var accentOverride = ItemLabelAccentOverride();

        var row = new Border
        {
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 0, 2),
            Tag = task,
            AllowDrop = true
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox { IsChecked = task.IsChecked, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Grid.SetColumn(checkBox, 0);

        var text = new TextBlock
        {
            Text = task.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Cursor = Cursors.Hand,
            Foreground = accentOverride ?? (Brush)FindResource(task.IsChecked ? "SecondaryTextBrush" : "TitleTextBrush"),
            TextDecorations = task.IsChecked ? TextDecorations.Strikethrough : null
        };
        Grid.SetColumn(text, 1);

        var textBox = new TextBox
        {
            Text = task.Text,
            Visibility = Visibility.Collapsed,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = accentOverride ?? (Brush)FindResource("TitleTextBrush"),
            FontFamily = (FontFamily)FindResource("LadaFontFamily"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBox, 1);

        var deleteButton = new TextBlock
        {
            Text = "✕",
            Cursor = Cursors.Hand,
            Foreground = accentOverride ?? (Brush)FindResource("SecondaryTextBrush"),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(deleteButton, 2);

        void RefreshTextVisual()
        {
            text.Text = task.Text;
            text.Foreground = accentOverride ?? (Brush)FindResource(task.IsChecked ? "SecondaryTextBrush" : "TitleTextBrush");
            text.TextDecorations = task.IsChecked ? TextDecorations.Strikethrough : null;
        }

        checkBox.Click += (_, _) =>
        {
            task.IsChecked = checkBox.IsChecked == true;
            RefreshTextVisual();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        };

        void CommitEdit()
        {
            task.Text = string.IsNullOrWhiteSpace(textBox.Text) ? task.Text : textBox.Text.Trim();
            RefreshTextVisual();
            textBox.Visibility = Visibility.Collapsed;
            text.Visibility = Visibility.Visible;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            UpdateHoverFadeOpacity();
        }

        text.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2)
                return;

            // WS_EX_NOACTIVATE means this window never gets OS keyboard
            // focus from an ordinary click, so typing wouldn't reach the
            // TextBox at all without this -- same fix already used for
            // title/tab renaming (BeginTitleEdit/BeginRename).
            ActivateForTitleEdit();

            textBox.Text = task.Text;
            text.Visibility = Visibility.Collapsed;
            textBox.Visibility = Visibility.Visible;
            textBox.Focus();
            textBox.SelectAll();
            e.Handled = true;
        };

        textBox.LostFocus += (_, _) => CommitEdit();
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                textBox.Text = task.Text;
                textBox.Visibility = Visibility.Collapsed;
                text.Visibility = Visibility.Visible;
                UpdateHoverFadeOpacity();
                e.Handled = true;
            }
        };

        deleteButton.MouseLeftButtonDown += (_, e) =>
        {
            _tabs[_activeTabIndex].ToDoTasks.Remove(task);
            RenderToDoList();
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };

        AttachToDoTaskDragSource(row, task);

        row.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(typeof(ToDoTaskEntry)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;

            if (e.Data.GetDataPresent(typeof(ToDoTaskEntry)))
            {
                ShowToDoInsertionIndicator(ComputeToDoDropIndex(row, task, e.GetPosition(row).Y));
            }
        };
        row.Drop += (_, e) => HandleToDoTaskReorderDrop(e, ComputeToDoDropIndex(row, task, e.GetPosition(row).Y));

        grid.Children.Add(checkBox);
        grid.Children.Add(text);
        grid.Children.Add(textBox);
        grid.Children.Add(deleteButton);
        row.Child = grid;

        return row;
    }

    // Deliberately NOT a reuse of AttachItemDragSource (LadaWindow.DragDrop.cs)
    // -- that one is tightly coupled to IconGrid's 2D WrapPanel coordinate
    // space, List<LadaItem> as the drag payload, and multi-selection, none
    // of which applies to this vertical single-column task list. Same
    // PATTERN (DragDrop.DoDragDrop + a movement threshold), simpler data.
    private void AttachToDoTaskDragSource(Border row, ToDoTaskEntry task)
    {
        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _toDoTaskDragStart = e.GetPosition(ToDoTaskList);
            _isDraggingToDoTask = false;
        };

        row.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDraggingToDoTask)
                return;

            var current = e.GetPosition(ToDoTaskList);
            var delta = current - _toDoTaskDragStart;

            if (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4)
            {
                _isDraggingToDoTask = true;
                UpdateHoverFadeOpacity();

                // Same WS_EX_NOACTIVATE/OLE-drag-drop reliability fix already
                // used for item reordering (see AttachItemDragSource in
                // LadaWindow.DragDrop.cs): DoDragDrop is documented to behave
                // unreliably when the drag source isn't the foreground window.
                if (_hwnd != IntPtr.Zero)
                    NativeMethods.SetForegroundWindow(_hwnd);

                row.Opacity = 0.4;
                DragDrop.DoDragDrop(row, task, DragDropEffects.Move);
                row.Opacity = 1.0;

                _isDraggingToDoTask = false;
                UpdateHoverFadeOpacity();
            }
        };
    }

    // Which data index the drop would land at if released right now: the
    // top of a row means "insert before it", the bottom means "insert
    // after it" -- this is what lets the live indicator (and the eventual
    // drop) land exactly at the top when dragging a bottom item above the
    // very first row, not just swap with whatever it's over.
    //
    // Hysteresis, not a flat 50/50 split: a plain midpoint check flickered
    // between "before" and "after" on ordinary mouse jitter near a row's
    // center, since even holding the mouse still involves a couple of
    // pixels of tremor. Once the indicator is already showing "after" this
    // row, the cursor has to move back above the top 20% to flip to
    // "before" again (and symmetrically the other way) -- a wide ~60% dead
    // zone in the middle where the decision doesn't change once made.
    private int ComputeToDoDropIndex(Border row, ToDoTaskEntry task, double cursorY)
    {
        var tasks = _tabs[_activeTabIndex].ToDoTasks;
        var rowIndex = tasks.IndexOf(task);
        var beforeIndex = rowIndex;
        var afterIndex = rowIndex + 1;

        if (row.ActualHeight <= 0)
            return beforeIndex;

        var ratio = cursorY / row.ActualHeight;

        return _toDoInsertionTargetIndex == afterIndex
            ? (ratio < 0.20 ? beforeIndex : afterIndex)
            : (ratio > 0.80 ? afterIndex : beforeIndex);
    }

    private Border? _toDoInsertionIndicator;
    private int? _toDoInsertionTargetIndex;

    private void ShowToDoInsertionIndicator(int targetIndex)
    {
        // Skip the remove+insert entirely when nothing actually changed --
        // re-inserting the indicator every single DragOver (dozens per
        // second) shifts every row below it by a couple pixels each time,
        // which was itself feeding back into the hysteresis check above via
        // slightly different cursor-relative-to-row positions next frame.
        if (_toDoInsertionTargetIndex == targetIndex)
            return;

        if (_toDoInsertionIndicator is not null)
        {
            ToDoTaskList.Children.Remove(_toDoInsertionIndicator);
        }

        _toDoInsertionIndicator = new Border
        {
            Height = 2,
            Margin = new Thickness(2, 1, 2, 1),
            Background = (Brush)FindResource("AccentBrush")
        };

        var insertAt = Math.Clamp(targetIndex, 0, ToDoTaskList.Children.Count);
        ToDoTaskList.Children.Insert(insertAt, _toDoInsertionIndicator);
        _toDoInsertionTargetIndex = targetIndex;
    }

    private void ClearToDoInsertionIndicator()
    {
        _toDoInsertionTargetIndex = null;

        if (_toDoInsertionIndicator is not null)
        {
            ToDoTaskList.Children.Remove(_toDoInsertionIndicator);
            _toDoInsertionIndicator = null;
        }
    }

    private void HandleToDoTaskReorderDrop(DragEventArgs e, int targetIndex)
    {
        e.Handled = true;
        _toDoInsertionIndicator = null;
        _toDoInsertionTargetIndex = null;

        if (!e.Data.GetDataPresent(typeof(ToDoTaskEntry)))
            return;

        var draggedTask = (ToDoTaskEntry)e.Data.GetData(typeof(ToDoTaskEntry))!;
        var tasks = _tabs[_activeTabIndex].ToDoTasks;
        var draggedIndex = tasks.IndexOf(draggedTask);
        if (draggedIndex == -1)
            return;

        tasks.RemoveAt(draggedIndex);

        // Removing the dragged task shifts every later index down by one,
        // so a target position computed before the removal needs adjusting
        // when it was after the task's original spot.
        var adjustedTarget = targetIndex > draggedIndex ? targetIndex - 1 : targetIndex;
        tasks.Insert(Math.Clamp(adjustedTarget, 0, tasks.Count), draggedTask);

        RenderToDoList();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToDoNewTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        var text = ToDoNewTaskBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        _tabs[_activeTabIndex].ToDoTasks.Add(new ToDoTaskEntry { Text = text });
        ToDoNewTaskBox.Clear();
        RenderToDoList();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
