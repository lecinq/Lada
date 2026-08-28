using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Lada.Resources;

namespace Lada.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Title = Strings.AboutMenuItem;
        MessageText.Text = Strings.AboutMessage;
        OpenSourceLabel.Text = Strings.AboutOpenSource;
        WeatherAttributionLink.Inlines.Add(Strings.WeatherDataAttribution);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // WindowStyle="None" drops the native title bar along with its
    // built-in drag-to-move -- ordinary WPF windows (unlike LadaWindow,
    // which is pinned via WS_EX_NOACTIVATE and moves through its own
    // native-level handling) can just call DragMove() here instead.
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
        e.Handled = true;
    }
}
