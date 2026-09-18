using System.Security.Cryptography;
using System.Text;
using PulseDeck.Core;

namespace PulseDeck.Agent.Configuration;

public sealed class CalendarCredentials
{
    private readonly string path;
    private readonly object gate = new();
    private string? url;
    public bool Configured { get { lock (gate) return url is not null; } }
    public string? Read() { lock (gate) return url; }
    public CalendarCredentials(ConfigStore config)
    {
        path = Path.Combine(config.DirectoryPath, "calendar.credentials");
        try
        {
            if (!File.Exists(path)) return;
            var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            try { var value = Encoding.UTF8.GetString(clear); if (GoogleCalendarUrl.IsValid(value)) url = value; }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }
        catch { url = null; }
    }
    public void Save(string value)
    {
        if (!GoogleCalendarUrl.IsValid(value)) throw new ArgumentException("Incolla l’indirizzo iCal di Google Calendar.");
        var clear = Encoding.UTF8.GetBytes(value);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(clear); }
        lock (gate)
        {
            File.WriteAllBytes(path + ".tmp", encrypted); File.Move(path + ".tmp", path, true); url = value;
        }
    }
    public void Clear() { lock (gate) { if (File.Exists(path)) File.Delete(path); url = null; } }
}
public sealed record CalendarLinkRequest(string Url);
