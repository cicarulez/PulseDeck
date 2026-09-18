using System.Security.Cryptography;

namespace PulseDeck.Agent.Providers;

/// <summary>Use Terminal's installed Tux asset for explicitly WSL-labelled tabs.</summary>
public sealed class TerminalIcon
{
    // Terminal's built-in Linux profile icon. Resolve locally, never copy its assets into the app.
    private const string LinuxIcon = "{9acb9455-ca41-5af7-950f-6bca1bc9722f}";
    private string? cachedExecutable;
    private ApplicationIcon? cachedIcon;
    private DateTimeOffset expires;

    public ApplicationIcon? Read(string executable, string title)
    {
        // The window API exposes no selected profile ID. This is deliberately a
        // title convention, not a guess based on other WSL processes running.
        var text = title.Trim();
        if (!text.Equals("WSL", StringComparison.OrdinalIgnoreCase)
            && !(text.Length > 3 && text.StartsWith("WSL", StringComparison.OrdinalIgnoreCase)
                && (char.IsWhiteSpace(text[3]) || text[3] is '-' or ':')))
            return null;
        if (string.Equals(cachedExecutable, executable, StringComparison.OrdinalIgnoreCase)
            && DateTimeOffset.UtcNow < expires) return cachedIcon;
        cachedExecutable = executable;
        cachedIcon = null;
        try
        {
            var directory = Path.Combine(Path.GetDirectoryName(executable)!, "ProfileIcons");
            foreach (var suffix in new[] { ".scale-200.png", ".scale-100.png", ".png" })
            {
                var path = Path.Combine(directory, LinuxIcon + suffix);
                if (!File.Exists(path)) continue;
                using var file = File.OpenRead(path);
                if (file.Length is < 8 or > 524288) continue;
                var bytes = new byte[(int)file.Length];
                file.ReadExactly(bytes);
                if (!bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) continue;
                cachedIcon = new(Convert.ToHexString(SHA256.HashData(bytes)), bytes);
                break;
            }
        }
        catch { /* Missing/unreadable resources fall back to the executable icon. */ }
        expires = DateTimeOffset.UtcNow.AddSeconds(cachedIcon is null ? 5 : 60);
        return cachedIcon;
    }
}
