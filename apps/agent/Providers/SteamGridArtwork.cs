using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PulseDeck.Core;
using PulseDeck.Agent.Configuration;

namespace PulseDeck.Agent.Providers;

public sealed class SteamGridArtwork(HttpClient client, ConfigStore config, SteamGridCredentials credentials)
{
    public int Revision => credentials.Revision;
    public bool Configured => credentials.Configured;

    private async Task<JsonDocument> Request(string path, string key, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.steamgriddb.com/api/v2/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
    }
    public async Task<bool> CheckKey(string key, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) return false;
        try
        {
            using var json = await Request("search/autocomplete/Battlefield", key, token);
            return json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return false; }
    }
    public async Task<GameArtworkSnapshot> Fetch(string title, CancellationToken token)
    {
        var key = credentials.Read();
        if (key is null) return new();
        var directory = Path.Combine(config.DirectoryPath, "game-artwork");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SteamGridImages.Normalize(title))));
        var cover = Path.Combine(directory, "sgdb-" + hash + "-cover.img");
        var hero = Path.Combine(directory, "sgdb-" + hash + "-hero.img");
        bool Fresh(string path) => File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(30);
        string? coverPath = Fresh(cover) ? cover : null, heroPath = Fresh(hero) ? hero : null;
        try
        {
            Directory.CreateDirectory(directory);
            if (coverPath is null || heroPath is null)
            {
                using var search = await Request("search/autocomplete/" + Uri.EscapeDataString(title), key, token);
                var id = SteamGridImages.MatchGame(search.RootElement, title);
                if (id is not null)
                {
                    if (heroPath is null) heroPath = await FetchImage($"heroes/game/{id}?types=static&nsfw=false&humor=false", key, hero, true, token);
                    if (coverPath is null) coverPath = await FetchImage($"grids/game/{id}?types=static&dimensions=920x430,460x215&nsfw=false&humor=false", key, cover, false, token);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { } // Steam remains available if this optional service fails.
        return new(coverPath is not null || heroPath is not null ? "available" : "unavailable", coverPath ?? heroPath,
            coverPath is not null || heroPath is not null ? "SteamGridDB · titolo esatto" : null) { BackgroundPath = heroPath };
    }
    private async Task<string?> FetchImage(string query, string key, string path, bool hero, CancellationToken token)
    {
        try
        {
            using var json = await Request(query, key, token);
            foreach (var url in SteamGridImages.Candidates(json.RootElement, hero))
            {
                try
                {
                    // A fresh request: never forward the API Authorization header to the CDN.
                    var bytes = await client.GetByteArrayAsync(url, token);
                    using var data = SkiaSharp.SKData.CreateCopy(bytes);
                    using var codec = SkiaSharp.SKCodec.Create(data);
                    if (codec is null || codec.FrameCount > 1 || codec.Info.Width < 100 || codec.Info.Height < 100
                        || (long)codec.Info.Width * codec.Info.Height > 4 * 1024 * 1024) continue;
                    using var image = SkiaSharp.SKBitmap.Decode(codec);
                    if (image is null) continue;
                    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { await File.WriteAllBytesAsync(temporary, bytes, token); File.Move(temporary, path, true); }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    return path;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch { }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { }
        return null;
    }
}
