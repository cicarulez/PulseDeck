using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PulseDeck.Core;

public sealed record LyricLine(double Seconds, string Text);
public sealed record LyricsSnapshot(string Status = "idle", string TrackKey = "", LyricLine[]? Lines = null, string? PlainText = null)
{
    public int CurrentLine(double seconds)
    {
        if (!double.IsFinite(seconds) || Lines is null) return -1;
        for (var i = Lines.Length - 1; i >= 0; i--) if (Lines[i].Seconds <= seconds) return i;
        return -1;
    }
}

public static class SpotifyLyrics
{
    public static bool IsSpotify(MediaSnapshot media) => media.Source == "windows" && media.Status == "connected"
        && (media.App.Equals("Spotify.exe", StringComparison.OrdinalIgnoreCase) || media.App.Equals("Spotify", StringComparison.OrdinalIgnoreCase)
            || media.App.StartsWith("SpotifyAB.SpotifyMusic_", StringComparison.OrdinalIgnoreCase) && media.App.EndsWith("!Spotify", StringComparison.OrdinalIgnoreCase));
    public static bool Eligible(MediaSnapshot media) => IsSpotify(media) && !string.IsNullOrWhiteSpace(media.Title)
        && !string.IsNullOrWhiteSpace(media.Artist) && media.Title.Length <= 300 && media.Artist.Length <= 300
        && double.IsFinite(media.PositionSeconds) && media.PositionSeconds >= 0
        && double.IsFinite(media.DurationSeconds) && media.DurationSeconds is >= 1 and <= 3600;
    public static string Key(MediaSnapshot media) => JsonSerializer.Serialize(new[] { media.Title, media.Artist, media.Album,
        Math.Round(media.DurationSeconds).ToString(CultureInfo.InvariantCulture) });
    public static bool Show(DeckState state, DeckConfig config) => config.SpotifyLyrics && state.Profile == "music"
        && (state.SpotifyTransition || Eligible(state.Media) && state.Lyrics.TrackKey == Key(state.Media) && state.Lyrics.Status is "synced" or "plain" or "loading");
    public static LyricsSnapshot Parse(JsonElement data, MediaSnapshot media)
    {
        string Str(string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        var key = Key(media);
        if (SteamGridImages.Normalize(Str("trackName")) != SteamGridImages.Normalize(media.Title)
            || SteamGridImages.Normalize(Str("artistName")) != SteamGridImages.Normalize(media.Artist)
            || !data.TryGetProperty("duration", out var duration) || !duration.TryGetDouble(out var seconds)
            || !double.IsFinite(seconds) || Math.Abs(seconds - media.DurationSeconds) > 2) return new("not-found", key);
        if (data.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True) return new("instrumental", key);
        var lrc = Str("syncedLyrics");
        var lines = ParseLrc(lrc, media.DurationSeconds);
        if (lines.Length > 0) return new("synced", key, lines);
        var plain = Str("plainLyrics");
        return plain.Length is > 0 and <= 64000 ? new("plain", key, PlainText: plain) : new("not-found", key);
    }
    public static LyricLine[] ParseLrc(string lrc, double duration)
    {
        if (lrc.Length > 64000 || !double.IsFinite(duration)) return [];
        var result = new List<LyricLine>();
        var offsetMatch = Regex.Match(lrc, @"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        var offset = offsetMatch.Success && int.TryParse(offsetMatch.Groups[1].Value, out var ms) ? ms / 1000d : 0;
        foreach (var row in lrc.Split('\n').Take(2000))
        {
            var tags = Regex.Matches(row, @"\[(\d{1,3}):([0-5]\d)(?:[.:](\d{1,3}))?\]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (tags.Count == 0) continue;
            var last = tags[^1];
            var text = new string(row[(last.Index + last.Length)..].Where(c => !char.IsControl(c)).Take(400).ToArray()).Trim();
            foreach (Match tag in tags)
            {
                var fraction = tag.Groups[3].Value;
                var time = int.Parse(tag.Groups[1].Value) * 60 + int.Parse(tag.Groups[2].Value)
                    + (fraction.Length == 0 ? 0 : int.Parse(fraction) / Math.Pow(10, fraction.Length)) - offset;
                if (time >= -5 && time <= duration + 2) result.Add(new(Math.Max(0, time), text));
            }
        }
        return result.OrderBy(l => l.Seconds).GroupBy(l => l.Seconds).Select(g => g.Last()).Take(2000).ToArray();
    }
}
