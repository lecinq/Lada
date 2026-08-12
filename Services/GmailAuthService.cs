using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Util.Store;

namespace Lada.Services;

// One shared connection for the whole app (spec: single Google account,
// not per-tab/per-lada). Wraps GoogleWebAuthorizationBroker, which already
// implements the loopback+PKCE flow this app would otherwise hand-roll
// (see the mail widget design spec's "OAuth & Gmail access" section for
// why the SDK was chosen over a hand-rolled implementation).
public sealed class GmailAuthService
{
    private static readonly string[] Scopes = { GmailService.Scope.GmailReadonly };
    private const string UserId = "lada-user";

    private readonly string _clientSecretPath;
    private readonly IDataStore _dataStore;

    public event Action? Changed;

    public GmailAuthService(string appDataFolder)
    {
        _clientSecretPath = Path.Combine(appDataFolder, "google_client_secret.json");
        _dataStore = new DpapiTokenStore(Path.Combine(appDataFolder, "google_tokens"));
    }

    public bool HasClientSecretFile() => File.Exists(_clientSecretPath);

    public async Task<bool> IsConnectedAsync()
    {
        if (!GoogleClientSecretFile.TryLoad(_clientSecretPath, out _))
            return false;

        var token = await _dataStore.GetAsync<TokenResponse>($"Google.Apis.Auth.OAuth2.Responses.TokenResponse-{UserId}");
        return token is not null;
    }

    // Opens the system browser to Google's consent screen and waits for the
    // user to approve -- GoogleWebAuthorizationBroker handles the loopback
    // listener and the token exchange internally.
    public async Task<bool> SignInAsync(CancellationToken cancellationToken)
    {
        if (!GoogleClientSecretFile.TryLoad(_clientSecretPath, out var secrets))
            return false;

        await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets!,
            Scopes,
            UserId,
            cancellationToken,
            _dataStore);

        Changed?.Invoke();
        return true;
    }

    public async Task SignOutAsync()
    {
        await _dataStore.ClearAsync();
        Changed?.Invoke();
    }

    // Returns a ready-to-use GmailService, refreshing the access token via
    // the stored refresh token if needed (UserCredential does this
    // automatically). Throws if not connected -- callers (GmailPollingService)
    // are expected to have checked IsConnectedAsync first.
    public async Task<GmailService> GetGmailServiceAsync(CancellationToken cancellationToken)
    {
        if (!GoogleClientSecretFile.TryLoad(_clientSecretPath, out var secrets))
            throw new InvalidOperationException("Google client secret file missing.");

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets!,
            Scopes,
            UserId,
            cancellationToken,
            _dataStore);

        return new GmailService(new Google.Apis.Services.BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Lada"
        });
    }
}
