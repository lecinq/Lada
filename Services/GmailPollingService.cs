using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Lada.Models;

namespace Lada.Services;

// Shared by every Mail-mode tab across every lada (spec: single account,
// one shared connection), same reasoning as HardwareMonitorService being
// shared across every CPU/GPU widget. Lazily started: EnsureStarted is a
// no-op after the first call, and polling only runs while at least one
// Mail-mode tab is currently on screen (ReleaseSubscriber stops it once
// the last one goes away).
public sealed class GmailPollingService : IDisposable
{
    private const int MaxResults = 15;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private GmailAuthService? _authService;
    private DispatcherTimer? _timer;
    private int _subscriberCount;
    private CancellationTokenSource? _cts;

    public IReadOnlyList<MailSummary> LastKnownMails { get; private set; } = Array.Empty<MailSummary>();
    public bool HasCompletedInitialFetch { get; private set; }
    public bool LastUpdateFailed { get; private set; }

    public event Action? MailsUpdated;
    public event Action? ReauthRequired;

    public void Configure(GmailAuthService authService)
    {
        _authService = authService;
    }

    public void EnsureStarted()
    {
        _subscriberCount++;

        if (_timer is not null)
            return;

        _cts = new CancellationTokenSource();
        _timer = new DispatcherTimer { Interval = PollInterval };
        _timer.Tick += async (_, _) => await PollOnceAsync();
        _timer.Start();

        _ = PollOnceAsync();
    }

    public void ReleaseSubscriber()
    {
        _subscriberCount = Math.Max(0, _subscriberCount - 1);
        if (_subscriberCount > 0)
            return;

        _timer?.Stop();
        _timer = null;
        _cts?.Cancel();
        _cts = null;
    }

    public Task RefreshNowAsync() => PollOnceAsync();

    private async Task PollOnceAsync()
    {
        if (_authService is null || _cts is null)
            return;

        try
        {
            if (!await _authService.IsConnectedAsync())
                return;

            var gmailService = await _authService.GetGmailServiceAsync(_cts.Token);

            var listRequest = gmailService.Users.Messages.List("me");
            listRequest.LabelIds = "INBOX";
            listRequest.MaxResults = MaxResults;
            var listResponse = await listRequest.ExecuteAsync(_cts.Token);

            var summaries = new List<MailSummary>();
            foreach (var messageRef in listResponse.Messages ?? Enumerable.Empty<Google.Apis.Gmail.v1.Data.Message>())
            {
                var getRequest = gmailService.Users.Messages.Get("me", messageRef.Id);
                getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                getRequest.MetadataHeaders = new[] { "From", "Subject" };
                var fullMessage = await getRequest.ExecuteAsync(_cts.Token);
                summaries.Add(MailSummaryBuilder.FromMessage(fullMessage));
            }

            ApplyFetchResult(success: true, summaries);
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 401)
        {
            ApplyReauthRequired();
        }
        catch (TokenResponseException ex) when (
            string.Equals(ex.Error?.Error, "invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            // A revoked/expired refresh token fails before the Gmail request is
            // sent, so it isn't a GoogleApiException/401. Treat it as the same
            // reconnectable authentication state instead of leaving the UI on
            // "Loading..." forever.
            Logger.LogError(nameof(PollOnceAsync), ex);
            ApplyReauthRequired();
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(PollOnceAsync), ex);
            ApplyFetchResult(success: false, new List<MailSummary>());
        }
    }

    // Split out from PollOnceAsync so the "keep last-known list on failure"
    // rule (spec: never blank the list for a one-off failure) is testable
    // without a live Gmail connection -- see GmailPollingServiceStateTests.
    public void ApplyFetchResult(bool success, IReadOnlyList<MailSummary> mails)
    {
        HasCompletedInitialFetch = true;
        LastUpdateFailed = !success;

        if (success)
            LastKnownMails = mails;

        MailsUpdated?.Invoke();
    }

    private void ApplyReauthRequired()
    {
        HasCompletedInitialFetch = true;
        LastUpdateFailed = false;
        ReauthRequired?.Invoke();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _cts?.Cancel();
    }
}
