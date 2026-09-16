namespace PulseDeck.Core;

/// <summary>Stable profile transitions; background media never displaces a foreground game.</summary>
public sealed class ProfileSelector
{
    private string current = "desktop";
    private string candidate = "desktop";
    private DateTimeOffset candidateSince;

    public string Select(DeckConfig config, string foregroundProcess, bool musicPlaying, DateTimeOffset now)
    {
        if (config.ProfileMode != "auto")
        {
            current = candidate = config.ProfileMode;
            candidateSince = now;
            return current;
        }
        var process = Path.GetFileNameWithoutExtension(foregroundProcess);
        var next = config.GameProcesses.Any(p => string.Equals(Path.GetFileNameWithoutExtension(p), process, StringComparison.OrdinalIgnoreCase))
            ? "gaming" : musicPlaying ? "music" : "desktop";
        if (candidate != next) { candidate = next; candidateSince = now; }
        if (current != candidate && now - candidateSince >= TimeSpan.FromSeconds(config.ProfileDelaySeconds)) current = candidate;
        return current;
    }
}
