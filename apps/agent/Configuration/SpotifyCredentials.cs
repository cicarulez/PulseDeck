using System.Security.Cryptography;
using System.Text.Json;
using PulseDeck.Core;

namespace PulseDeck.Agent.Configuration;

public sealed class SpotifyCredentials(ConfigStore config) : ISpotifyCredentials
{
    private readonly string path = Path.Combine(config.DirectoryPath, "spotify.credentials");
    public SpotifySecret? Read()
    {
        if (!File.Exists(path)) return null;
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<SpotifySecret>(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Save(SpotifySecret value)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            File.WriteAllBytes(path + ".tmp", ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser));
            File.Move(path + ".tmp", path, true);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Clear() { if (File.Exists(path)) File.Delete(path); }
}
