using System.Security.Cryptography;
using System.Text.Json;

namespace PulseDeck.Agent.Configuration;

public sealed record GmailSecret(string ClientId, string ClientSecret, string? RefreshToken = null);
public sealed class GmailCredentials(ConfigStore config)
{
    private readonly string path = Path.Combine(config.DirectoryPath, "gmail.credentials");
    public GmailSecret? Read()
    {
        if (!File.Exists(path)) return null;
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<GmailSecret>(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Save(GmailSecret value)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            var encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path + ".tmp", encrypted);
            File.Move(path + ".tmp", path, true);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Clear() { if (File.Exists(path)) File.Delete(path); }
}
