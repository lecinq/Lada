using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lada.Models;
using Lada.Resources;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private bool _mailSubscribed;
    private bool _mailReauthNeeded;

    private void RenderMail()
    {
        if (_gmailAuthService is null || _gmailPollingService is null)
            return;

        if (!_mailSubscribed)
        {
            _mailSubscribed = true;
            _gmailPollingService.EnsureStarted();
            _gmailPollingService.MailsUpdated += OnMailsUpdated;
            _gmailPollingService.ReauthRequired += OnMailReauthRequired;
        }

        RenderMailContent();
    }

    private void OnMailsUpdated() => Dispatcher.Invoke(RenderMailContent);

    private void OnMailReauthRequired()
    {
        _mailReauthNeeded = true;
        Dispatcher.Invoke(RenderMailContent);
    }

    private async void RenderMailContent()
    {
        MailContentPanel.Children.Clear();

        if (_gmailAuthService is null)
            return;

        if (!_gmailAuthService.HasClientSecretFile())
        {
            MailContentPanel.Children.Add(new TextBlock
            {
                Text = Strings.MailNoClientSecret,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            });
            return;
        }

        var isConnected = await _gmailAuthService.IsConnectedAsync();
        if (!isConnected)
        {
            MailContentPanel.Children.Add(BuildConnectButton());
            return;
        }

        if (_mailReauthNeeded)
        {
            MailContentPanel.Children.Add(BuildReauthBanner());
        }

        if (_gmailPollingService!.LastKnownMails.Count == 0)
        {
            MailContentPanel.Children.Add(new TextBlock
            {
                Text = Strings.MailLoadingIndicator,
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            });
            return;
        }

        foreach (var mail in _gmailPollingService.LastKnownMails)
        {
            MailContentPanel.Children.Add(BuildMailRow(mail));
        }
    }

    private Button BuildConnectButton()
    {
        var button = new Button { Content = Strings.MailConnectButton, Padding = new Thickness(8, 4, 8, 4) };
        button.Click += async (_, _) => await SignInAndRefreshAsync();
        return button;
    }

    private Border BuildReauthBanner()
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(new TextBlock
        {
            Text = Strings.MailReauthRequired,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var button = new Button { Content = Strings.MailReauthButton, Padding = new Thickness(6, 2, 6, 2) };
        button.Click += async (_, _) => await SignInAndRefreshAsync();
        stack.Children.Add(button);

        return new Border { Child = stack };
    }

    // Shared by the connect button and the reauth banner's button. A prior
    // version of this had no try/catch at all -- an exception from
    // SignInAsync (or from GoogleWebAuthorizationBroker deep inside it)
    // would have gone completely unobserved (no crash, no log, no visible
    // change), which is exactly what made an earlier bug here silent.
    private async Task SignInAndRefreshAsync()
    {
        try
        {
            await _gmailAuthService!.SignInAsync(System.Threading.CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(SignInAndRefreshAsync), ex);
        }

        _mailReauthNeeded = false;
        RenderMailContent();
    }

    private Border BuildMailRow(MailSummary mail)
    {
        var row = new Border { Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 4), Cursor = Cursors.Hand };

        var stack = new StackPanel();

        var fromAndSubject = new TextBlock
        {
            Text = $"{mail.From} — {mail.Subject}",
            FontWeight = mail.IsUnread ? FontWeights.Bold : FontWeights.Normal,
            Foreground = (Brush)FindResource("TitleTextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(fromAndSubject);

        var snippet = new TextBlock
        {
            Text = mail.Snippet,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(snippet);

        row.Child = stack;
        row.MouseLeftButtonDown += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"https://mail.google.com/mail/u/0/#all/{mail.GmailMessageId}",
                UseShellExecute = true
            });
        };

        return row;
    }
}
