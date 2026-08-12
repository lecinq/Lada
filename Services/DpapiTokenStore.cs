using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Util.Store;

namespace Lada.Services;

// Replaces the SDK's default FileDataStore, which writes tokens as
// plaintext JSON (confirmed against Google.Apis.Util.Store.FileDataStore's
// own docs while writing the mail widget spec) -- this encrypts every
// value with Windows DPAPI (current-user scope) before it ever touches
// disk, so another account on the same machine can't read the file.
public sealed class DpapiTokenStore : IDataStore
{
    private readonly string _folder;

    public DpapiTokenStore(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public Task StoreAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PathFor(key), encrypted);
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
            return Task.FromResult(default(T)!);

        var encrypted = File.ReadAllBytes(path);
        var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(plainBytes);
        return Task.FromResult(JsonSerializer.Deserialize<T>(json)!);
    }

    public Task DeleteAsync<T>(string key)
    {
        var path = PathFor(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        foreach (var file in Directory.GetFiles(_folder))
        {
            File.Delete(file);
        }
        return Task.CompletedTask;
    }

    // One file per key, named by its SHA256 hash rather than the raw key
    // string, so a key containing characters invalid in a filename (the
    // SDK's own keys are typically "<type>-<user>", but this makes no
    // assumption about that) never breaks File.WriteAllBytes.
    private string PathFor(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_folder, hash + ".dat");
    }
}
