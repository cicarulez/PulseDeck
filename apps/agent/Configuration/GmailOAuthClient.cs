using System.Reflection;
using System.Text.Json;

namespace PulseDeck.Agent.Configuration;

// Desktop OAuth clients identify the application. User refresh tokens are never
// included in this resource; they remain in DPAPI-protected runtime credentials.
public static class GmailOAuthClient
{
    public static GmailSecret? ReadBundled()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PulseDeck.GmailOAuthClient.json");
        if (stream is null) return null;
        using var document = JsonDocument.Parse(stream);
        return Parse(document.RootElement);
    }

    public static GmailSecret Parse(JsonElement root)
    {
        var installed = root.GetProperty("installed");
        var id = installed.GetProperty("client_id").GetString();
        var key = installed.GetProperty("client_secret").GetString();
        if (id is null || !id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)
            || id.Length > 256 || string.IsNullOrWhiteSpace(key) || key.Length > 256)
            throw new FormatException("Invalid Desktop OAuth client.");
        return new(id, key);
    }
}
