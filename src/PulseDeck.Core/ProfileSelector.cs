namespace PulseDeck.Core;

/// <summary>Stable profile transitions; background media never displaces a foreground game.</summary>
public sealed class ProfileSelector
{
    private string current = "desktop";
    private string candidate = "desktop";
    private DateTimeOffset candidateSince;

    public static bool IsGame(DeckConfig config, string foregroundProcess)
    {
        var process = Path.GetFileNameWithoutExtension(foregroundProcess);
        return config.GameProcesses.Any(p => string.Equals(Path.GetFileNameWithoutExtension(p), process, StringComparison.OrdinalIgnoreCase));
    }

    public string Select(DeckConfig config, string foregroundProcess, bool musicPlaying, DateTimeOffset now, bool? recognizedGame = null)
    {
        if (config.ProfileMode != "auto")
        {
            current = candidate = config.ProfileMode;
            candidateSince = now;
            return current;
        }
        var next = (recognizedGame ?? IsGame(config, foregroundProcess))
            ? "gaming" : musicPlaying ? "music" : "desktop";
        if (candidate != next) { candidate = next; candidateSince = now; }
        if (current != candidate && now - candidateSince >= TimeSpan.FromSeconds(config.ProfileDelaySeconds)) current = candidate;
        return current;
    }
}
