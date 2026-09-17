using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PulseDeck.Agent.Configuration;
using PulseDeck.Core;

namespace PulseDeck.Agent.Providers;

// Exact title matching only. Images and their cache never enter the source tree.
public sealed class GameArtworkProvider(HttpClient client, ConfigStore config) : IDisposable
{
    private string? title;
    private Task<GameArtworkSnapshot>? pending;
    private CancellationTokenSource? request;
    private GameArtworkSnapshot snapshot = new();
    private DateTimeOffset retryAt;
    private readonly Dictionary<string, (GameArtworkSnapshot Snapshot, DateTimeOffset Retry)> results = new();

    public GameArtworkSnapshot Read(ForegroundSnapshot? game, CancellationToken token)
    {
        var next = game?.DisplayName;
        if (next != title)
        {
            request?.Cancel(); request?.Dispose(); request = null; pending = null;
            title = next; snapshot = new(next is null ? "unavailable" : "loading"); retryAt = DateTimeOffset.MinValue;
            if (next is not null && results.TryGetValue(next, out var cached)
                && (cached.Snapshot.Path is null || File.Exists(cached.Snapshot.Path)))
            { snapshot = cached.Snapshot; retryAt = cached.Retry; }
        }
        if (next is null) return snapshot;
        if (pending?.IsCompleted == true)
        {
            snapshot = pending.GetAwaiter().GetResult(); pending = null;
            retryAt = DateTimeOffset.UtcNow.AddHours(snapshot.Status == "available" ? 24 : 1);
            if (results.Count >= 32 && !results.ContainsKey(next)) results.Remove(results.Keys.First());
            results[next] = (snapshot, retryAt);
        }
        if (pending is null && DateTimeOffset.UtcNow >= retryAt)
        {
            request?.Dispose(); request = CancellationTokenSource.CreateLinkedTokenSource(token);
            request.CancelAfter(TimeSpan.FromSeconds(15));
            pending = Fetch(next, request.Token);
        }
        return snapshot;
    }

    private async Task<GameArtworkSnapshot> Fetch(string name, CancellationToken token)
    {
        try
        {
            var directory = Path.Combine(config.DirectoryPath, "game-artwork");
            Directory.CreateDirectory(directory);
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(name))));
            var path = Path.Combine(directory, key + ".jpg");
            if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(30))
                return new("available", path, "Steam Store · titolo esatto");
            using var search = JsonDocument.Parse(await client.GetByteArrayAsync(
                "https://store.steampowered.com/api/storesearch/?cc=it&l=english&term=" + Uri.EscapeDataString(name), token));
            var matches = search.RootElement.GetProperty("items").EnumerateArray()
                .Where(i => Normalize(i.GetProperty("name").GetString() ?? "") == Normalize(name)).ToArray();
            if (matches.Length != 1) return new("unavailable");
            var id = matches[0].GetProperty("id").GetInt32();
            using var details = JsonDocument.Parse(await client.GetByteArrayAsync(
                $"https://store.steampowered.com/api/appdetails?appids={id}&cc=it&l=english", token));
            var data = details.RootElement.GetProperty(id.ToString()).GetProperty("data");
            if (Normalize(data.GetProperty("name").GetString() ?? "") != Normalize(name) || data.GetProperty("type").GetString() != "game")
                return new("unavailable");
            var url = new Uri(data.GetProperty("header_image").GetString()!);
            if (url.Scheme != "https" || !(url.Host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase)
                || url.Host.EndsWith(".steamcdn-a.akamaihd.net", StringComparison.OrdinalIgnoreCase))) return new("unavailable");
            var bytes = await client.GetByteArrayAsync(url, token);
            // Validate format/dimensions before persisting downloaded content.
            using var imageData = SkiaSharp.SKData.CreateCopy(bytes);
            using var codec = SkiaSharp.SKCodec.Create(imageData);
            if (codec is null || codec.Info.Width < 100 || codec.Info.Height < 1 || (long)codec.Info.Width * codec.Info.Height > 4 * 1024 * 1024) return new("unavailable");
            using var image = SkiaSharp.SKBitmap.Decode(codec);
            if (image is null) return new("unavailable");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllBytesAsync(temporary, bytes, token); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return new("available", path, $"Steam Store · app {id} · titolo esatto");
        }
        catch { return new("unavailable"); }
    }

    private static string Normalize(string name) => new(name.Normalize(NormalizationForm.FormD)
        .Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    public void Dispose() { request?.Cancel(); request?.Dispose(); }
}
