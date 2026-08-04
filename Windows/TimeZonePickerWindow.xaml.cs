using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Lada.Resources;

namespace Lada.Windows;

public partial class TimeZonePickerWindow : Window
{
    private readonly TimeZoneInfo[] _allZones = TimeZoneInfo.GetSystemTimeZones().ToArray();

    public TimeZoneInfo? SelectedTimeZone { get; private set; }

    public TimeZonePickerWindow()
    {
        InitializeComponent();
        Title = Strings.TimeZonePickerTitle;
        Loaded += (_, _) =>
        {
            PopulateResults(_allZones);
            SearchBox.Focus();
        };
    }

    private void PopulateResults(TimeZoneInfo[] zones)
    {
        ResultsList.ItemsSource = zones.Select(z => z.DisplayName).ToList();
        ResultsList.Tag = zones;
        if (zones.Length > 0)
        {
            ResultsList.SelectedIndex = 0;
        }
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var filter = SearchBox.Text;
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allZones
            : _allZones.Where(z => z.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

        PopulateResults(filtered);
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Commit();

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
        else if (e.Key == Key.Down || e.Key == Key.Up)
        {
            // Arrow keys work while typing in the search box, not just when
            // the list itself has focus.
            var count = ResultsList.Items.Count;
            if (count == 0)
                return;

            var delta = e.Key == Key.Down ? 1 : -1;
            ResultsList.SelectedIndex = Math.Clamp(ResultsList.SelectedIndex + delta, 0, count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
    }

    private void Commit()
    {
        if (ResultsList.Tag is TimeZoneInfo[] zones && ResultsList.SelectedIndex >= 0 && ResultsList.SelectedIndex < zones.Length)
        {
            SelectedTimeZone = zones[ResultsList.SelectedIndex];
            DialogResult = true;
        }
    }
}
