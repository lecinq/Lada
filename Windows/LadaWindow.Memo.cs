using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Lada.Windows;

public partial class LadaWindow
{
    private static readonly TimeSpan MemoAutosaveDebounceInterval = TimeSpan.FromMilliseconds(300);
    private DispatcherTimer? _memoAutosaveTimer;

    private void RenderMemo()
    {
        // Assigning Text below would otherwise raise TextChanged itself and
        // schedule a pointless autosave of the value just loaded from disk.
        MemoTextBox.TextChanged -= MemoTextBox_TextChanged;
        MemoTextBox.Text = _tabs[_activeTabIndex].MemoText;
        MemoTextBox.TextChanged += MemoTextBox_TextChanged;

        // Anderson: follows this lada's own accent, same as item labels and
        // the tab '+' button (ItemLabelAccentOverride, LadaWindow.Theme.cs).
        // SetResourceReference (not ClearValue) restores the static
        // DynamicResource binding for Midnight/Modernism -- MemoTextBox's
        // Foreground is bound directly in XAML, not through a Style, so
        // ClearValue would wipe it permanently instead of restoring it (the
        // same lesson learned earlier this session for MainBorder/
        // TitleTabSeparator).
        if (ItemLabelAccentOverride() is { } accent)
        {
            MemoTextBox.Foreground = accent;
        }
        else
        {
            MemoTextBox.SetResourceReference(TextBox.ForegroundProperty, "TitleTextBrush");
        }
    }

    // Same debounce shape already used for Drawers' FileSystemWatcher
    // (~300ms DispatcherTimer, restarted on every event) -- a keystroke
    // arrives far more often than the user actually pauses, so writing on
    // every single one would be wasteful and isn't needed for correctness.
    private void MemoTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _memoAutosaveTimer?.Stop();
        _memoAutosaveTimer = new DispatcherTimer { Interval = MemoAutosaveDebounceInterval };
        _memoAutosaveTimer.Tick += (_, _) =>
        {
            _memoAutosaveTimer!.Stop();
            _tabs[_activeTabIndex].MemoText = MemoTextBox.Text;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        };
        _memoAutosaveTimer.Start();
    }
}
