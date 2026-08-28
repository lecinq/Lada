using System;
using System.Diagnostics;
using System.IO;
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
            _gmailPollingService.MailsUpdated += OnMailsUpdated;
            _gmailPollingService.ReauthRequired += OnMailReauthRequired;
            _gmailPollingService.EnsureStarted();
        }

        RenderMailContent();
    }

    private void OnMailsUpdated() => Dispatcher.Invoke(() =>
    {
        // A successful shared poll also clears the reconnect banner in every
        // Mail tab, not only in the window whose button initiated the login.
        if (!_gmailPollingService!.LastUpdateFailed)
            _mailReauthNeeded = false;

        RenderMailContent();
    });

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
            MailContentPanel.Children.Add(BuildGmailSetupPanel());
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
            if (_gmailPollingService!.LastKnownMails.Count == 0)
                return;
        }

        if (_gmailPollingService!.LastUpdateFailed)
        {
            MailContentPanel.Children.Add(new TextBlock
            {
                Text = Strings.MailLastUpdateFailed,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (_gmailPollingService.LastKnownMails.Count == 0)
                return;
        }

        if (_gmailPollingService.LastKnownMails.Count == 0)
        {
            MailContentPanel.Children.Add(new TextBlock
            {
                Text = !_gmailPollingService.HasCompletedInitialFetch
                    ? Strings.MailLoadingIndicator
                    : Strings.MailNoMessages,
                Foreground = (Brush)FindResource("SecondaryTextBrush")
            });
            return;
        }

        foreach (var mail in _gmailPollingService.LastKnownMails)
        {
            MailContentPanel.Children.Add(BuildMailRow(mail));
        }
    }

    private StackPanel BuildGmailSetupPanel()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = Strings.MailNoClientSecret,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        });

        var button = new Button
        {
            Content = Strings.OpenGmailConfigurationFolder,
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) =>
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Lada");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        };
        panel.Children.Add(button);
        return panel;
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
        var reconnecting = _mailReauthNeeded;

        try
        {
            // AuthorizeAsync otherwise reloads the revoked refresh token and
            // fails with invalid_grant again without opening the browser.
            if (reconnecting)
                await _gmailAuthService!.SignOutAsync();

            var signedIn = await _gmailAuthService!.SignInAsync(System.Threading.CancellationToken.None);
            if (!signedIn)
                return;

            _mailReauthNeeded = false;
            RenderMailContent();
            await _gmailPollingService!.RefreshNowAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(SignInAndRefreshAsync), ex);
            _mailReauthNeeded = reconnecting;
            RenderMailContent();
        }
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
