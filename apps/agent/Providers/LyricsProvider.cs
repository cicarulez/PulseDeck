using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

public sealed class LyricsProvider(HttpClient client, ConfigStore config) : IDisposable
{
    private readonly SemaphoreSlim network = new(1);
    private DateTimeOffset nextRequest;
    private readonly Dictionary<string, (LyricsSnapshot Lyrics, DateTimeOffset Until)> cache = new();
    private Task<LyricsSnapshot>? pending;
    private CancellationTokenSource? request;
    private LyricsSnapshot snapshot = new();
    private string? current;
    private DateTimeOffset retryAt;
    private DateTimeOffset rateLimitUntil;
    public LyricsSnapshot Read(MediaSnapshot media, string profile, bool enabled, CancellationToken token)
    {
        var key = enabled && profile == "music" && SpotifyLyrics.Eligible(media) ? SpotifyLyrics.Key(media) : null;
        if (key != current)
        {
            request?.Cancel(); request?.Dispose(); request = null; pending = null;
            current = key; snapshot = new(key is null ? "idle" : "loading", key ?? ""); retryAt = default;
            if (key is not null && cache.TryGetValue(key, out var saved) && saved.Until > DateTimeOffset.UtcNow)
            { snapshot = saved.Lyrics; retryAt = saved.Until; }
        }
        if (key is null) return new();
        if (pending?.IsCompleted == true)
        {
            snapshot = pending.GetAwaiter().GetResult(); pending = null;
            retryAt = DateTimeOffset.UtcNow.Add(snapshot.Status is "synced" or "plain" or "instrumental" ? TimeSpan.FromHours(24) : TimeSpan.FromMinutes(10));
            if (cache.Count >= 64) cache.Remove(cache.Keys.First());
            cache[key] = (snapshot, retryAt);
        }
        if (pending is null && DateTimeOffset.UtcNow >= retryAt)
        {
            request?.Dispose(); request = CancellationTokenSource.CreateLinkedTokenSource(token);
            request.CancelAfter(TimeSpan.FromSeconds(10));
            pending = Fetch(media, request.Token);
        }
        return snapshot;
    }
    private async Task<LyricsSnapshot> Fetch(MediaSnapshot media, CancellationToken token)
    {
        try
        {
            await network.WaitAsync(token);
            try { return await FetchSerial(media, token); }
            finally { network.Release(); }
        }
        catch { return new("unavailable", SpotifyLyrics.Key(media)); }
    }
    private async Task<LyricsSnapshot> FetchSerial(MediaSnapshot media, CancellationToken token)
    {
        var key = SpotifyLyrics.Key(media);
        var directory = Path.Combine(config.DirectoryPath, "lyrics");
        var path = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json");
        try
        {
            if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(30) && new FileInfo(path).Length <= 256 * 1024)
            {
                try { using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(path, token)); return SpotifyLyrics.Parse(saved.RootElement, media); }
                catch (JsonException) { }
            }
            if (DateTimeOffset.UtcNow < rateLimitUntil) return new("unavailable", key);
            var url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(media.Title) + "&artist_name=" + Uri.EscapeDataString(media.Artist)
                + "&duration=" + Math.Round(media.DurationSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(media.Album)) url += "&album_name=" + Uri.EscapeDataString(media.Album);
            var delay = nextRequest - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, token);
            using var response = await client.GetAsync(url, token);
            nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(300);
            if (response.StatusCode == HttpStatusCode.NotFound) return new("not-found", key);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitUntil = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));
                return new("unavailable", key);
            }
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            using var json = JsonDocument.Parse(bytes);
            var result = SpotifyLyrics.Parse(json.RootElement, media);
            if (result.Status is "synced" or "plain" or "instrumental")
            {
                Directory.CreateDirectory(directory);
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { await File.WriteAllBytesAsync(temporary, bytes, token); File.Move(temporary, path, true); }
                catch (IOException) { } // The current lyrics still work if cache storage fails.
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            return result;
        }
        catch { return new("unavailable", key); }
    }
    public void Dispose() { request?.Cancel(); request?.Dispose(); }
}
