namespace PulseDeck.Core;

/// <summary>Stable profile transitions; background media never displaces a foreground game.</summary>
public sealed class ProfileSelector
{
    private string current = "desktop";
    private string candidate = "desktop";
    private DateTimeOffset candidateSince;
    private bool lastMusicWasSpotify;
    public bool SpotifyTransition { get; private set; }

    public static bool IsGame(DeckConfig config, string foregroundProcess)
    {
        var process = Path.GetFileNameWithoutExtension(foregroundProcess);
        return config.GameProcesses.Any(p => string.Equals(Path.GetFileNameWithoutExtension(p), process, StringComparison.OrdinalIgnoreCase));
    }

    public string Select(DeckConfig config, string foregroundProcess, bool musicPlaying, DateTimeOffset now, bool? recognizedGame = null, MediaSnapshot? media = null)
    {
        SpotifyTransition = false;
        if (config.ProfileMode != "auto")
        {
            lastMusicWasSpotify = false;
            current = candidate = config.ProfileMode;
            candidateSince = now;
            return current;
        }
        if (musicPlaying) lastMusicWasSpotify = media is not null && SpotifyLyrics.IsSpotify(media);
        var grace = current == "music" && lastMusicWasSpotify && media is not null
            && (SpotifyLyrics.IsSpotify(media) || media.Status is "idle" or "unavailable" && string.IsNullOrEmpty(media.App));
        var next = (recognizedGame ?? IsGame(config, foregroundProcess))
            ? "gaming" : musicPlaying ? "music" : "desktop";
        if (candidate != next) { candidate = next; candidateSince = now; }
        var delay = next == "desktop" && grace ? Math.Max(8, config.ProfileDelaySeconds) : config.ProfileDelaySeconds;
        if (current != candidate && now - candidateSince >= TimeSpan.FromSeconds(delay)) current = candidate;
        SpotifyTransition = current == "music" && grace && !(recognizedGame ?? IsGame(config, foregroundProcess))
            && (!SpotifyLyrics.Eligible(media!) || !musicPlaying && media!.PositionSeconds >= media.DurationSeconds - 1);
        return current;
    }
}
