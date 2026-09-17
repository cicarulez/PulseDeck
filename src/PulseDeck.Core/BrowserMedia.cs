using System.Text.RegularExpressions;

namespace PulseDeck.Core;

public sealed record BrowserMedia(string VideoId, string Title, string Artist, bool Playing,
    double PositionSeconds, double DurationSeconds, double PlaybackRate)
{
    public bool IsValid => VideoId is not null && Regex.IsMatch(VideoId, @"\A[A-Za-z0-9_-]{11}\z")
        && !string.IsNullOrWhiteSpace(Title) && Title.Length <= 300 && Artist is { Length: <= 160 }
        && double.IsFinite(PositionSeconds) && PositionSeconds is >= 0 and <= 31536000
        && double.IsFinite(DurationSeconds) && DurationSeconds is >= 0 and <= 31536000
        && double.IsFinite(PlaybackRate) && PlaybackRate is > 0 and <= 16;
}
public sealed record BrowserMediaUpdate(BrowserMedia? Media);

// Memory only. A stalled/disabled extension must never pin stale browser metadata.
public sealed class BrowserMediaStore(TimeProvider? clock = null)
{
    public const string ExtensionOrigin = "chrome-extension://fdnmkffkacgdkcajobjemocddofgkpdf";
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object gate = new();
    private BrowserMedia? current;
    private DateTimeOffset received;

    public bool Update(BrowserMedia? media)
    {
        if (media is not null && !media.IsValid) return false;
        lock (gate) { current = media; received = time.GetUtcNow(); }
        return true;
    }
    public BrowserMedia? Read()
    {
        lock (gate)
        {
            var age = time.GetUtcNow() - received;
            if (current is null || age < TimeSpan.Zero || age >= TimeSpan.FromSeconds(10)) return null;
            var position = current.PositionSeconds + (current.Playing ? age.TotalSeconds * current.PlaybackRate : 0);
            if (current.DurationSeconds > 0) position = Math.Min(position, current.DurationSeconds);
            return current with { PositionSeconds = position };
        }
    }
}

public static class BrowserMediaSelection
{
    public static bool PreferBrowser(bool browserPlaying, string? windowsApp, bool windowsPlaying)
    {
        if (string.IsNullOrEmpty(windowsApp)) return true;
        if (windowsApp.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return true;
        // Preserve the existing preference for Spotify and other playing native apps.
        return browserPlaying && !windowsPlaying;
    }
}
