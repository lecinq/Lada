using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;

namespace Lada.Services;

// Reads the small JSON file the user creates by hand from their Google
// Cloud OAuth client (see docs/superpowers/plans/2026-08-12-mail-widget.md,
// Task 0) -- {"client_id": "...", "client_secret": "..."} -- rather than
// depending on the SDK's own credentials.json loader, keeping this app in
// full control of the on-disk format.
public static class GoogleClientSecretFile
{
    // Explicit [JsonPropertyName] is required here -- PropertyNameCaseInsensitive
    // only ignores case, it does NOT bridge snake_case ("client_id") to
    // PascalCase ("ClientId"); without these attributes, deserialization
    // "succeeds" but silently leaves both properties at their "" default,
    // which made TryLoad return false for every file, indistinguishable
    // from a missing/malformed file (root-caused via SignInAsync returning
    // false in under 2ms -- far too fast to have attempted the OAuth flow).
    private sealed class Payload
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = "";

        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; } = "";
    }

    public static bool TryLoad(string path, out ClientSecrets? secrets)
    {
        secrets = null;

        if (!File.Exists(path))
            return false;

        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<Payload>(json);

        if (payload is null || string.IsNullOrWhiteSpace(payload.ClientId) || string.IsNullOrWhiteSpace(payload.ClientSecret))
            return false;

        secrets = new ClientSecrets { ClientId = payload.ClientId, ClientSecret = payload.ClientSecret };
        return true;
    }
}
