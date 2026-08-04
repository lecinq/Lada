using System;
using System.Windows;
using System.Windows.Input;
using Lada.Resources;

namespace Lada.Windows;

public partial class TimerDurationPickerWindow : Window
{
    public TimeSpan SelectedDuration { get; private set; }

    public TimerDurationPickerWindow()
    {
        InitializeComponent();
        Title = Strings.TimerDurationPickerTitle;
        HintLabel.Text = Strings.TimerDurationPickerHint;
        Loaded += (_, _) =>
        {
            MinutesBox.Focus();
            MinutesBox.SelectAll();
        };
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void Commit()
    {
        var hours = ParseNonNegativeInt(HoursBox.Text);
        var minutes = ParseNonNegativeInt(MinutesBox.Text);
        var seconds = ParseNonNegativeInt(SecondsBox.Text);
        var totalSeconds = hours * 3600 + minutes * 60 + seconds;

        if (totalSeconds <= 0)
            return; // Ignore Enter on an all-zero duration instead of creating a useless 0s timer.

        SelectedDuration = TimeSpan.FromSeconds(totalSeconds);
        DialogResult = true;
    }

    private static int ParseNonNegativeInt(string text) => int.TryParse(text, out var value) && value >= 0 ? value : 0;
}
