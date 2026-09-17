using System.Security.Cryptography;
using System.Text;

namespace PulseDeck.Agent.Configuration;

public sealed class SteamGridCredentials
{
    private readonly string path;
    private readonly object gate = new();
    private string? key;
    private int revision;
    public int Revision { get { lock (gate) return revision; } }
    public bool Configured { get { lock (gate) return key is not null; } }
    public string? Read() { lock (gate) return key; }
    public SteamGridCredentials(ConfigStore config)
    {
        path = Path.Combine(config.DirectoryPath, "steamgriddb.credentials");
        try
        {
            if (File.Exists(path)) key = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
        }
        catch { key = null; }
    }
    public void Save(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Chiave API non valida.");
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        lock (gate)
        {
            File.WriteAllBytes(path + ".tmp", encrypted);
            File.Move(path + ".tmp", path, true);
            key = value; revision++;
        }
    }
    public void Clear()
    {
        lock (gate) { if (File.Exists(path)) File.Delete(path); key = null; revision++; }
    }
}
public sealed record SteamGridKeyRequest(string Key);
