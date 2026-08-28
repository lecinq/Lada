using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Resources;

namespace Lada.Windows;

// Themed replacement for MessageBox.Show(... YesNo ...), matching the same
// custom-chrome technique as AboutWindow (DynamicResource-bound to whichever
// of the three app themes is active) instead of a generic Windows dialog.
public partial class ConfirmationWindow : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmationWindow(string title, string message)
    {
        InitializeComponent();
        TitleTextBlock.Text = title;
        MessageText.Text = message;
        ConfirmButtonText.Text = Strings.ConfirmButton;
        CancelButtonText.Text = Strings.CancelButton;
        Loaded += (_, _) => ApplyConfirmButtonContrast();
    }

    private void ApplyConfirmButtonContrast()
    {
        // ConfirmationWindow lives in its own resource tree, so explicitly
        // resolve the per-lada AccentBrush from its owner (especially
        // important for Anderson's synchronized/custom colors).
        if (Owner is FrameworkElement owner && owner.TryFindResource("AccentBrush") is Brush ownerAccent)
            ConfirmButton.Background = ownerAccent;

        if (ConfirmButton.Background is SolidColorBrush background)
            ConfirmButtonText.Foreground = ColorContrast.ForegroundBrush(background.Color);
        else
            ConfirmButtonText.Foreground = Brushes.White;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Confirmed = true;
            Close();
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void CancelButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void ConfirmButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Confirmed = true;
        Close();
        e.Handled = true;
    }
}
