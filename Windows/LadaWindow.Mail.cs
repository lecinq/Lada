using System.Windows.Controls;

namespace Lada.Windows;

public partial class LadaWindow
{
    private void RenderMail()
    {
        MailContentPanel.Children.Clear();
        MailContentPanel.Children.Add(new TextBlock { Text = "Mail (à venir)" });
    }
}
